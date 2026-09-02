using System;
using System.Collections.Generic;
using System.Linq;
using Raylib_cs;
using System.Numerics;
using BepuPhysics;
using LiteNetLib;
using LiteNetLib.Utils;

// ========================================================
// LE CYCLE DE VIE DE LA SESSION EN LIGNE (Phase 1 de la roadmap WAN)
// ========================================================
// Machine à états HOST-AUTHORITATIVE :
//   Lobby --(host lance)--> Playing --(limite atteinte)--> PostMatch --(10 s)--> Lobby (boucle)
// L'hôte décide de TOUTES les transitions et les diffuse en fiable ; le client suit.
// Les sockets restent OUVERTS entre deux matchs : on ne retourne JAMAIS au menu
// principal en fin de partie (c'était le défaut de l'ancien code).
//
// HOT-JOIN : un retardataire est accepté en pleine partie ; l'hôte lui envoie une
// "baseline" (map + règles + murs déjà posés + scores + point de spawn) avant qu'il
// n'apparaisse, pour qu'il voie exactement le même monde que les autres.
partial class Program
{
    // ==========================================
    // ÉTAT DU MATCH (règles + horloge)
    // ==========================================
    // Config choisie par l'hôte dans le lobby (et rejouée d'un match à l'autre)
    public static int matchScoreLimitConfig = 20;   // premier à N kills gagne (0 = pas de limite)
    public static int matchTimeLimitConfig = 600;   // durée max en secondes (0 = pas de limite)

    // Règles effectives du match EN COURS (copiées au lancement)
    public static int matchScoreLimit = 20;
    public static int matchTimeLimit = 600;
    public static double matchStartTime = 0;        // Raylib.GetTime() au lancement (hôte : autorité)
    public static bool matchEnCours = false;        // vrai pendant Playing en ligne
    public static int matchRemainingAffiche = -1;   // temps restant affiché (client : lu du snapshot ; -1 = pas de limite)
    public static bool partieEnCoursCoteHote = false; // client : vrai s'il a quitté vers le salon alors qu'un match tourne (-> bouton REJOINDRE)

    // Modificateur aléatoire (règle "chaos") : compte à rebours avant le prochain tirage.
    public const float ChaosPeriode = 60f;
    public static float chaosTimer = ChaosPeriode;

    // Écran de fin de match (PostMatch)
    public const float PostMatchDuree = 10f;
    public static float postMatchTimer = 0f;
    public static List<Packets.ScoreEntry> dernierMatchScores = new List<Packets.ScoreEntry>();
    public static int dernierMatchWinnerId = -1;
    public static string dernierMatchWinnerNom = "";

    // Hot-join côté hôte : les peers connectés en pleine partie mais pas encore
    // "baseline terminée" -> on ne leur route aucun dégât tant qu'ils n'ont pas spawn.
    public static HashSet<int> peersEnBaseline = new HashSet<int>();

    // ==========================================
    // RECONNEXION (Task 3.4) : la "grâce" de 30 s
    // Quand un joueur saute en plein match, on NE purge PAS immédiatement sa place :
    // on garde son ID, son score et son skin en réserve pendant 30 s. S'il revient
    // avec le bon (Id, jeton), il reprend exactement où il en était.
    // ==========================================
    public const float GraceReconnexionSec = 30f;

    public class SlotReserve
    {
        public int Id;
        public string Name = "";
        public int Token;
        public int Kills, Deaths;
        public int SkinColor, SkinHat = -1, SkinFace = -1;
        public float GraceRestant;
    }

    public static Dictionary<int, SlotReserve> slotsReserves = new Dictionary<int, SlotReserve>();
    public static Dictionary<int, int> tokensParId = new Dictionary<int, int>(); // Id -> jeton (hôte)
    static readonly Random rngToken = new Random();

    // Petits messages éphémères (arrivées / départs) affichés en haut de l'écran
    public struct Toast { public string Texte; public float Timer; }
    public static List<Toast> toasts = new List<Toast>();

    public static void AjouterToast(string texte)
    {
        toasts.Add(new Toast { Texte = texte, Timer = 3.5f });
        if (toasts.Count > 5) toasts.RemoveAt(0);
    }

    public static void MettreAJourToasts(float dt)
    {
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            Toast t = toasts[i];
            t.Timer -= dt;
            toasts[i] = t;
            if (t.Timer <= 0) toasts.RemoveAt(i);
        }
    }

    // ==========================================
    // LE TICK DE SESSION PAR FRAME (appelé par la boucle principale quand isOnline)
    // ==========================================
    // ==========================================
    // HÔTE : mettre un joueur déconnecté "en réserve" au lieu de le purger (Task 3.4)
    // Renvoie true si la place a été réservée (match en cours), false sinon (purge normale).
    // ==========================================
    public static bool HoteReserverSlot(LobbyPlayer partant)
    {
        if (!isServer || partant == null || !matchEnCours) return false;

        int id = partant.Id;
        remotePlayers.TryGetValue(id, out RemotePlayer rp);
        tokensParId.TryGetValue(id, out int token);

        slotsReserves[id] = new SlotReserve
        {
            Id = id,
            Name = partant.Name,
            Token = token,
            Kills = rp?.Kills ?? 0,
            Deaths = rp?.Deaths ?? 0,
            SkinColor = rp?.SkinColor ?? 0,
            SkinHat = rp?.SkinHat ?? -1,
            SkinFace = rp?.SkinFace ?? -1,
            GraceRestant = GraceReconnexionSec
        };

        // On coupe le lien "peer" mais on garde l'entrée dans le roster + le score.
        partant.Peer = null;
        peersParId.Remove(id);
        peersEnBaseline.Remove(id);
        if (rp != null) { rp.HasState = false; rp.Tampon.Clear(); rp.HorlogeInitialisee = false; }

        AjouterToast($"{partant.Name} déconnecté — reconnexion possible {(int)GraceReconnexionSec}s");
        Console.WriteLine($"[SESSION] Slot du joueur {id} réservé {GraceReconnexionSec}s (reconnexion).");
        return true;
    }

    // Le compte à rebours des réserves : à expiration -> purge définitive.
    static void TickReconnexionHote(float dt)
    {
        if (slotsReserves.Count == 0) return;
        List<int> expires = null;
        foreach (var kv in slotsReserves)
        {
            kv.Value.GraceRestant -= dt;
            if (kv.Value.GraceRestant <= 0f) (expires ??= new List<int>()).Add(kv.Key);
        }
        if (expires == null) return;

        foreach (int id in expires)
        {
            SlotReserve slot = slotsReserves[id];
            slotsReserves.Remove(id);
            tokensParId.Remove(id);
            remotePlayers.Remove(id);
            lock (_lobbyLock) currentLobbyPlayers.RemoveAll(p => p.Id == id);

            // Maintenant seulement on annonce le départ définitif aux autres
            Packets.PlayerLeftPacket depart = new Packets.PlayerLeftPacket { PlayerId = id };
            DiffuserATous(depart, DeliveryMethod.ReliableOrdered);
            AjouterToast($"{slot.Name} n'est pas revenu");
            Console.WriteLine($"[SESSION] Grâce expirée : joueur {id} purgé définitivement.");
        }
        MettreAJourEtDiffuserLobby();
    }

    // HÔTE : tente de rebrancher un joueur qui revient sur une place réservée.
    // Renvoie l'ID réutilisé si la reconnexion est valide, sinon -1 (join normal).
    public static int HoteTenterReconnexion(NetPeer peer, Packets.JoinPacket passeport)
    {
        if (passeport.ReconnectId < 0) return -1;
        if (!slotsReserves.TryGetValue(passeport.ReconnectId, out SlotReserve slot)) return -1;
        if (slot.Token == 0 || slot.Token != passeport.ReconnectToken) return -1;

        int id = slot.Id;
        slotsReserves.Remove(id);

        // On restaure l'identité + le score dans remotePlayers
        RemotePlayer rp = ObtenirJoueurDistant(id);
        rp.Name = slot.Name;
        rp.Kills = slot.Kills;
        rp.Deaths = slot.Deaths;
        rp.SkinColor = slot.SkinColor;
        rp.SkinHat = slot.SkinHat;
        rp.SkinFace = slot.SkinFace;

        // On recolle le peer à l'ancien ID
        peer.Tag = id;
        peersParId[id] = peer;

        lock (_lobbyLock)
        {
            var entry = currentLobbyPlayers.Find(p => p.Id == id);
            if (entry != null) entry.Peer = peer;
            else currentLobbyPlayers.Add(new LobbyPlayer { Name = slot.Name, IsHost = false, IsReady = true, Id = id, Peer = peer });
        }

        AjouterToast($"{slot.Name} s'est reconnecté !");
        Console.WriteLine($"[SESSION] Joueur {id} reconnecté sur sa place réservée (score {slot.Kills}/{slot.Deaths}).");
        return id;
    }

    public static void TickSessionFrame(float dt)
    {
        MettreAJourToasts(dt);

        if (isServer)
        {
            TickReconnexionHote(dt);
            // L'hôte affiche son propre chrono (il est l'autorité)
            if (matchEnCours && matchTimeLimit > 0)
            {
                double reste = matchTimeLimit - (Raylib.GetTime() - matchStartTime);
                matchRemainingAffiche = (int)Math.Max(0, reste);
            }
            else if (matchEnCours) matchRemainingAffiche = -1;

            HoteVerifierFinMatch();  // fin par le temps
            HoteTickPostMatch(dt);   // compte à rebours de l'écran de score
        }
        else
        {
            TickReconnexionClient(dt); // Task 3.4 : reconnexion automatique côté client
        }
    }

    // Client : tente de se rebrancher à l'hôte pendant la fenêtre de grâce, puis abandonne.
    static void TickReconnexionClient(float dt)
    {
        if (!clientEnReconnexion) return;

        if (netManager.ConnectedPeersCount > 0) { clientEnReconnexion = false; return; } // revenu !

        reconnexionRestant -= dt;
        reconnexionRetryTimer -= dt;

        if (reconnexionRestant <= 0f)
        {
            clientEnReconnexion = false;
            CouperReseau();
            derniereErreurReseau = "Reconnexion impossible (hôte injoignable).";
            currentState = GameState.NetworkHub;
            Raylib.EnableCursor();
            isPaused = false;
            Console.WriteLine("[RÉSEAU] Reconnexion abandonnée après 30 s.");
            return;
        }

        if (reconnexionRetryTimer <= 0f && hostEndpoint != null)
        {
            reconnexionRetryTimer = 3f;
            ConnecterAHote(hostEndpoint);
            Console.WriteLine($"[RÉSEAU] Tentative de reconnexion ({(int)reconnexionRestant}s restantes)...");
        }
    }

    // ==========================================
    // ENVOI D'UNE STRUCT À UN PEER PRÉCIS (hot-join)
    // ==========================================
    static void EnvoyerStructA<T>(NetPeer peer, ref T packet, DeliveryMethod method) where T : struct, INetSerializable
    {
        reusableWriter.Reset();
        netProcessor.WriteNetSerializable(reusableWriter, ref packet);
        peer.Send(reusableWriter, method);
    }

    static void EnvoyerClasseA<T>(NetPeer peer, T packet, DeliveryMethod method) where T : class, new()
    {
        reusableWriter.Reset();
        netProcessor.Write(reusableWriter, packet);
        peer.Send(reusableWriter, method);
    }

    // ==========================================
    // CONSTRUCTION DE LA TABLE DES SCORES (autorité hôte)
    //   - nous (id 0) : killCount / deathCount
    //   - chaque joueur distant : Kills / Deaths (tenus à jour par OnPlayerDiedReceived)
    // ==========================================
    public static Packets.ScoreEntry[] ConstruireTableScores()
    {
        List<Packets.ScoreEntry> table = new List<Packets.ScoreEntry>();
        table.Add(new Packets.ScoreEntry
        {
            Id = (byte)Math.Clamp(myPlayerId, 0, 255),
            Nom = NetConfig.NettoyerNom(myLobbyName, "Hôte"),
            Kills = (ushort)Math.Clamp(killCount, 0, ushort.MaxValue),
            Deaths = (ushort)Math.Clamp(deathCount, 0, ushort.MaxValue)
        });
        foreach (RemotePlayer rp in remotePlayers.Values)
        {
            table.Add(new Packets.ScoreEntry
            {
                Id = (byte)Math.Clamp(rp.Id, 0, 255),
                Nom = NetConfig.NettoyerNom(rp.Name, "???"),
                Kills = (ushort)Math.Clamp(rp.Kills, 0, ushort.MaxValue),
                Deaths = (ushort)Math.Clamp(rp.Deaths, 0, ushort.MaxValue)
            });
        }
        return table.ToArray();
    }

    // Le temps restant à mettre dans l'en-tête du WorldSnapshot (0xFFFF = illimité)
    public static ushort SessionRemainingSec()
    {
        if (!matchEnCours || matchTimeLimit <= 0) return 0xFFFF;
        double reste = matchTimeLimit - (Raylib.GetTime() - matchStartTime);
        if (reste < 0) reste = 0;
        return (ushort)Math.Min(reste, 65534);
    }

    // ==========================================
    // LES MUTATEURS DE MATCH SUR LE RÉSEAU (autorité hôte)
    // ==========================================
    // L'hôte est le SEUL à régler : il diffuse la liste complète des valeurs à chaque
    // changement du salon, au lancement du match et au hot-join. Le client se contente
    // de l'appliquer et de l'afficher en lecture seule.
    static Packets.MatchRulesPacket ConstruirePaquetRegles()
    {
        float[] valeurs = new float[MatchRules.Liste.Count];
        for (int i = 0; i < valeurs.Length; i++) valeurs[i] = MatchRules.Liste[i].Valeur;
        return new Packets.MatchRulesPacket { Version = MatchRules.VersionReseau, Valeurs = valeurs };
    }

    /// <summary>HÔTE : envoie les règles courantes à tout le monde.</summary>
    public static void HoteDiffuserRegles()
    {
        if (!isServer || netManager == null || !netManager.IsRunning) return;
        Packets.MatchRulesPacket paquet = ConstruirePaquetRegles();
        DiffuserStructATous(ref paquet, DeliveryMethod.ReliableOrdered);
    }

    /// <summary>HÔTE : envoie les règles à UN peer précis (arrivée / hot-join).</summary>
    public static void HoteEnvoyerReglesA(NetPeer peer)
    {
        if (!isServer || peer == null) return;
        Packets.MatchRulesPacket paquet = ConstruirePaquetRegles();
        EnvoyerStructA(peer, ref paquet, DeliveryMethod.ReliableOrdered);
    }

    /// <summary>CLIENT : applique les règles reçues de l'hôte (jamais l'inverse).</summary>
    public static void OnMatchRulesReceived(Packets.MatchRulesPacket packet, NetPeer peer)
    {
        // S2 : l'hôte fait autorité. Un paquet de règles reçu par l'hôte (donc venant
        // d'un client) est purement et simplement ignoré.
        if (isServer) return;
        if (packet.Valeurs == null) return;

        int n = Math.Min(packet.Valeurs.Length, MatchRules.Liste.Count);
        for (int i = 0; i < n; i++)
        {
            float v = packet.Valeurs[i];
            if (float.IsNaN(v) || float.IsInfinity(v)) continue;   // S3 : valeur forgée -> on garde la nôtre
            MatchRules.Poser(MatchRules.Liste[i], v);              // Poser() borne déjà sur [Min, Max]
        }
        MatchRules.Appliquer();
        Console.WriteLine($"[SESSION] Règles reçues de l'hôte ({n} réglages) : {MatchRules.Resume(4)}");
    }

    // ==========================================
    // HÔTE : LANCEMENT D'UN MATCH DEPUIS LE LOBBY
    // ==========================================
    public static void HoteDemarrerMatch()
    {
        if (!isServer) return;

        // Les règles PARTENT AVANT le top départ : les clients chargent la map avec la
        // bonne gravité, la bonne vie max et les bonnes armes dès la première frame.
        HoteDiffuserRegles();

        Packets.StartGamePacket start = new Packets.StartGamePacket
        {
            MapIndex = mapChoisieIndex,
            ScoreLimit = matchScoreLimitConfig,
            TimeLimitSec = matchTimeLimitConfig
        };

        NetDataWriter writer = new NetDataWriter();
        netProcessor.Write(writer, start);
        netManager.SendToAll(writer, DeliveryMethod.ReliableOrdered);

        OnStartGameReceived(start, null); // l'hôte s'applique la même chose
    }

    // Appelé (hôte + client) quand la partie démarre réellement : fixe les règles + l'horloge.
    // Côté client, l'horloge n'est qu'indicative : l'autorité reste l'hôte via le snapshot.
    public static void DemarrerHorlogeMatch(int scoreLimit, int timeLimit)
    {
        matchScoreLimit = scoreLimit;
        matchTimeLimit = timeLimit;
        matchStartTime = Raylib.GetTime();
        matchEnCours = true;
        matchRemainingAffiche = timeLimit > 0 ? timeLimit : -1;
        // Nouveau match : on repart de scores vierges (les distants aussi)
        foreach (RemotePlayer rp in remotePlayers.Values) { rp.Kills = 0; rp.Deaths = 0; }

        // Les mutateurs prennent effet MAINTENANT (gravité, vie max, sauts, barils...).
        MatchRules.Appliquer();
        chaosTimer = ChaosPeriode;

        Console.WriteLine($"[SESSION] Match démarré (limite {scoreLimit} kills / {timeLimit}s) — {MatchRules.Resume(4)}.");
    }

    // ==========================================
    // LE MODIFICATEUR ALÉATOIRE (règle "chaos")
    // ==========================================
    // Appelé à chaque frame de jeu (solo ET en ligne). En ligne, seul l'HÔTE tire :
    // il rediffuse ensuite les nouvelles règles, le client les reçoit comme n'importe
    // quel autre changement. Personne ne peut donc se retrouver avec des règles à part.
    public static void TickModificateurAleatoire(float dt)
    {
        if (!MatchRules.ChaosActif) return;
        if (currentState != GameState.Playing) return;
        if (isOnline && !isServer) return;

        chaosTimer -= dt;
        if (chaosTimer > 0f) return;
        chaosTimer = ChaosPeriode;

        string texte = MatchRules.TirerModificateurAleatoire(isOnline);
        if (texte == "") return;

        MatchRules.Appliquer();
        if (isServer) { HoteDiffuserRegles(); AjouterToast("CHAOS : " + texte); }
        DeclencherOverlayTuto("MODIFICATEUR ALÉATOIRE", "", texte, 4f);
        Console.WriteLine($"[CHAOS] Nouveau modificateur : {texte}");
    }

    // ==========================================
    // HÔTE : DÉTECTION DE FIN DE MATCH
    // Appelée à chaque frame (timer) et après chaque mort (score).
    // ==========================================
    public static void HoteVerifierFinMatch()
    {
        if (!isServer || !matchEnCours || currentState != GameState.Playing) return;

        bool fini = false;

        // 1. Limite de score : quelqu'un a-t-il atteint le seuil de kills ?
        if (matchScoreLimit > 0)
        {
            if (killCount >= matchScoreLimit) fini = true;
            foreach (RemotePlayer rp in remotePlayers.Values)
                if (rp.Kills >= matchScoreLimit) fini = true;
        }

        // 2. Limite de temps
        if (matchTimeLimit > 0 && (Raylib.GetTime() - matchStartTime) >= matchTimeLimit) fini = true;

        if (fini) HoteTerminerMatch();
    }

    public static void HoteTerminerMatch()
    {
        if (!isServer || currentState != GameState.Playing) return;

        Packets.ScoreEntry[] table = ConstruireTableScores();
        // Le gagnant = le plus de kills (départage : le moins de morts)
        int winnerId = -1; int meilleurKills = -1; int meilleurDeaths = int.MaxValue;
        foreach (var e in table)
        {
            if (e.Kills > meilleurKills || (e.Kills == meilleurKills && e.Deaths < meilleurDeaths))
            {
                meilleurKills = e.Kills; meilleurDeaths = e.Deaths; winnerId = e.Id;
            }
        }

        Packets.MatchEndPacket fin = new Packets.MatchEndPacket { WinnerId = winnerId, Scores = table };
        DiffuserStructATous(ref fin, DeliveryMethod.ReliableOrdered);
        Console.WriteLine($"[SESSION] Match terminé. Gagnant = joueur {winnerId} ({meilleurKills} kills).");

        EntrerPostMatch(winnerId, table);
    }

    // ==========================================
    // ENTRÉE DANS L'ÉCRAN DE FIN (hôte + client)
    // ==========================================
    public static void EntrerPostMatch(int winnerId, Packets.ScoreEntry[] scores)
    {
        matchEnCours = false;
        currentState = GameState.PostMatch;
        postMatchTimer = PostMatchDuree;
        dernierMatchScores = scores.ToList();
        dernierMatchWinnerId = winnerId;
        dernierMatchWinnerNom = scores.FirstOrDefault(s => s.Id == winnerId).Nom ?? "???";
        Raylib.EnableCursor();
        isPaused = false;
    }

    // Client : réception du verdict de l'hôte
    public static void OnMatchEndReceived(Packets.MatchEndPacket packet, NetPeer peer)
    {
        if (isServer) return; // l'hôte l'a déjà appliqué localement
        EntrerPostMatch(packet.WinnerId, packet.Scores ?? Array.Empty<Packets.ScoreEntry>());
    }

    // ==========================================
    // HÔTE : COMPTE À REBOURS DU POSTMATCH -> RETOUR AU SALON
    // ==========================================
    public static void HoteTickPostMatch(float dt)
    {
        if (!isServer || currentState != GameState.PostMatch) return;
        postMatchTimer -= dt;
        if (postMatchTimer <= 0) HoteRetourAuSalon();
    }

    public static void HoteRetourAuSalon()
    {
        if (!isServer) return;
        Packets.SessionSignalPacket sig = new Packets.SessionSignalPacket { Signal = Packets.SessionSignalPacket.RetourAuSalon };
        DiffuserATous(sig, DeliveryMethod.ReliableOrdered);
        RetourAuSalonLocal();
        // On remet tout le monde "pas prêt" pour le prochain match
        lock (_lobbyLock) foreach (var p in currentLobbyPlayers) if (!p.IsHost) p.IsReady = false;
        MettreAJourEtDiffuserLobby();
        Console.WriteLine("[SESSION] Retour au salon (session maintenue).");
    }

    // Nettoyage commun du monde de jeu en revenant au salon (murs, tirs, explosions...)
    // On archive d'abord les scores dans "dernierMatch" pour le panneau du lobby.
    public static void RetourAuSalonLocal()
    {
        matchEnCours = false;
        partieEnCoursCoteHote = false;
        // Le match est fini : plus de reconnexion "en cours de partie" à honorer.
        slotsReserves.Clear();

        // On vide le monde physique/visuel du match
        foreach (MurPose mur in listeMur) simulation.Statics.Remove(mur.handlePhysique);
        listeMur.Clear();
        mursParJoueur.Clear();
        remoteShots.Clear();
        activeExplosions.Clear();
        activeDamageTexts.Clear();
        peersEnBaseline.Clear();

        // Les joueurs distants gardent leur identité mais "disparaissent" jusqu'au prochain match
        foreach (RemotePlayer rp in remotePlayers.Values)
        {
            rp.HasState = false;
            rp.Tampon.Clear();
            rp.HorlogeInitialisee = false;
        }

        currentState = GameState.Lobby;
        Raylib.EnableCursor();
        isPaused = false;
    }

    // ==========================================
    // HOT-JOIN — CÔTÉ HÔTE
    // ==========================================
    // Appelé après le Join quand l'hôte est déjà en partie : on envoie la config
    // du match pour que le retardataire charge la bonne map, puis on attend son "MapChargee".
    public static void HoteDemarrerBaseline(NetPeer peer, int idJoueur)
    {
        peersEnBaseline.Add(idJoueur);

        // Les mutateurs d'abord : le retardataire charge sa map avec les bonnes règles.
        HoteEnvoyerReglesA(peer);

        Packets.MatchInfoPacket info = new Packets.MatchInfoPacket
        {
            MapIndex = mapChoisieIndex,
            ScoreLimit = matchScoreLimit,
            TimeLimitSec = matchTimeLimit,
            ElapsedSec = (int)(Raylib.GetTime() - matchStartTime)
        };
        EnvoyerClasseA(peer, info, DeliveryMethod.ReliableOrdered);
        Console.WriteLine($"[HÔTE] Hot-join du joueur {idJoueur} : MatchInfo envoyé, attente du chargement de sa map.");
    }

    // Le retardataire nous dit "ma map est prête" -> on lui streame la baseline complète.
    public static void HoteEnvoyerBaseline(NetPeer peer, int idJoueur)
    {
        // 1. Les murs déjà posés, par lots de 30 (ReliableOrdered => arrivée dans l'ordre)
        var murs = listeMur.Select(m => new Packets.WallBatchPacket.Mur
        {
            X = m.position.X, Y = m.position.Y, Z = m.position.Z,
            QX = m.rotation.X, QY = m.rotation.Y, QZ = m.rotation.Z, QW = m.rotation.W,
            Couleur = (byte)m.couleurIdx
        }).ToList();
        for (int i = 0; i < murs.Count; i += 30)
        {
            Packets.WallBatchPacket lot = new Packets.WallBatchPacket { Murs = murs.Skip(i).Take(30).ToArray() };
            EnvoyerStructA(peer, ref lot, DeliveryMethod.ReliableOrdered);
        }

        // 2. La table des scores en cours
        Packets.ScoreSnapshotPacket scores = new Packets.ScoreSnapshotPacket { Scores = ConstruireTableScores() };
        EnvoyerStructA(peer, ref scores, DeliveryMethod.ReliableOrdered);

        // 3. Le point de spawn choisi (le plus loin des autres joueurs) + top départ
        Vector3 spawn = ChoisirSpawnHotJoin();
        Packets.BaselineEndPacket fin = new Packets.BaselineEndPacket { SpawnX = spawn.X, SpawnY = spawn.Y, SpawnZ = spawn.Z };
        EnvoyerClasseA(peer, fin, DeliveryMethod.ReliableOrdered);

        peersEnBaseline.Remove(idJoueur); // baseline terminée : il devient un joueur normal (dégâts routés)
        AjouterToast($"{ObtenirNomJoueur(idJoueur)} a rejoint la partie");
        Console.WriteLine($"[HÔTE] Baseline envoyée au joueur {idJoueur} ({murs.Count} murs).");
    }

    // Un point de spawn éloigné des joueurs déjà présents (évite le spawn-kill)
    public static Vector3 ChoisirSpawnHotJoin()
    {
        if (listeSpawns == null || listeSpawns.Count == 0) return new Vector3(0, 10, 0);

        List<Vector3> positionsJoueurs = new List<Vector3>();
        BodyReference moi = simulation.Bodies.GetBodyReference(PlayerId);
        positionsJoueurs.Add(moi.Pose.Position);
        foreach (RemotePlayer rp in remotePlayers.Values) if (rp.HasState) positionsJoueurs.Add(rp.Position);

        Vector3 meilleur = listeSpawns[0];
        float meilleureDistMin = -1f;
        foreach (Vector3 spawn in listeSpawns)
        {
            float distMin = float.MaxValue;
            foreach (Vector3 pj in positionsJoueurs) distMin = MathF.Min(distMin, Vector3.Distance(spawn, pj));
            if (distMin > meilleureDistMin) { meilleureDistMin = distMin; meilleur = spawn; }
        }
        return meilleur;
    }

    static string ObtenirNomJoueur(int id)
    {
        if (id == myPlayerId) return NetConfig.NettoyerNom(myLobbyName, "Hôte");
        if (remotePlayers.TryGetValue(id, out RemotePlayer rp)) return rp.Name;
        var l = currentLobbyPlayers.FirstOrDefault(p => p.Id == id);
        return l != null ? l.Name : "Un joueur";
    }

    // ==========================================
    // HOT-JOIN — CÔTÉ CLIENT
    // ==========================================
    public static void OnMatchInfoReceived(Packets.MatchInfoPacket packet, NetPeer peer)
    {
        if (isServer) return;
        Console.WriteLine($"[CLIENT] Hot-join : chargement de la map {packet.MapIndex} (match en cours).");

        mapChoisieIndex = packet.MapIndex;
        isOnline = true;
        // On charge la map SANS choisir de spawn : l'hôte nous en donnera un via BaselineEnd
        ChargerMapReseau(packet.MapIndex, spawnAleatoire: false);
        DemarrerHorlogeMatch(packet.ScoreLimit, packet.TimeLimitSec);
        matchStartTime = Raylib.GetTime() - packet.ElapsedSec; // on aligne notre horloge sur l'écoulé de l'hôte

        currentState = GameState.Loading;

        // "Ma map est prête, envoie-moi la baseline"
        Packets.SessionSignalPacket sig = new Packets.SessionSignalPacket { Signal = Packets.SessionSignalPacket.MapChargee };
        EnvoyerAuServeur(sig, DeliveryMethod.ReliableOrdered);
    }

    public static void OnWallBatchReceived(Packets.WallBatchPacket packet, NetPeer peer)
    {
        if (isServer || packet.Murs == null) return;
        foreach (var m in packet.Murs)
        {
            Vector3 pos = new Vector3(m.X, m.Y, m.Z);
            Quaternion rot = new Quaternion(m.QX, m.QY, m.QZ, m.QW);
            if (!VecteurValide(pos)) continue;
            float nq = rot.Length();
            if (nq < 0.9f || nq > 1.1f) continue;
            PoserMur(pos, rot, m.Couleur);
        }
    }

    public static void OnScoreSnapshotReceived(Packets.ScoreSnapshotPacket packet, NetPeer peer)
    {
        if (isServer || packet.Scores == null) return;
        foreach (var e in packet.Scores)
        {
            if (e.Id == myPlayerId) { killCount = e.Kills; deathCount = e.Deaths; }
            else
            {
                RemotePlayer rp = ObtenirJoueurDistant(e.Id);
                rp.Name = e.Nom;
                rp.Kills = e.Kills;
                rp.Deaths = e.Deaths;
            }
        }
    }

    public static void OnBaselineEndReceived(Packets.BaselineEndPacket packet, NetPeer peer)
    {
        if (isServer) return;
        Vector3 spawn = new Vector3(packet.SpawnX, packet.SpawnY, packet.SpawnZ);
        if (!VecteurValide(spawn)) spawn = new Vector3(0, 10, 0);

        BodyReference joueur = simulation.Bodies.GetBodyReference(PlayerId);
        joueur.Pose.Position = spawn;
        joueur.Velocity.Linear = Vector3.Zero;
        joueur.Velocity.Angular = Vector3.Zero;
        localPlayer.Respawn();

        partieEnCoursCoteHote = false;
        currentState = GameState.Playing;
        Raylib.DisableCursor();
        Console.WriteLine("[CLIENT] Baseline reçue : entrée en jeu (hot-join terminé).");
    }

    // ==========================================
    // "RETOUR AU SALON" / "REJOINDRE" DEPUIS LA PARTIE (signaux client<->hôte)
    // ==========================================
    // Le client, depuis le menu pause, choisit de retourner au salon sans se déconnecter.
    public static void ClientQuitterVersSalon()
    {
        if (isServer) return;
        Packets.SessionSignalPacket sig = new Packets.SessionSignalPacket { Signal = Packets.SessionSignalPacket.QuitterMatch };
        EnvoyerAuServeur(sig, DeliveryMethod.ReliableOrdered);
        partieEnCoursCoteHote = true; // le match continue chez l'hôte -> on pourra le rejoindre
        currentState = GameState.Lobby;
        Raylib.EnableCursor();
        isPaused = false;
    }

    // Le client, depuis le lobby pendant qu'un match tourne, demande à (re)entrer.
    public static void ClientDemanderRejoindre()
    {
        if (isServer) return;
        Packets.SessionSignalPacket sig = new Packets.SessionSignalPacket { Signal = Packets.SessionSignalPacket.RejoindreMatch };
        EnvoyerAuServeur(sig, DeliveryMethod.ReliableOrdered);
    }

    public static void OnSessionSignalReceived(Packets.SessionSignalPacket packet, NetPeer peer)
    {
        switch (packet.Signal)
        {
            case Packets.SessionSignalPacket.MapChargee:
                // Client prêt -> l'hôte lui envoie la baseline
                if (isServer && peer != null && IdExpediteur(peer, out int idPret))
                    HoteEnvoyerBaseline(peer, idPret);
                break;

            case Packets.SessionSignalPacket.RejoindreMatch:
                // Un joueur du lobby veut entrer dans le match en cours
                if (isServer && peer != null && IdExpediteur(peer, out int idRejoint))
                {
                    if (currentState == GameState.Playing) HoteDemarrerBaseline(peer, idRejoint);
                }
                break;

            case Packets.SessionSignalPacket.QuitterMatch:
                // Un joueur retourne au salon : il disparaît du monde mais reste dans le roster
                if (isServer && peer != null && IdExpediteur(peer, out int idParti))
                {
                    if (remotePlayers.TryGetValue(idParti, out RemotePlayer rp)) { rp.HasState = false; rp.Tampon.Clear(); }
                    peersEnBaseline.Remove(idParti);
                    AjouterToast($"{ObtenirNomJoueur(idParti)} est retourné au salon");
                }
                break;

            case Packets.SessionSignalPacket.RetourAuSalon:
                // L'hôte ordonne le retour au salon (fin du PostMatch)
                if (!isServer) RetourAuSalonLocal();
                break;
        }
    }

    // ==========================================
    // AFFICHAGE : timer de match + toasts + écran PostMatch
    // ==========================================
    public static void DessinerHudSession()
    {
        // Task 3.4 : bandeau de reconnexion (le monde est figé, on essaie de revenir)
        if (clientEnReconnexion)
        {
            Raylib.DrawRectangle(0, 0, LargeurFenetre, HauteurFenetre, new Color(0, 0, 0, 160));
            string t = $"Connexion perdue — reconnexion... ({(int)MathF.Ceiling(reconnexionRestant)}s)";
            int w = Raylib.MeasureText(t, 34);
            Raylib.DrawText(t, LargeurFenetre / 2 - w / 2, HauteurFenetre / 2 - 20, 34, Color.Gold);
            string t2 = "Ta place et ton score sont gardés côté hôte.";
            int w2 = Raylib.MeasureText(t2, 20);
            Raylib.DrawText(t2, LargeurFenetre / 2 - w2 / 2, HauteurFenetre / 2 + 25, 20, Color.LightGray);
        }

        // Le chrono du match, centré en haut
        if (matchEnCours && matchRemainingAffiche >= 0)
        {
            int m = matchRemainingAffiche / 60;
            int s = matchRemainingAffiche % 60;
            string chrono = $"{m}:{s:D2}";
            int w = Raylib.MeasureText(chrono, 40);
            Color c = matchRemainingAffiche <= 30 ? Color.Red : Color.White;
            Raylib.DrawText(chrono, LargeurFenetre / 2 - w / 2 + 2, 22, 40, Color.Black);
            Raylib.DrawText(chrono, LargeurFenetre / 2 - w / 2, 20, 40, c);
        }
        if (matchScoreLimit > 0 && matchEnCours)
        {
            string obj = $"Objectif : {matchScoreLimit} kills";
            Raylib.DrawText(obj, LargeurFenetre / 2 - Raylib.MeasureText(obj, 18) / 2, 62, 18, Color.White);
        }

        // Les toasts (arrivées / départs), empilés en haut à droite
        int ty = 90;
        foreach (Toast t in toasts)
        {
            int alpha = (int)(Math.Clamp(t.Timer, 0f, 1f) * 255);
            int w = Raylib.MeasureText(t.Texte, 20);
            Raylib.DrawText(t.Texte, LargeurFenetre - w - 20, ty, 20, new Color(255, 255, 255, alpha));
            ty += 26;
        }
    }

    // L'écran de fin de match : panneau structuré, gagnant mis en avant, médailles,
    // barre de compte à rebours qui coule vers le retour au salon.
    public static void DessinerPostMatch()
    {
        Raylib.DrawRectangle(0, 0, LargeurFenetre, HauteurFenetre, new Color(0, 0, 0, 205));

        int cx = LargeurFenetre / 2;

        // --- Bandeau gagnant ---
        bool jaiGagne = dernierMatchWinnerId == myPlayerId;
        string sousTitre = jaiGagne ? "VICTOIRE !" : "FIN DE LA PARTIE";
        Raylib.DrawText(sousTitre, cx - Raylib.MeasureText(sousTitre, 34) / 2, 55, 34, jaiGagne ? Color.Gold : Color.LightGray);

        string banniere = $"{dernierMatchWinnerNom} remporte la partie";
        Raylib.DrawText(banniere, cx - Raylib.MeasureText(banniere, 44) / 2 + 2, 100, 44, Color.Black);
        Raylib.DrawText(banniere, cx - Raylib.MeasureText(banniere, 44) / 2, 98, 44, Color.Gold);

        // --- Panneau du tableau final ---
        int panW = 640, panX = cx - panW / 2, panY = 165;
        var tries = dernierMatchScores.OrderByDescending(s => s.Kills).ThenBy(s => s.Deaths).ToList();
        int panH = 70 + Math.Min(tries.Count, 10) * 44 + 20;
        Raylib.DrawRectangle(panX, panY, panW, panH, new Color(25, 25, 30, 235));
        Raylib.DrawRectangleLinesEx(new Rectangle(panX, panY, panW, panH), 2, new Color(120, 120, 120, 255));

        // En-têtes
        int hy = panY + 22;
        Raylib.DrawText("#", panX + 25, hy, 22, Color.Gray);
        Raylib.DrawText("JOUEUR", panX + 75, hy, 22, Color.Gray);
        Raylib.DrawText("KILLS", panX + 420, hy, 22, Color.Gray);
        Raylib.DrawText("MORTS", panX + 530, hy, 22, Color.Gray);
        Raylib.DrawLine(panX + 15, panY + 52, panX + panW - 15, panY + 52, new Color(90, 90, 90, 255));

        int y = panY + 66;
        int rang = 1;
        foreach (var e in tries)
        {
            bool moi = e.Id == myPlayerId;
            // surlignage de notre propre ligne
            if (moi) Raylib.DrawRectangle(panX + 8, y - 4, panW - 16, 40, new Color(60, 60, 30, 180));

            // médaille pour le podium
            Color medaille = rang switch { 1 => Color.Gold, 2 => new Color(200, 200, 200, 255), 3 => new Color(190, 130, 70, 255), _ => Color.Gray };
            Raylib.DrawText(rang.ToString(), panX + 25, y, 26, medaille);

            Color c = e.Id == dernierMatchWinnerId ? Color.Gold : (moi ? Color.White : Color.LightGray);
            Raylib.DrawText(e.Nom, panX + 75, y, 26, c);
            Raylib.DrawText(e.Kills.ToString(), panX + 425, y, 26, c);
            Raylib.DrawText(e.Deaths.ToString(), panX + 535, y, 26, c);
            y += 44;
            rang++;
            if (rang > 10) break;
        }

        // --- Barre de compte à rebours vers le salon ---
        float frac = Math.Clamp(postMatchTimer / PostMatchDuree, 0f, 1f);
        int barW = 400, barX = cx - barW / 2, barY = HauteurFenetre - 80;
        Raylib.DrawRectangle(barX, barY, barW, 10, new Color(60, 60, 60, 255));
        Raylib.DrawRectangle(barX, barY, (int)(barW * frac), 10, Color.Gold);
        string bas = $"Retour au salon dans {MathF.Ceiling(postMatchTimer)}s";
        Raylib.DrawText(bas, cx - Raylib.MeasureText(bas, 22) / 2, barY + 20, 22, Color.LightGray);
    }
}
