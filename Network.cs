using System;
using System.Collections.Generic;
using System.Linq;
using Raylib_cs;
using System.Numerics;

// Réseau
using LiteNetLib;
using LiteNetLib.Utils;

// ========================================================
// LA SYNCHRONISATION EN JEU (Le coeur du multijoueur LAN)
// ========================================================
// Pendant la partie :
//  - Chaque joueur envoie sa position 60x/s au serveur (paquet non fiable, c'est ok d'en perdre)
//  - Le serveur relaie chaque position à tous les autres joueurs
//  - Les tirs et les dégâts passent en fiable (ReliableOrdered) pour ne jamais les perdre
partial class Program
{
    // La représentation d'un AUTRE joueur sur notre écran (pas de physique BEPU, juste du visuel)
    public class RemotePlayer
    {
        public int Id;
        public string Name = "???";
        public Vector3 Position;        // Position affichée (lissée pour éviter les téléportations)
        public Vector3 TargetPosition;  // Dernière position reçue du réseau
        public float Yaw;
        public float Pitch;
        public int Health = 100;
        public bool HasState;           // Tant qu'on n'a reçu aucune position, on ne le dessine pas
        public int Kills;
        public int Deaths;
    }

    // Un trait de tir d'un AUTRE joueur (purement visuel)
    public class RemoteShotVisual
    {
        public Vector3 Start;
        public Vector3 End;
        public float Timer;
    }

    public static int myPlayerId = 0;                 // Notre numéro unique (0 = l'hôte)
    public static int nextPlayerId = 1;               // Compteur d'IDs (utilisé par le serveur uniquement)
    public static string myLobbyName = "";            // Notre pseudo tel qu'il apparaît dans le lobby
    public static Dictionary<int, RemotePlayer> remotePlayers = new Dictionary<int, RemotePlayer>();
    public static Dictionary<int, NetPeer> peersParId = new Dictionary<int, NetPeer>(); // Serveur : pour router les paquets
    public static List<RemoteShotVisual> remoteShots = new List<RemoteShotVisual>();

    static NetDataWriter reusableWriter = new NetDataWriter();

    // ==========================================
    // ABONNEMENTS DES PAQUETS EN JEU
    // (Appelé par AllumerMoteurReseau)
    // ==========================================
    public static void EnregistrerPaquetsEnJeu()
    {
        netProcessor.SubscribeReusable<Packets.YourIdPacket, NetPeer>(OnYourIdReceived);
        netProcessor.SubscribeReusable<Packets.PlayerStatePacket, NetPeer>(OnPlayerStateReceived);
        netProcessor.SubscribeReusable<Packets.ShootPacket, NetPeer>(OnShootReceived);
        netProcessor.SubscribeReusable<Packets.PlayerHitPacket, NetPeer>(OnPlayerHitReceived);
        netProcessor.SubscribeReusable<Packets.PlayerDiedPacket, NetPeer>(OnPlayerDiedReceived);
        netProcessor.SubscribeReusable<Packets.PlayerLeftPacket, NetPeer>(OnPlayerLeftReceived);
    }

    // Remise à zéro de la session réseau (au moment d'allumer le moteur)
    public static void ReinitialiserSessionReseau(bool host)
    {
        remotePlayers.Clear();
        peersParId.Clear();
        remoteShots.Clear();
        myPlayerId = host ? 0 : -1; // Le client recevra son vrai ID via YourIdPacket
        nextPlayerId = 1;
    }

    // Petit utilitaire : envoyer un paquet au serveur (client) ou à tout le monde (serveur)
    static void EnvoyerAuServeur<T>(T packet, DeliveryMethod method) where T : class, new()
    {
        reusableWriter.Reset();
        netProcessor.Write(reusableWriter, packet);
        if (netManager.FirstPeer != null)
            netManager.FirstPeer.Send(reusableWriter, method);
    }

    static void DiffuserATous<T>(T packet, DeliveryMethod method, NetPeer saufLui = null) where T : class, new()
    {
        reusableWriter.Reset();
        netProcessor.Write(reusableWriter, packet);
        if (saufLui != null) netManager.SendToAll(reusableWriter, method, saufLui);
        else netManager.SendToAll(reusableWriter, method);
    }

    // Récupère (ou crée) le joueur distant correspondant à un ID
    static RemotePlayer ObtenirJoueurDistant(int id)
    {
        if (!remotePlayers.TryGetValue(id, out RemotePlayer rp))
        {
            rp = new RemotePlayer { Id = id };
            // On essaie de retrouver son pseudo grâce à la liste du lobby
            var lobbyInfo = currentLobbyPlayers.FirstOrDefault(p => p.Id == id);
            if (lobbyInfo != null) rp.Name = lobbyInfo.Name;
            remotePlayers[id] = rp;
        }
        return rp;
    }

    // ==========================================
    // RÉCEPTION DES PAQUETS
    // ==========================================
    public static void OnYourIdReceived(Packets.YourIdPacket packet, NetPeer peer)
    {
        if (!isServer)
        {
            myPlayerId = packet.PlayerId;
            Console.WriteLine($"[RÉSEAU] Le serveur m'a donné l'ID {myPlayerId}");
        }
    }

    public static void OnPlayerStateReceived(Packets.PlayerStatePacket packet, NetPeer peer)
    {
        if (packet.PlayerId == myPlayerId) return; // C'est notre propre écho, on ignore

        RemotePlayer rp = ObtenirJoueurDistant(packet.PlayerId);
        rp.TargetPosition = new Vector3(packet.X, packet.Y, packet.Z);
        rp.Yaw = packet.Yaw;
        rp.Pitch = packet.Pitch;
        rp.Health = packet.Health;
        if (!rp.HasState)
        {
            // Première nouvelle : on téléporte directement (pas de glissement depuis (0,0,0))
            rp.Position = rp.TargetPosition;
            rp.HasState = true;
        }

        // Le serveur fait le facteur : il relaie la position aux autres clients
        if (isServer)
        {
            reusableWriter.Reset();
            netProcessor.Write(reusableWriter, packet);
            netManager.SendToAll(reusableWriter, DeliveryMethod.Sequenced, peer);
        }
    }

    public static void OnShootReceived(Packets.ShootPacket packet, NetPeer peer)
    {
        if (packet.PlayerId == myPlayerId) return;

        Vector3 origin = new Vector3(packet.OriginX, packet.OriginY, packet.OriginZ);
        Vector3 dir = new Vector3(packet.DirX, packet.DirY, packet.DirZ);

        // 1. Le visuel du tir (le trait)
        remoteShots.Add(new RemoteShotVisual
        {
            Start = origin,
            End = origin + dir * packet.Distance,
            Timer = 1.0f
        });

        // 2. Le son 3D de l'arme, entendu depuis la position du tireur
        Weapon armeDuTireur = weapons.FirstOrDefault(w => w.name == packet.WeaponName);
        if (armeDuTireur != null && currentState == GameState.Playing)
        {
            PlaySound3D(armeDuTireur.soundname, origin, 150f);
        }

        // 3. Relais serveur
        if (isServer)
        {
            reusableWriter.Reset();
            netProcessor.Write(reusableWriter, packet);
            netManager.SendToAll(reusableWriter, DeliveryMethod.ReliableOrdered, peer);
        }
    }

    // Le dernier joueur qui nous a fait mal (pour savoir QUI nous a tués, même en tombant)
    public static int lastDamagerId = -1;

    public static void OnPlayerHitReceived(Packets.PlayerHitPacket packet, NetPeer peer)
    {
        if (packet.TargetId == myPlayerId)
        {
            // C'est NOUS qui sommes touchés !
            lastDamagerId = packet.ShooterId;
            localPlayer.TakeDamage(packet.Damage);
            // (L'annonce de la mort se fait dans AnnoncerMaMort, appelée par la boucle de respawn)
        }
        else if (isServer && peersParId.TryGetValue(packet.TargetId, out NetPeer cible))
        {
            // Le serveur route le paquet vers la victime
            reusableWriter.Reset();
            netProcessor.Write(reusableWriter, packet);
            cible.Send(reusableWriter, DeliveryMethod.ReliableOrdered);
        }
    }

    public static void OnPlayerDiedReceived(Packets.PlayerDiedPacket packet, NetPeer peer)
    {
        // Mise à jour des scores locaux
        if (packet.VictimId == myPlayerId)
        {
            // (deathCount est déjà incrémenté par Player.Die(), rien à faire ici)
        }
        else
        {
            ObtenirJoueurDistant(packet.VictimId).Deaths++;
        }

        if (packet.KillerId == myPlayerId)
        {
            killCount++;
            TriggerHitmarker(true); // Le son et la croix de kill !
        }
        else if (packet.KillerId >= 0 && packet.KillerId != packet.VictimId)
        {
            ObtenirJoueurDistant(packet.KillerId).Kills++;
        }

        // Le serveur diffuse l'info à tous les autres
        if (isServer)
        {
            DiffuserATous(packet, DeliveryMethod.ReliableOrdered, peer);
        }
    }

    public static void OnPlayerLeftReceived(Packets.PlayerLeftPacket packet, NetPeer peer)
    {
        remotePlayers.Remove(packet.PlayerId);
    }

    // ==========================================
    // ANNONCER NOTRE MORT (Appelé par la boucle de respawn en ligne)
    // Couvre TOUTES les morts : tirs ennemis, chute dans le vide...
    // ==========================================
    public static void AnnoncerMaMort()
    {
        if (netManager == null) return;

        Packets.PlayerDiedPacket mort = new Packets.PlayerDiedPacket
        {
            VictimId = myPlayerId,
            KillerId = lastDamagerId // -1 si on est mort tout seul (chute...)
        };
        lastDamagerId = -1;

        if (isServer)
        {
            OnPlayerDiedReceived(mort, null); // Le serveur s'applique la logique et diffuse
        }
        else
        {
            EnvoyerAuServeur(mort, DeliveryMethod.ReliableOrdered);
        }
    }

    // ==========================================
    // MISE À JOUR PAR FRAME (Appelé par BouclePrincipale)
    // ==========================================
    public static void MettreAJourReseauEnJeu(float deltaTime, Vector3 maPosition, float monYaw, float monPitch)
    {
        if (netManager == null) return;

        // 1. ENVOYER notre position (non fiable : si un paquet se perd, le suivant corrige)
        Packets.PlayerStatePacket etat = new Packets.PlayerStatePacket
        {
            PlayerId = myPlayerId,
            X = maPosition.X,
            Y = maPosition.Y,
            Z = maPosition.Z,
            Yaw = monYaw,
            Pitch = monPitch,
            Health = localPlayer.Health
        };

        if (isServer)
        {
            DiffuserATous(etat, DeliveryMethod.Sequenced);
        }
        else
        {
            EnvoyerAuServeur(etat, DeliveryMethod.Sequenced);
        }

        // 2. LISSER les positions des joueurs distants (interpolation)
        foreach (RemotePlayer rp in remotePlayers.Values)
        {
            if (!rp.HasState) continue;
            float lerp = Math.Clamp(15f * deltaTime, 0f, 1f);
            rp.Position += (rp.TargetPosition - rp.Position) * lerp;
        }

        // 3. FAIRE VIEILLIR les traits de tir distants
        for (int i = remoteShots.Count - 1; i >= 0; i--)
        {
            remoteShots[i].Timer -= deltaTime;
            if (remoteShots[i].Timer <= 0) remoteShots.RemoveAt(i);
        }
    }

    // ==========================================
    // RENDU 3D DES JOUEURS DISTANTS (Dans BeginMode3D)
    // ==========================================
    public static void DessinerJoueursDistants()
    {
        foreach (RemotePlayer rp in remotePlayers.Values)
        {
            if (!rp.HasState) continue;

            // La même capsule que le joueur local, mais orange pour les distinguer
            Vector3 pointHaut = new Vector3(rp.Position.X, rp.Position.Y + 0.5f, rp.Position.Z);
            Vector3 pointBas = new Vector3(rp.Position.X, rp.Position.Y - 0.5f, rp.Position.Z);
            Raylib.DrawCapsule(pointHaut, pointBas, 0.5f, 8, 8, Color.Orange);
            Raylib.DrawCapsuleWires(pointHaut, pointBas, 0.5f, 8, 8, Color.Black);

            // Un petit "nez" pour voir dans quelle direction il regarde
            Vector3 regard = new Vector3(
                MathF.Cos(rp.Pitch) * MathF.Sin(rp.Yaw),
                MathF.Sin(rp.Pitch),
                MathF.Cos(rp.Pitch) * MathF.Cos(rp.Yaw)
            );
            Vector3 teteCentre = pointHaut + new Vector3(0, 0.2f, 0);
            Raylib.DrawSphere(teteCentre + regard * 0.45f, 0.15f, Color.DarkGray);
        }

        // Les traits de tir des autres joueurs
        foreach (RemoteShotVisual shot in remoteShots)
        {
            int alpha = (int)(Math.Clamp(shot.Timer, 0f, 1f) * 255);
            Raylib.DrawLine3D(shot.Start, shot.End, new Color(255, 200, 80, alpha));
        }
    }

    // ==========================================
    // RENDU 2D : PSEUDOS + BARRES DE VIE AU-DESSUS DES TÊTES (Après EndMode3D)
    // ==========================================
    public static void DessinerNomsJoueursDistants(Camera3D cam, Vector3 camForward)
    {
        foreach (RemotePlayer rp in remotePlayers.Values)
        {
            if (!rp.HasState) continue;

            Vector3 tete = rp.Position + new Vector3(0, 1.4f, 0);
            Vector3 versJoueur = tete - cam.Position;
            if (Vector3.Dot(versJoueur, camForward) <= 0) continue; // Derrière nous : on n'affiche pas

            Vector2 screenPos = Raylib.GetWorldToScreen(tete, cam);

            // Le pseudo centré
            int taille = 20;
            int largeur = Raylib.MeasureText(rp.Name, taille);
            Raylib.DrawText(rp.Name, (int)screenPos.X - largeur / 2 + 1, (int)screenPos.Y - 24 + 1, taille, Color.Black);
            Raylib.DrawText(rp.Name, (int)screenPos.X - largeur / 2, (int)screenPos.Y - 24, taille, Color.White);

            // Sa barre de vie
            int barWidth = 60;
            int barHeight = 6;
            float hpPercent = Math.Clamp(rp.Health / 100f, 0f, 1f);
            int posX = (int)screenPos.X - barWidth / 2;
            int posY = (int)screenPos.Y;
            Raylib.DrawRectangle(posX, posY, barWidth, barHeight, Color.Red);
            Raylib.DrawRectangle(posX, posY, (int)(barWidth * hpPercent), barHeight, Color.Green);
            Raylib.DrawRectangleLines(posX, posY, barWidth, barHeight, Color.Black);
        }
    }

    // ==========================================
    // QUAND ON TIRE EN LIGNE : envoyer le tir + tester si on touche un joueur
    // ==========================================
    public static void TraiterTirLocalReseau(Weapon arme, Vector3 origine, Vector3 direction, Vector3 startLaser, Vector3 endLaser)
    {
        if (netManager == null) return;

        // La distance réellement parcourue par la balle (elle s'arrête au premier mur/ennemi)
        float distanceTir = (endLaser - startLaser).Length();

        // 1. ANNONCER le tir à tout le monde (visuel + son chez les autres)
        Packets.ShootPacket tir = new Packets.ShootPacket
        {
            PlayerId = myPlayerId,
            OriginX = startLaser.X,
            OriginY = startLaser.Y,
            OriginZ = startLaser.Z,
            DirX = direction.X,
            DirY = direction.Y,
            DirZ = direction.Z,
            Distance = distanceTir,
            WeaponName = arme.name
        };
        if (isServer) DiffuserATous(tir, DeliveryMethod.ReliableOrdered);
        else EnvoyerAuServeur(tir, DeliveryMethod.ReliableOrdered);

        bool isBazooka = string.Equals(arme.name, "Bazooka", StringComparison.OrdinalIgnoreCase);

        // 2. EST-CE QU'ON A TOUCHÉ UN JOUEUR ? (Même hitbox virtuelle que les ennemis)
        if (arme.range >= 10 && !isBazooka)
        {
            Ray rayonTir = new Ray(origine, direction);
            RemotePlayer meilleureCible = null;
            float meilleureDistance = distanceTir + 0.75f; // Petite marge : la balle s'arrête sur la capsule

            foreach (RemotePlayer rp in remotePlayers.Values)
            {
                if (!rp.HasState || rp.Health <= 0) continue;

                Vector3 minBox = rp.Position - new Vector3(0.5f, 1f, 0.5f);
                Vector3 maxBox = rp.Position + new Vector3(0.5f, 1.5f, 0.5f);
                BoundingBox hitbox = new BoundingBox(minBox, maxBox);

                RayCollision collision = Raylib.GetRayCollisionBox(rayonTir, hitbox);
                if (collision.Hit && collision.Distance < meilleureDistance)
                {
                    meilleureDistance = collision.Distance;
                    meilleureCible = rp;
                }
            }

            if (meilleureCible != null)
            {
                EnvoyerDegats(meilleureCible.Id, arme.damage);
                TriggerHitmarker(false);
            }
        }

        // 3. LA MÊLÉE (Couteau, Épée) : distance + angle
        if (arme.range < 10)
        {
            foreach (RemotePlayer rp in remotePlayers.Values)
            {
                if (!rp.HasState || rp.Health <= 0) continue;

                Vector3 centre = rp.Position + new Vector3(0, 0.25f, 0);
                float dist = Vector3.Distance(origine, centre);
                if (dist <= arme.range)
                {
                    Vector3 versLui = Vector3.Normalize(centre - origine);
                    if (Vector3.Dot(direction, versLui) > 0.5f)
                    {
                        EnvoyerDegats(rp.Id, arme.damage);
                        TriggerHitmarker(false);
                    }
                }
            }
        }

        // 4. LE BAZOOKA : dégâts de zone autour du point d'impact
        if (isBazooka)
        {
            Vector3 impact = origine + direction * distanceTir;
            float explosionRadius = 6f;
            foreach (RemotePlayer rp in remotePlayers.Values)
            {
                if (!rp.HasState || rp.Health <= 0) continue;
                if (Vector3.Distance(impact, rp.Position) <= explosionRadius)
                {
                    EnvoyerDegats(rp.Id, arme.damage);
                    TriggerHitmarker(false);
                }
            }
        }
    }

    static void EnvoyerDegats(int targetId, int damage)
    {
        Packets.PlayerHitPacket hit = new Packets.PlayerHitPacket
        {
            TargetId = targetId,
            ShooterId = myPlayerId,
            Damage = damage
        };

        if (isServer)
        {
            // Le serveur envoie directement à la victime
            if (peersParId.TryGetValue(targetId, out NetPeer cible))
            {
                reusableWriter.Reset();
                netProcessor.Write(reusableWriter, hit);
                cible.Send(reusableWriter, DeliveryMethod.ReliableOrdered);
            }
        }
        else
        {
            // Le client passe par le serveur qui routera
            EnvoyerAuServeur(hit, DeliveryMethod.ReliableOrdered);
        }
    }

    // ==========================================
    // FERMETURE PROPRE DU RÉSEAU (Retour menu, déconnexion serveur...)
    // ==========================================
    public static void CouperReseau()
    {
        if (netManager != null) netManager.Stop();
        isOnline = false;
        remotePlayers.Clear();
        peersParId.Clear();
        remoteShots.Clear();
        lock (_lobbyLock) { currentLobbyPlayers.Clear(); }
    }
}
