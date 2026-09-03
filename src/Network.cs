using System;
using System.Collections.Generic;
using System.Linq;
using Raylib_cs;
using System.Numerics;

// Physique (pour poser les murs reçus du réseau)
using BepuPhysics;

// Réseau
using LiteNetLib;
using LiteNetLib.Utils;

// ========================================================
// LA SYNCHRONISATION EN JEU (Le coeur du multijoueur)
// ========================================================
// Architecture Phase 0 (docs/NETWORK_ROADMAP.md) :
//  - Tick unifié 64 Hz : chaque client envoie son état à chaque tick (paquet quantifié ~17 o)
//  - L'hôte assemble UN SEUL WorldSnapshot par tick et le diffuse à tous
//    (fini le relais paquet-par-paquet en O(n²) et le bug du canal Sequenced partagé)
//  - Chaque joueur distant est INTERPOLÉ dans un tampon de snapshots (~47 ms de retard)
//    au lieu du vieux lerp qui élastiquait dès que la latence dépassait le LAN
//  - Les événements (tirs, dégâts, morts, murs) partent IMMÉDIATEMENT en fiable,
//    jamais en attente du prochain tick
// Sécurité (S1-S4) : identité déduite du peer (jamais du paquet), champs bornés,
// ressources plafonnées. Voir §5 de la roadmap.
partial class Program
{
    // Une entrée du tampon d'interpolation : l'état d'un joueur à un tick donné
    public struct SnapEntree
    {
        public uint Tick;                 // tick "étendu" (u16 du câble -> u32 sans rebouclage)
        public Packets.PlayerSnap Snap;
    }

    // La représentation d'un AUTRE joueur sur notre écran (pas de physique BEPU, juste du visuel)
    public class RemotePlayer
    {
        public int Id;
        public string Name = "???";
        public Vector3 Position;        // Position affichée (résultat de l'interpolation)
        public float Yaw;
        public float Pitch;
        public int Health = 100;
        public bool IsAlive = true;
        public bool HasState;           // Tant qu'on n'a reçu aucune position, on ne le dessine pas
        public int Kills;
        public int Deaths;
        // Son skin (reçu via PlayerInfoPacket, UNE fois — plus dans chaque paquet d'état !)
        public int SkinColor;
        public int SkinHat = -1;
        public int SkinFace = -1;
        // Son arme tenue + animations
        public int WeaponIndex;
        public bool IsReloading;
        public float ReloadStartTime;   // Heure locale du début du rechargement (pour l'animation)
        public float BobSpeed;          // Vitesse de déplacement lissée (pour le balancement de l'arme)
        public float Lean;              // Penchement wall-run (roll caméra reçu)
        public bool IsAiming;           // En visée -> arme centrée

        // --- Le tampon d'interpolation (§4.3 de la roadmap) ---
        public List<SnapEntree> Tampon = new List<SnapEntree>();
        public uint DernierTickEtendu;  // le tick le plus récent reçu pour CE joueur
        public double HorlogeRendu;     // notre curseur de lecture dans le tampon (en ticks)
        public bool HorlogeInitialisee;

        // --- Contrôle de cohérence mouvement (Phase 3, côté hôte) ---
        public Vector3 SecuDernierePos;
        public double SecuDerniereHeure;
        public bool SecuInit;
        public int SecuViolations;
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
    public static bool localIsAiming = false;          // Sommes-nous en train de viser ? (rempli par BouclePrincipale)
    public static Dictionary<int, RemotePlayer> remotePlayers = new Dictionary<int, RemotePlayer>();
    public static Dictionary<int, NetPeer> peersParId = new Dictionary<int, NetPeer>(); // Serveur : pour router les paquets
    public static List<RemoteShotVisual> remoteShots = new List<RemoteShotVisual>();

    // Le compteur de ticks local (sert de tick serveur quand on est l'hôte)
    public static uint tickLocal = 0;
    // Combien de murs chaque joueur a posés (plafond S4)
    public static Dictionary<int, int> mursParJoueur = new Dictionary<int, int>();
    // Heure du dernier tir de chaque joueur (Phase 3 : contrôle de cadence)
    public static Dictionary<int, float> dernierTirParJoueur = new Dictionary<int, float>();

    static NetDataWriter reusableWriter = new NetDataWriter();

    // ==========================================
    // ABONNEMENTS DES PAQUETS EN JEU
    // (Appelé par AllumerMoteurReseau)
    // ==========================================
    public static void EnregistrerPaquetsEnJeu()
    {
        netProcessor.SubscribeReusable<Packets.YourIdPacket, NetPeer>(OnYourIdReceived);
        netProcessor.SubscribeNetSerializable<Packets.ClientStatePacket, NetPeer>(OnClientStateReceived);
        netProcessor.SubscribeNetSerializable<Packets.WorldSnapshotPacket, NetPeer>(OnWorldSnapshotReceived);
        netProcessor.SubscribeReusable<Packets.PlayerInfoPacket, NetPeer>(OnPlayerInfoReceived);
        netProcessor.SubscribeReusable<Packets.ShootPacket, NetPeer>(OnShootReceived);
        netProcessor.SubscribeReusable<Packets.PlayerHitPacket, NetPeer>(OnPlayerHitReceived);
        netProcessor.SubscribeReusable<Packets.PlayerDiedPacket, NetPeer>(OnPlayerDiedReceived);
        netProcessor.SubscribeReusable<Packets.PlayerLeftPacket, NetPeer>(OnPlayerLeftReceived);
        netProcessor.SubscribeReusable<Packets.WallPlacedPacket, NetPeer>(OnWallPlacedReceived);
        // Cycle de vie de la session (Phase 1)
        netProcessor.SubscribeReusable<Packets.SessionSignalPacket, NetPeer>(OnSessionSignalReceived);
        netProcessor.SubscribeReusable<Packets.MatchInfoPacket, NetPeer>(OnMatchInfoReceived);
        netProcessor.SubscribeNetSerializable<Packets.WallBatchPacket, NetPeer>(OnWallBatchReceived);
        netProcessor.SubscribeNetSerializable<Packets.ScoreSnapshotPacket, NetPeer>(OnScoreSnapshotReceived);
        netProcessor.SubscribeReusable<Packets.BaselineEndPacket, NetPeer>(OnBaselineEndReceived);
        netProcessor.SubscribeNetSerializable<Packets.MatchEndPacket, NetPeer>(OnMatchEndReceived);
    }

    // Remise à zéro de la session réseau (au moment d'allumer le moteur)
    public static void ReinitialiserSessionReseau(bool host)
    {
        remotePlayers.Clear();
        peersParId.Clear();
        remoteShots.Clear();
        mursParJoueur.Clear();
        dernierTirParJoueur.Clear();
        slotsReserves.Clear();
        tokensParId.Clear();
        myPlayerId = host ? 0 : -1; // Le client recevra son vrai ID via YourIdPacket
        nextPlayerId = 1;
        tickLocal = 0;
        dernierTickServeurEtendu = 0;
        derniereErreurReseau = ""; // nouvelle tentative : l'ancien message ne s'affiche plus
    }

    // ==========================================
    // SÉCURITÉ S2 : L'IDENTITÉ VIENT DU PEER, JAMAIS DU PAQUET !
    // L'hôte range l'ID attribué dans peer.Tag au moment du Join.
    // Un paquet arrivé d'un peer sans Tag (pas encore Join) est ignoré.
    // peer == null : c'est l'hôte qui s'applique sa propre logique en local -> on lui fait confiance.
    // ==========================================
    static bool IdExpediteur(NetPeer peer, out int id)
    {
        if (peer == null) { id = myPlayerId; return true; }
        if (peer.Tag is int idTag) { id = idTag; return true; }
        id = -1;
        return false; // Paquet reçu AVANT le Join : on n'y touche pas
    }

    // ==========================================
    // SÉCURITÉ S3 : LES BORNES DES CHAMPS REÇUS
    // ==========================================
    static bool VecteurValide(Vector3 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z)
        && MathF.Abs(v.X) < 2000f && MathF.Abs(v.Z) < 2000f && v.Y > -500f && v.Y < 2000f;

    static bool DirectionValide(Vector3 d)
    {
        if (!float.IsFinite(d.X) || !float.IsFinite(d.Y) || !float.IsFinite(d.Z)) return false;
        float l = d.Length();
        return l > 0.5f && l < 1.5f; // une direction ~normalisée, pas un vecteur fou
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

    // Les variantes pour les structs INetSerializable (le chemin chaud 64 Hz)
    static void EnvoyerStructAuServeur<T>(ref T packet, DeliveryMethod method) where T : struct, INetSerializable
    {
        reusableWriter.Reset();
        netProcessor.WriteNetSerializable(reusableWriter, ref packet);
        if (netManager.FirstPeer != null)
            netManager.FirstPeer.Send(reusableWriter, method);
    }

    static void DiffuserStructATous<T>(ref T packet, DeliveryMethod method, NetPeer saufLui = null) where T : struct, INetSerializable
    {
        reusableWriter.Reset();
        netProcessor.WriteNetSerializable(reusableWriter, ref packet);
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
    // L'EXTENSION DE TICK : le câble transporte un u16 qui resboucle toutes
    // les ~17 minutes ; on le "déroule" en u32 par rapport au dernier connu.
    // ==========================================
    static uint EtendreTick(ushort recu, uint reference)
    {
        uint candidat = (reference & 0xFFFF0000u) | recu;
        long diff = (long)candidat - reference;
        if (diff > 32768 && candidat >= 65536) candidat -= 65536;
        else if (diff < -32768) candidat += 65536;
        return candidat;
    }

    // Range une snap dans le tampon d'un joueur distant (ignore le périmé)
    static void EmpilerSnap(RemotePlayer rp, uint tick, Packets.PlayerSnap snap)
    {
        if (rp.Tampon.Count > 0 && tick <= rp.DernierTickEtendu) return; // en retard : le canal Sequenced l'aurait jeté, on double la ceinture
        rp.Tampon.Add(new SnapEntree { Tick = tick, Snap = snap });
        rp.DernierTickEtendu = tick;
        // On borne la mémoire (S4) : le tampon ne grandit jamais au-delà de ~1.5 s
        while (rp.Tampon.Count > NetConfig.MaxSnapshotsBuffer) rp.Tampon.RemoveAt(0);
    }

    // ==========================================
    // RÉCEPTION DES PAQUETS
    // ==========================================
    // Notre jeton de reconnexion (client) : à représenter si on saute en plein match.
    public static int myReconnectToken = 0;

    public static void OnYourIdReceived(Packets.YourIdPacket packet, NetPeer peer)
    {
        if (!isServer)
        {
            myPlayerId = packet.PlayerId;
            myReconnectToken = packet.ReconnectToken;
            Console.WriteLine($"[RÉSEAU] Le serveur m'a donné l'ID {myPlayerId}");
            // On envoie aussitôt notre fiche d'identité (pseudo + skin), UNE fois.
            EnvoyerMonInfoJoueur();
        }
    }

    // Le client annonce son identité (pseudo + skin). L'hôte n'en croit que le contenu
    // cosmétique : l'ID est TOUJOURS déduit du peer (S2).
    public static void EnvoyerMonInfoJoueur()
    {
        if (netManager == null || isServer) return;
        Packets.PlayerInfoPacket info = new Packets.PlayerInfoPacket
        {
            PlayerId = myPlayerId,
            Name = NetConfig.NettoyerNom(myLobbyName, "Anonyme"),
            SkinColor = skinCouleur,
            SkinHat = skinHat,
            SkinFace = skinFace
        };
        EnvoyerAuServeur(info, DeliveryMethod.ReliableOrdered);
    }

    // L'hôte envoie à UN peer la fiche d'un joueur donné
    public static void EnvoyerInfoJoueurA(NetPeer destinataire, int id, string nom, int couleur, int chapeau, int visage)
    {
        Packets.PlayerInfoPacket info = new Packets.PlayerInfoPacket
        {
            PlayerId = id,
            Name = NetConfig.NettoyerNom(nom, "???"),
            SkinColor = couleur,
            SkinHat = chapeau,
            SkinFace = visage
        };
        reusableWriter.Reset();
        netProcessor.Write(reusableWriter, info);
        destinataire.Send(reusableWriter, DeliveryMethod.ReliableOrdered);
    }

    public static void OnPlayerInfoReceived(Packets.PlayerInfoPacket packet, NetPeer peer)
    {
        if (isServer)
        {
            // S2 : l'identité vient du peer, pas du champ PlayerId !
            if (!IdExpediteur(peer, out int senderId)) return;
            if (peer == null) return; // l'hôte ne s'envoie pas de fiche à lui-même

            RemotePlayer rp = ObtenirJoueurDistant(senderId);
            rp.Name = NetConfig.NettoyerNom(packet.Name, rp.Name);
            rp.SkinColor = packet.SkinColor;
            rp.SkinHat = packet.SkinHat;
            rp.SkinFace = packet.SkinFace;

            // On relaie la fiche corrigée aux autres clients
            packet.PlayerId = senderId;
            packet.Name = rp.Name;
            DiffuserATous(packet, DeliveryMethod.ReliableOrdered, peer);
        }
        else
        {
            if (packet.PlayerId == myPlayerId) return;
            bool nouveau = !remotePlayers.ContainsKey(packet.PlayerId);
            RemotePlayer rp = ObtenirJoueurDistant(packet.PlayerId);
            rp.Name = NetConfig.NettoyerNom(packet.Name, rp.Name);
            rp.SkinColor = packet.SkinColor;
            rp.SkinHat = packet.SkinHat;
            rp.SkinFace = packet.SkinFace;
            // Un nouveau venu pendant qu'on est déjà en jeu -> toast d'arrivée
            if (nouveau && currentState == GameState.Playing)
                AjouterToast($"{rp.Name} a rejoint la partie");
        }
    }

    // CLIENT -> HÔTE : l'état d'un client à chaque tick (l'hôte uniquement)
    public static void OnClientStateReceived(Packets.ClientStatePacket packet, NetPeer peer)
    {
        if (!isServer || peer == null) return;
        if (!IdExpediteur(peer, out int senderId)) return; // pas encore Join : ignoré

        Packets.PlayerSnap snap = packet.Etat;
        snap.PlayerId = (byte)senderId; // S2 : on écrase ce que le client prétend être
        // (Les positions quantifiées sont des entiers courts : pas de NaN possible par construction.)

        RemotePlayer rp = ObtenirJoueurDistant(senderId);
        uint tick = EtendreTick(packet.Tick, rp.DernierTickEtendu);
        EmpilerSnap(rp, tick, snap);

        // Phase 3 : contrôle de cohérence du mouvement (borne un client modifié, sans gêner
        // le jeu légitime — le dash/wall-run rapides restent SOUS le seuil "impossible").
        VerifierMouvement(rp, snap.Position);
    }

    // ==========================================
    // PHASE 3 — COHÉRENCE DU MOUVEMENT (host)
    // Pas de l'anti-cheat : on ne détecte QUE l'impossible (téléportation continue à
    // vitesse absurde), et il faut BEAUCOUP de violations d'affilée avant d'exclure,
    // pour ne JAMAIS punir un joueur rapide légitime.
    // ==========================================
    static void VerifierMouvement(RemotePlayer rp, Vector3 pos)
    {
        double now = Raylib.GetTime();
        if (rp.SecuInit)
        {
            double dt = now - rp.SecuDerniereHeure;
            if (dt > 0.0001)
            {
                Vector3 d = pos - rp.SecuDernierePos;
                float distH = new Vector2(d.X, d.Z).Length();
                float vitesse = distH / (float)dt;
                // > seuil ET déplacement pas gigantesque (un respawn = un seul saut énorme, ignoré)
                if (vitesse > NetConfig.VitesseHorizSuspecte && distH < 30f)
                {
                    rp.SecuViolations++;
                    if (rp.SecuViolations == 1 || rp.SecuViolations % 10 == 0)
                        Console.WriteLine($"[SÉCU] Vitesse suspecte joueur {rp.Id} : {vitesse:F0} u/s (violation {rp.SecuViolations}/{NetConfig.ViolationsAvantKick}).");
                    if (rp.SecuViolations >= NetConfig.ViolationsAvantKick)
                    {
                        Console.WriteLine($"[SÉCU] Joueur {rp.Id} exclu : vitesse incohérente persistante.");
                        KickJoueur(rp.Id, NetConfig.RejetTricheur);
                    }
                }
                else if (rp.SecuViolations > 0)
                {
                    rp.SecuViolations--; // décroissance : les pics isolés ne s'accumulent pas
                }
            }
        }
        rp.SecuDernierePos = pos;
        rp.SecuDerniereHeure = now;
        rp.SecuInit = true;
    }

    // ==========================================
    // PHASE 3 — EXCLUSION D'UN JOUEUR (host)
    // Le nettoyage (roster, remotePlayers, diffusion du départ) se fait dans
    // PeerDisconnectedEvent, exactement comme un départ normal.
    // ==========================================
    public static void KickJoueur(int id, byte raison)
    {
        if (!isServer) return;
        if (peersParId.TryGetValue(id, out NetPeer peer))
        {
            NetDataWriter w = new NetDataWriter();
            w.Put(raison);
            netManager.DisconnectPeer(peer, w);
            Console.WriteLine($"[HÔTE] Joueur {id} déconnecté ({NetConfig.MessageRejet(raison)}).");
        }
    }

    // HÔTE -> CLIENTS : le monde entier en un paquet, une fois par tick
    public static void OnWorldSnapshotReceived(Packets.WorldSnapshotPacket packet, NetPeer peer)
    {
        if (isServer) return;

        // Toutes les entrées partagent le tick serveur du paquet
        uint reference = dernierTickServeurEtendu;
        uint tick = EtendreTick(packet.ServerTick, reference);
        if (tick <= dernierTickServeurEtendu && dernierTickServeurEtendu != 0) return; // périmé
        dernierTickServeurEtendu = tick;
        heureDernierSnapshot = (float)Raylib.GetTime();

        // Le temps restant du match (autorité hôte). 0xFFFF = pas de limite.
        matchRemainingAffiche = packet.RemainingSec == 0xFFFF ? -1 : packet.RemainingSec;

        if (packet.Joueurs == null) return;
        foreach (Packets.PlayerSnap snap in packet.Joueurs)
        {
            if (snap.PlayerId == myPlayerId) continue; // notre propre écho : on s'ignore
            RemotePlayer rp = ObtenirJoueurDistant(snap.PlayerId);
            EmpilerSnap(rp, tick, snap);
        }
    }

    static uint dernierTickServeurEtendu = 0;
    static float heureDernierSnapshot = 0f; // pour l'affichage HUD "âge du snapshot"

    // Chaque arme a sa "portée sonore" cohérente : un coup de couteau ne s'entend
    // pas à 20 mètres, alors qu'un sniper résonne dans toute la map.
    public static float DistanceAudibleArme(string nomArme)
    {
        switch (nomArme)
        {
            case "Karambit": return 12f;   // le couteau : quasi silencieux
            case "Pistol":   return 60f;
            case "Revolver": return 80f;
            case "Shotgun":  return 90f;
            case "Sword":    return 90f;   // attention : la "Sword" est en réalité un fusil type SCAR
            case "Sniper":   return 160f;
            case "Bazooka":  return 130f;
            default:         return 80f;
        }
    }

    public static void OnShootReceived(Packets.ShootPacket packet, NetPeer peer)
    {
        // S2 : côté hôte, le tireur est le peer expéditeur, point final.
        if (isServer && peer != null)
        {
            if (!IdExpediteur(peer, out int senderId)) return;
            packet.PlayerId = senderId;
        }
        if (packet.PlayerId == myPlayerId) return;

        Vector3 origin = new Vector3(packet.OriginX, packet.OriginY, packet.OriginZ);
        Vector3 dir = new Vector3(packet.DirX, packet.DirY, packet.DirZ);

        // S3 : un tir aux coordonnées folles (NaN, infini, hors map) est jeté
        if (!VecteurValide(origin) || !DirectionValide(dir)) return;
        if (packet.WeaponIndex >= weapons.Count) return;
        float distance = Math.Clamp(packet.Distance, 0f, 500f);

        // Phase 3 : cadence de tir. Un client qui tire plus vite que l'arme ne le permet
        // (avec 20 % de tolérance pour la gigue réseau) voit son tir ignoré.
        if (isServer && peer != null && IdExpediteur(peer, out int tireurId))
        {
            float maintenant = (float)Raylib.GetTime();
            // La cadence de référence suit le mutateur "Cadence de tir" : sinon, un match
            // en cadence x3 verrait tous les tirs légitimes rejetés par l'anti-triche.
            float cadence = weapons[packet.WeaponIndex].CadenceEffective;
            if (dernierTirParJoueur.TryGetValue(tireurId, out float dernier)
                && maintenant - dernier < cadence * NetConfig.ToleranceCadenceTir)
            {
                Console.WriteLine($"[SÉCU] Tir trop rapide du joueur {tireurId} ({packet.WeaponIndex}) -> ignoré.");
                return;
            }
            dernierTirParJoueur[tireurId] = maintenant;
        }

        // 1. Le visuel du tir (le trait) — plafonné (S4)
        if (remoteShots.Count >= NetConfig.MaxTirsVisuels) remoteShots.RemoveAt(0);
        remoteShots.Add(new RemoteShotVisual
        {
            Start = origin,
            End = origin + dir * distance,
            Timer = 1.0f
        });

        // 2. Le son 3D de l'arme, entendu depuis la position du tireur
        Weapon armeDuTireur = weapons[packet.WeaponIndex];
        if (currentState == GameState.Playing)
        {
            PlaySound3D(armeDuTireur.soundname, origin, DistanceAudibleArme(armeDuTireur.name));
        }

        // 3. Relais serveur (avec l'ID corrigé et la distance bornée)
        if (isServer)
        {
            packet.Distance = distance;
            DiffuserATous(packet, DeliveryMethod.ReliableOrdered, peer);
        }
    }

    // Le dernier joueur qui nous a fait mal (pour savoir QUI nous a tués, même en tombant)
    public static int lastDamagerId = -1;

    public static void OnPlayerHitReceived(Packets.PlayerHitPacket packet, NetPeer peer)
    {
        // S2 : le tireur est TOUJOURS le peer expéditeur (un client ne peut pas
        // faire porter le chapeau à un autre). S3 : dégâts bornés.
        if (isServer && peer != null)
        {
            if (!IdExpediteur(peer, out int senderId)) return;
            packet.ShooterId = senderId;

            // Phase 3 : on bride les dégâts au maximum de l'arme que le tireur tient
            // réellement (vue par l'hôte via son dernier état). Un client modifié ne
            // peut donc pas envoyer un "one-shot" avec un pistolet.
            // Le plafond suit le mutateur "Dégâts" (DegatsEffectifs) : en match x3, les
            // vrais tirs passent, et un client modifié reste bridé au même maximum.
            int maxDegats = 300;
            if (remotePlayers.TryGetValue(senderId, out RemotePlayer tireur)
                && tireur.WeaponIndex >= 0 && tireur.WeaponIndex < weapons.Count)
                maxDegats = weapons[tireur.WeaponIndex].DegatsEffectifs;
            if (packet.Damage > maxDegats)
            {
                Console.WriteLine($"[SÉCU] Dégâts {packet.Damage} > max {maxDegats} du joueur {senderId} -> bridés.");
                packet.Damage = maxDegats;
            }
        }
        // Garde-fou absolu (S3). Le plafond doit rester au-dessus du pire cas légitime :
        // arme la plus forte (100) x mutateur "Dégâts" au maximum (x5) = 500.
        packet.Damage = Math.Clamp(packet.Damage, 0, 1000);

        if (packet.TargetId == myPlayerId)
        {
            // C'est NOUS qui sommes touchés !
            lastDamagerId = packet.ShooterId;
            localPlayer.TakeDamage(packet.Damage);
            // (L'annonce de la mort se fait dans AnnoncerMaMort, appelée par la boucle de respawn)
        }
        else if (isServer && !peersEnBaseline.Contains(packet.TargetId) && peersParId.TryGetValue(packet.TargetId, out NetPeer cible))
        {
            // Le serveur route le paquet vers la victime (sauf si elle est encore en hot-join)
            reusableWriter.Reset();
            netProcessor.Write(reusableWriter, packet);
            cible.Send(reusableWriter, DeliveryMethod.ReliableOrdered);
        }
    }

    public static void OnPlayerDiedReceived(Packets.PlayerDiedPacket packet, NetPeer peer)
    {
        // S2 : seule la victime annonce SA mort. Côté hôte, la victime = l'expéditeur.
        if (isServer && peer != null)
        {
            if (!IdExpediteur(peer, out int senderId)) return;
            packet.VictimId = senderId;
            // Le tueur revendiqué doit exister (ou -1 : mort toute seule)
            if (packet.KillerId != -1 && packet.KillerId != 0 && !peersParId.ContainsKey(packet.KillerId))
                packet.KillerId = -1;
        }

        // Mise à jour des scores locaux
        if (packet.VictimId == myPlayerId)
        {
            // (deathCount est déjà incrémenté par Player.Die(), rien à faire ici)
        }
        else
        {
            RemotePlayer victime = ObtenirJoueurDistant(packet.VictimId);
            victime.Deaths++;
            victime.IsAlive = false;
            victime.HasState = false;
            victime.Tampon.Clear();
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

        // Le serveur diffuse l'info à TOUT le monde, y compris la victime.
        // (Important : sans ça, la victime ne verrait jamais le kill de son tueur
        //  monter sur le tableau des scores, car elle n'applique pas la logique en local.)
        if (isServer)
        {
            DiffuserATous(packet, DeliveryMethod.ReliableOrdered);
            // Une mort peut faire atteindre la limite de kills -> fin de match
            HoteVerifierFinMatch();
        }
    }

    public static void OnPlayerLeftReceived(Packets.PlayerLeftPacket packet, NetPeer peer)
    {
        // Seul l'hôte est légitime pour annoncer un départ (le paquet vient toujours de lui)
        if (isServer && peer != null) return;
        if (remotePlayers.TryGetValue(packet.PlayerId, out RemotePlayer rp))
        {
            AjouterToast($"{rp.Name} a quitté la partie");
            remotePlayers.Remove(packet.PlayerId);
        }
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
    // LE TICK RÉSEAU 64 Hz (Appelé par la boucle à pas fixe de Jeu.cs)
    // Client : on envoie notre état. Hôte : on diffuse le snapshot du monde.
    // ==========================================
    public static void TickReseauEnJeu(Vector3 maPosition, float monYaw, float monPitch)
    {
        if (netManager == null) return;
        tickLocal++;

        Packets.PlayerSnap monEtat = Packets.PlayerSnap.Depuis(
            Math.Max(myPlayerId, 0), maPosition, monYaw, monPitch, rollActuel,
            localPlayer.Health, weapons.IndexOf(currentWeapon),
            localIsAiming, currentWeapon.isReloading);

        if (isServer)
        {
            // L'hôte assemble LE paquet du tick : son propre état + le dernier état
            // connu de chaque client (la dernière entrée de chaque tampon).
            int n = 1;
            foreach (RemotePlayer rp in remotePlayers.Values) if (rp.Tampon.Count > 0) n++;

            Packets.PlayerSnap[] tous = new Packets.PlayerSnap[n];
            tous[0] = monEtat;
            int i = 1;
            foreach (RemotePlayer rp in remotePlayers.Values)
            {
                if (rp.Tampon.Count == 0) continue;
                tous[i++] = rp.Tampon[rp.Tampon.Count - 1].Snap;
            }

            Packets.WorldSnapshotPacket snapshot = new Packets.WorldSnapshotPacket
            {
                ServerTick = (ushort)tickLocal,
                RemainingSec = SessionRemainingSec(),
                Joueurs = tous
            };
            DiffuserStructATous(ref snapshot, DeliveryMethod.Sequenced);
        }
        else
        {
            Packets.ClientStatePacket etat = new Packets.ClientStatePacket
            {
                Tick = (ushort)tickLocal,
                Etat = monEtat
            };
            EnvoyerStructAuServeur(ref etat, DeliveryMethod.Sequenced);
        }
    }

    // ==========================================
    // MISE À JOUR VISUELLE PAR FRAME (Appelé par BouclePrincipale)
    // C'est ici que vit l'INTERPOLATION : on lit le tampon de chaque joueur
    // distant avec ~3 ticks de retard (47 ms) pour absorber la gigue du WAN.
    // ==========================================
    public static void MettreAJourReseauVisuel(float deltaTime)
    {
        if (netManager == null) return;

        foreach (RemotePlayer rp in remotePlayers.Values)
        {
            if (rp.Tampon.Count == 0) continue;
            Vector3 avant = rp.Position;

            // 1. L'HORLOGE DE RENDU de ce joueur avance en temps réel, avec une
            // correction douce (±15 %) vers la cible "dernier tick - retard".
            double cible = (double)rp.DernierTickEtendu - NetConfig.InterpTicks;
            if (!rp.HorlogeInitialisee)
            {
                rp.HorlogeRendu = cible;
                rp.HorlogeInitialisee = true;
            }
            double derive = cible - rp.HorlogeRendu;
            if (Math.Abs(derive) > 16.0)
                rp.HorlogeRendu = cible; // trop décalé (freeze, alt-tab...) : resynchro franche
            else
                rp.HorlogeRendu += deltaTime * NetConfig.TickRate * (1.0 + Math.Clamp(derive * 0.05, -0.15, 0.15));

            // 2. ÉCHANTILLONNAGE du tampon à l'instant HorlogeRendu
            EchantillonnerTampon(rp);

            // 3. On purge le vieux passé du tampon (mais on garde toujours 2 entrées)
            while (rp.Tampon.Count > 2 && rp.Tampon[1].Tick < rp.HorlogeRendu - 2.0)
                rp.Tampon.RemoveAt(0);

            // 4. On estime sa vitesse horizontale (pour le balancement de son arme)
            float deplaceHoriz = new Vector2(rp.Position.X - avant.X, rp.Position.Z - avant.Z).Length();
            float vitesseInst = deltaTime > 0f ? deplaceHoriz / deltaTime : 0f;
            rp.BobSpeed += (vitesseInst - rp.BobSpeed) * 0.15f; // lissage
        }

        // FAIRE VIEILLIR les traits de tir distants
        for (int i = remoteShots.Count - 1; i >= 0; i--)
        {
            remoteShots[i].Timer -= deltaTime;
            if (remoteShots[i].Timer <= 0) remoteShots.RemoveAt(i);
        }

        MettreAJourStatsHud(deltaTime);
    }

    // Lit le tampon d'un joueur au temps rp.HorlogeRendu et remplit ses champs affichés.
    static void EchantillonnerTampon(RemotePlayer rp)
    {
        var tampon = rp.Tampon;
        double t = rp.HorlogeRendu;

        Packets.PlayerSnap resultatDiscret; // santé/arme/flags : toujours pris du plus récent des deux
        Vector3 pos;
        float yaw, pitch, lean;

        if (t <= tampon[0].Tick || tampon.Count == 1)
        {
            // On est encore avant la première entrée : on prend telle quelle
            var s = tampon[0].Snap;
            pos = s.Position; yaw = s.Yaw; pitch = s.Pitch; lean = s.Lean;
            resultatDiscret = s;
        }
        else if (t >= tampon[tampon.Count - 1].Tick)
        {
            // On a dépassé la dernière entrée (paquets perdus / en retard) :
            // EXTRAPOLATION bornée à 150 ms, puis on fige.
            var b = tampon[tampon.Count - 1];
            var a = tampon[tampon.Count - 2];
            double depassement = t - b.Tick;
            double maxTicks = NetConfig.ExtrapolationMaxSec * NetConfig.TickRate;
            if (depassement > maxTicks) depassement = maxTicks;

            double dt = b.Tick - a.Tick;
            Vector3 vitesse = dt > 0 ? (b.Snap.Position - a.Snap.Position) / (float)dt : Vector3.Zero;
            pos = b.Snap.Position + vitesse * (float)depassement;
            yaw = b.Snap.Yaw; pitch = b.Snap.Pitch; lean = b.Snap.Lean;
            resultatDiscret = b.Snap;
        }
        else
        {
            // Le cas nominal : t est encadré par deux entrées -> interpolation
            int i = tampon.Count - 2;
            while (i > 0 && tampon[i].Tick > t) i--;
            var a = tampon[i];
            var b = tampon[i + 1];

            Vector3 posA = a.Snap.Position, posB = b.Snap.Position;
            if (Vector3.Distance(posA, posB) > NetConfig.SeuilTeleportation)
            {
                // Téléportation (respawn) : on saute, on ne glisse pas à travers la map !
                pos = posB; yaw = b.Snap.Yaw; pitch = b.Snap.Pitch; lean = b.Snap.Lean;
            }
            else
            {
                float alpha = (float)((t - a.Tick) / (double)(b.Tick - a.Tick));
                pos = Vector3.Lerp(posA, posB, alpha);
                yaw = a.Snap.Yaw + DeltaAngle(a.Snap.Yaw, b.Snap.Yaw) * alpha;
                pitch = a.Snap.Pitch + (b.Snap.Pitch - a.Snap.Pitch) * alpha;
                lean = a.Snap.Lean + (b.Snap.Lean - a.Snap.Lean) * alpha;
            }
            resultatDiscret = b.Snap;
        }

        rp.Position = pos;
        rp.Yaw = yaw;
        rp.Pitch = pitch;
        rp.Lean = lean;
        rp.Health = resultatDiscret.Health;
        if (!rp.IsAlive && rp.Health >= Math.Max(1, MatchRules.VieMax))
            rp.IsAlive = true;
        rp.WeaponIndex = resultatDiscret.WeaponIndex;
        rp.IsAiming = resultatDiscret.Vise;
        // Détection du DÉBUT d'un rechargement (front montant) -> on lance l'animation localement
        if (resultatDiscret.Recharge && !rp.IsReloading)
            rp.ReloadStartTime = (float)Raylib.GetTime();
        rp.IsReloading = resultatDiscret.Recharge;
        rp.HasState = true;
    }

    // Le plus court chemin angulaire entre deux yaw (gère le passage 359° -> 1°)
    static float DeltaAngle(float a, float b)
    {
        float d = (b - a) % (2f * MathF.PI);
        if (d > MathF.PI) d -= 2f * MathF.PI;
        if (d < -MathF.PI) d += 2f * MathF.PI;
        return d;
    }

    // ==========================================
    // LE HUD RÉSEAU (F3) : ping, débits, âge du snapshot, profondeur des tampons
    // ==========================================
    public static string hudReseauLigne1 = "";
    public static string hudReseauLigne2 = "";
    static float statsChrono = 0f;
    static long statsBytesInPrec, statsBytesOutPrec, statsPktInPrec, statsPktOutPrec;

    static void MettreAJourStatsHud(float deltaTime)
    {
        statsChrono += deltaTime;
        if (statsChrono < 0.5f || netManager == null) return;

        var stats = netManager.Statistics;
        float dIn = (stats.BytesReceived - statsBytesInPrec) / statsChrono / 1024f;
        float dOut = (stats.BytesSent - statsBytesOutPrec) / statsChrono / 1024f;
        float pIn = (stats.PacketsReceived - statsPktInPrec) / statsChrono;
        float pOut = (stats.PacketsSent - statsPktOutPrec) / statsChrono;
        statsBytesInPrec = stats.BytesReceived;
        statsBytesOutPrec = stats.BytesSent;
        statsPktInPrec = stats.PacketsReceived;
        statsPktOutPrec = stats.PacketsSent;
        statsChrono = 0f;

        int ping = 0;
        if (!isServer && netManager.FirstPeer != null) ping = netManager.FirstPeer.Ping;
        else if (isServer)
        {
            int somme = 0, nb = 0;
            foreach (var p in peersParId.Values) { somme += p.Ping; nb++; }
            if (nb > 0) ping = somme / nb;
        }

        float profondeurMoy = 0f; int nbRp = 0;
        foreach (var rp in remotePlayers.Values) { profondeurMoy += rp.Tampon.Count; nbRp++; }
        if (nbRp > 0) profondeurMoy /= nbRp;

        float ageSnapshot = isServer ? 0f : ((float)Raylib.GetTime() - heureDernierSnapshot) * 1000f;

        hudReseauLigne1 = $"NET {(isServer ? "HOTE" : "CLIENT")} | ping {ping} ms | IN {dIn:F1} ko/s ({pIn:F0} pkt/s) | OUT {dOut:F1} ko/s ({pOut:F0} pkt/s)";
        hudReseauLigne2 = $"tick {tickLocal} | snapshot age {ageSnapshot:F0} ms | tampon moyen {profondeurMoy:F1} | joueurs {remotePlayers.Count + 1}";
    }

    // ==========================================
    // RENDU 3D DES JOUEURS DISTANTS (Dans BeginMode3D)
    // ==========================================
    public static void DessinerJoueursDistants()
    {
        foreach (RemotePlayer rp in remotePlayers.Values)
        {
            if (!rp.HasState || !rp.IsAlive) continue;

            // Le personnage complet avec son skin : couleur, chapeau et tête !
            // (+ le penchement wall-run, purement visuel)
            DessinerPersonnageComplet(rp.Position, rp.Yaw, rp.Pitch, rp.SkinColor, rp.SkinHat, rp.SkinFace, rp.Lean);

            // Son arme tenue (le bon modèle 3D, orienté + animé)
            DessinerArmeJoueurDistant(rp);
        }

        // Les traits de tir des autres joueurs
        foreach (RemoteShotVisual shot in remoteShots)
        {
            int alpha = (int)(Math.Clamp(shot.Timer, 0f, 1f) * 255);
            Raylib.DrawLine3D(shot.Start, shot.End, new Color(255, 200, 80, alpha));
        }
    }

    // ==========================================
    // L'ARME D'UN JOUEUR DISTANT : bon modèle 3D, orienté selon son regard,
    // avec balancement (vitesse) et animation de rechargement.
    // ==========================================
    // --- Constantes d'ajustement visuel (à régler à l'œil si besoin) ---
    const float ARME_ECHELLE = 1.0f;         // les modèles 3D sont en 1:1 (1 unité = 1 mètre)
    const float ARME_OFFSET_DROITE = 0.50f;  // décalage vers la main droite
    const float ARME_OFFSET_AVANT = 0.60f;   // décalage vers l'avant
    const float ARME_OFFSET_HAUT = 0.22f;    // hauteur (épaules)
    const float ARME_ROT_BASE = 0f;          // orientation de base du modèle (selon l'export Blender)
    // Bornes du pitch de l'arme : on bloque la rotation pour qu'elle ne traverse
    // jamais le corps quand le joueur regarde le sol ou le ciel.
    const float ARME_PITCH_MIN = -0.55f;     // ~-31° (vers le bas)
    const float ARME_PITCH_MAX = 0.75f;      // ~+43° (vers le haut)
    // --- Pose de VISÉE (clic droit) ---
    const float ARME_VISEE_HAUT = 0.48f;     // hauteur (yeux)
    const float ARME_VISEE_COTE = 0.70f;     // décalage latéral en visée (signe à inverser si mauvais côté)
    const float ARME_VISEE_AVANT = 0.40f;    // distance devant le visage (plus petit = plus proche)

    public static void DessinerArmeJoueurDistant(RemotePlayer rp)
    {
        if (rp.WeaponIndex < 0 || rp.WeaponIndex >= weapons.Count) return;
        Weapon arme = weapons[rp.WeaponIndex];

        // Repère horizontal du joueur (même convention que CamFroward : facing = (sin yaw, *, cos yaw))
        Vector3 forwardH = new Vector3(MathF.Sin(rp.Yaw), 0f, MathF.Cos(rp.Yaw));
        Vector3 right = new Vector3(MathF.Cos(rp.Yaw), 0f, -MathF.Sin(rp.Yaw));

        Vector3 mainPos;
        float pitchArme;

        if (rp.IsAiming)
        {
            // VISÉE : l'arme remonte devant le visage (pose "épaulée" que voit l'adversaire).
            // ARME_VISEE_COTE recadre le côté (le modèle a son origine sur la crosse) ;
            // ARME_VISEE_AVANT la rapproche du visage. Inverser le signe de COTE si du mauvais côté.
            mainPos = rp.Position
                    + new Vector3(0f, ARME_VISEE_HAUT, 0f)
                    + right * ARME_VISEE_COTE
                    + forwardH * ARME_VISEE_AVANT;
            pitchArme = Math.Clamp(rp.Pitch, -0.9f, 0.9f); // pitch complet : on vise droit
        }
        else
        {
            // HANCHE : à hauteur d'épaule, côté droit, un peu en avant, avec balancement
            mainPos = rp.Position
                    + new Vector3(0f, ARME_OFFSET_HAUT, 0f)
                    + right * ARME_OFFSET_DROITE
                    + forwardH * ARME_OFFSET_AVANT;
            float ampliBob = Math.Clamp(rp.BobSpeed * 0.006f, 0f, 0.06f);
            mainPos.Y += MathF.Sin((float)Raylib.GetTime() * 10f) * ampliBob;
            // Pitch borné hors visée : l'arme ne balaye pas le torse aux angles extrêmes.
            pitchArme = Math.Clamp(rp.Pitch, ARME_PITCH_MIN, ARME_PITCH_MAX);
        }

        // Animation de rechargement (l'arme bascule puis se redresse)
        float reloadTilt = 0f;
        if (rp.IsReloading)
        {
            float elapsed = (float)Raylib.GetTime() - rp.ReloadStartTime;
            reloadTilt = AngleRechargement(elapsed, arme.reloadtime);
        }

        // On empile une matrice : translation -> orientation -> dessin du modèle à l'origine
        Rlgl.PushMatrix();
        Rlgl.Translatef(mainPos.X, mainPos.Y, mainPos.Z);
        Rlgl.Rotatef(rp.Yaw * RadVersDeg + ARME_ROT_BASE, 0f, 1f, 0f); // orientation horizontale
        Rlgl.Rotatef(-pitchArme * RadVersDeg, 1f, 0f, 0f);             // visée haut/bas (bornée)
        Rlgl.Rotatef(reloadTilt, 1f, 0f, 0f);                          // inclinaison de rechargement
        Raylib.DrawModel(arme.modelname, Vector3.Zero, ARME_ECHELLE, Color.White);
        Rlgl.PopMatrix();
    }

    // ==========================================
    // LES MURS CONSTRUITS (Touche F) EN MULTIJOUEUR
    // Un mur posé doit exister chez TOUT le monde : en visuel ET en physique,
    // sinon les autres passent au travers sans même le voir !
    // ==========================================
    // 'couleurIdx' = la couleur de skin du POSEUR (index dans couleursSkin) : chaque
    // mur garde la couleur de celui qui l'a construit, même en réseau. On la borne ici,
    // une seule fois, pour que le rendu puisse indexer couleursSkin sans se poser de question.
    public static void PoserMur(Vector3 position, Quaternion rotation, int couleurIdx = 0)
    {
        StaticDescription description = new StaticDescription(position, rotation, formeMurIndex);
        StaticHandle handle = simulation.Statics.Add(description);
        listeMur.Add(new MurPose(position, rotation, handle, Math.Clamp(couleurIdx, 0, couleursSkin.Length - 1)));

        // ==========================================
        // NAVGRID DYNAMIQUE : le mur rend des cases inmarchables -> les zombies contournent.
        // (Solo uniquement : en réseau la grille est vide, BloquerBoite ne fait rien.)
        // On calcule l'AABB monde du mur (Box 2x2x0.5 tournée) via ses 8 coins, puis on
        // bloque les cases couvertes dont l'espace debout du zombie chevauche le mur.
        // ==========================================
        if (NavGrid.EstPrete)
        {
            Vector3 demi = new Vector3(1f, 1f, 0.25f); // demi-dimensions de la Box(2,2,0.5)
            Vector3 mn = new Vector3(float.MaxValue), mx = new Vector3(float.MinValue);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 coin = position + Vector3.Transform(new Vector3(sx * demi.X, sy * demi.Y, sz * demi.Z), rotation);
                        mn = Vector3.Min(mn, coin);
                        mx = Vector3.Max(mx, coin);
                    }

            List<Vector3> bloquees = NavGrid.BloquerBoite(mn, mx);
            if (bloquees.Count > 0)
                foreach (Enemy e in enemiesList)
                    e.NotifierZoneBloquee(bloquees);
        }
    }

    // Plafonds S4 : un client (même modifié) ne peut pas noyer la partie sous les murs
    public static bool PeutPoserMur(int joueurId)
    {
        if (listeMur.Count >= NetConfig.MaxMursTotal) return false;
        mursParJoueur.TryGetValue(joueurId, out int deja);
        return deja < NetConfig.MaxMursParJoueur;
    }

    static void CompterMurPose(int joueurId)
    {
        mursParJoueur.TryGetValue(joueurId, out int deja);
        mursParJoueur[joueurId] = deja + 1;
    }

    public static void AnnoncerMurPose(Vector3 position, Quaternion rotation)
    {
        CompterMurPose(myPlayerId);
        Packets.WallPlacedPacket paquet = new Packets.WallPlacedPacket
        {
            PlayerId = myPlayerId,
            X = position.X, Y = position.Y, Z = position.Z,
            QX = rotation.X, QY = rotation.Y, QZ = rotation.Z, QW = rotation.W,
            SkinColor = (byte)skinCouleur
        };
        if (isServer) DiffuserATous(paquet, DeliveryMethod.ReliableOrdered);
        else EnvoyerAuServeur(paquet, DeliveryMethod.ReliableOrdered);
    }

    public static void OnWallPlacedReceived(Packets.WallPlacedPacket packet, NetPeer peer)
    {
        // S2 : côté hôte, le poseur est le peer expéditeur
        if (isServer && peer != null)
        {
            if (!IdExpediteur(peer, out int senderId)) return;
            packet.PlayerId = senderId;
        }
        if (packet.PlayerId == myPlayerId) return; // notre propre écho

        Vector3 pos = new Vector3(packet.X, packet.Y, packet.Z);
        Quaternion rot = new Quaternion(packet.QX, packet.QY, packet.QZ, packet.QW);

        // S3 : position saine + quaternion fini et ~unitaire, sinon on jette
        if (!VecteurValide(pos)) return;
        if (!float.IsFinite(rot.X) || !float.IsFinite(rot.Y) || !float.IsFinite(rot.Z) || !float.IsFinite(rot.W)) return;
        float normeQ = rot.Length();
        if (normeQ < 0.9f || normeQ > 1.1f) return;

        // S4 : plafond de murs (l'hôte fait autorité ; un client légitime ne l'atteint jamais
        // car il applique le même plafond avant de poser)
        if (isServer && !PeutPoserMur(packet.PlayerId)) return;
        CompterMurPose(packet.PlayerId);

        PoserMur(pos, rot, packet.SkinColor);
        PlaySound3D(wallSound, pos, 20f);

        // Le serveur relaie le mur aux autres clients
        if (isServer)
        {
            DiffuserATous(packet, DeliveryMethod.ReliableOrdered, peer);
        }
    }

    // L'angle d'inclinaison de l'arme pendant le rechargement (copie de Weapon.GetReloadRotationAngle,
    // mais piloté par le temps réseau local pour rester synchrone visuellement).
    static float AngleRechargement(float elapsed, float reloadtime)
    {
        if (elapsed < 0f || elapsed >= reloadtime) return 0f;
        float thirdTime = reloadtime / 5f;
        float twoThirdTime = 3f * reloadtime / 5f;
        if (elapsed <= thirdTime) return -45f * (elapsed / thirdTime);
        if (elapsed <= twoThirdTime) return -45f;
        float p = (elapsed - twoThirdTime) / thirdTime;
        float a = -45f + 45f * p;
        return a >= 0f ? 0f : a;
    }

    // ==========================================
    // RENDU 2D : PSEUDOS + BARRES DE VIE AU-DESSUS DES TÊTES (Après EndMode3D)
    // ==========================================
    public static void DessinerNomsJoueursDistants(Camera3D cam, Vector3 camForward)
    {
        foreach (RemotePlayer rp in remotePlayers.Values)
        {
            if (!rp.HasState || !rp.IsAlive) continue;

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
            // La barre se remplit par rapport à la vie max DU MATCH (mutateur "Vie maximale")
            float hpPercent = Math.Clamp(rp.Health / (float)Math.Max(1, MatchRules.VieMax), 0f, 1f);
            int posX = (int)screenPos.X - barWidth / 2;
            int posY = (int)screenPos.Y;
            Raylib.DrawRectangle(posX, posY, barWidth, barHeight, Color.Red);
            Raylib.DrawRectangle(posX, posY, (int)(barWidth * hpPercent), barHeight, Color.Green);
            Raylib.DrawRectangleLines(posX, posY, barWidth, barHeight, Color.Black);
        }
    }

    // ==========================================
    // QUAND ON TIRE EN LIGNE : envoyer le tir + tester si on touche un joueur
    // (Envoyé IMMÉDIATEMENT, jamais mis en attente du prochain tick !)
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
            WeaponIndex = (byte)Math.Clamp(weapons.IndexOf(arme), 0, 255)
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

                // La MÊME boîte que pour les zombies (Weapon.Shoot) : elle épouse la
                // capsule physique (Y-1 à Y+1). Elle s'arrête au sommet du crâne, donc
                // un tir dans le chapeau ne touche personne : les cosmétiques ne portent
                // aucune hitbox, ils ne bloquent rien et n'encaissent rien.
                Vector3 minBox = rp.Position - new Vector3(0.5f, 1f, 0.5f);
                Vector3 maxBox = rp.Position + new Vector3(0.5f, 1f, 0.5f);
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
                EnvoyerDegats(meilleureCible.Id, arme.DegatsEffectifs);
                TriggerHitmarker(false);
            }
        }

        // 3. LA MÊLÉE (Couteau, Épée) : distance + angle
        if (arme.range < 10)
        {
            foreach (RemotePlayer rp in remotePlayers.Values)
            {
                if (!rp.HasState || rp.Health <= 0) continue;

                // Le centre de la hitbox = le centre du corps (la boîte va de Y-1 à Y+1)
                Vector3 centre = rp.Position;
                float dist = Vector3.Distance(origine, centre);
                if (dist <= arme.range)
                {
                    Vector3 versLui = Vector3.Normalize(centre - origine);
                    if (Vector3.Dot(direction, versLui) > 0.5f)
                    {
                        EnvoyerDegats(rp.Id, arme.DegatsEffectifs);
                        TriggerHitmarker(false);
                    }
                }
            }
        }

        // 4. LE BAZOOKA : dégâts de zone autour du point d'impact
        if (isBazooka)
        {
            Vector3 impact = origine + direction * distanceTir;
            float explosionRadius = Weapon.RayonExplosion; // mutateur "Puissance explosions"
            foreach (RemotePlayer rp in remotePlayers.Values)
            {
                if (!rp.HasState || rp.Health <= 0) continue;
                if (Vector3.Distance(impact, rp.Position) <= explosionRadius)
                {
                    EnvoyerDegats(rp.Id, arme.DegatsEffectifs);
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
            // Le serveur envoie directement à la victime (sauf si elle est encore en hot-join)
            if (!peersEnBaseline.Contains(targetId) && peersParId.TryGetValue(targetId, out NetPeer cible))
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
        // On retire notre salon de l'annuaire avant de couper (sinon il traîne jusqu'au TTL)
        if (isServer) HoteFermerSalon();
        if (netManager != null) netManager.Stop();
        isOnline = false;
        remotePlayers.Clear();
        peersParId.Clear();
        remoteShots.Clear();
        mursParJoueur.Clear();
        dernierTirParJoueur.Clear();
        dernierTickServeurEtendu = 0;
        lock (_lobbyLock) { currentLobbyPlayers.Clear(); }
    }
}
