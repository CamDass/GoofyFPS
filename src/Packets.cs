using System;
using System.Numerics;
using LiteNetLib.Utils;

// ========================================================
// Ce fichier contient uniquement les "Colis" de données qui voyagent sur le réseau.
//
// DEUX FAMILLES DE PAQUETS (Phase 0 de la roadmap) :
//
//  1. Les ÉVÉNEMENTS (classes) : rares, envoyés en fiable (ReliableOrdered).
//     NetPacketProcessor les sérialise tout seul par réflexion.
//     IMPORTANT : il ne sérialise QUE les propriétés publiques { get; set; } !
//
//  2. Le FLUX D'ÉTAT 64 Hz (structs INetSerializable) : le chemin chaud.
//     Sérialisation MANUELLE et QUANTIFIÉE (§4.2 de la roadmap) pour tenir
//     le budget d'upload résidentiel : ~14 octets par joueur par tick.
// ========================================================

// ==========================================
// LES OUTILS DE QUANTIFICATION (float -> entiers courts)
// ==========================================
public static class NetQuant
{
    // Position : float -> int16 au centimètre (±327 m)
    public static short QPos(float v) =>
        (short)Math.Clamp(MathF.Round(v * NetConfig.PrecisionPosition), short.MinValue, short.MaxValue);
    public static float DePos(short q) => q / NetConfig.PrecisionPosition;

    // Yaw : n'importe quel angle -> [0, 2π) -> ushort (précision 0.0055°)
    public static ushort QYaw(float yaw)
    {
        float n = yaw % (2f * MathF.PI);
        if (n < 0f) n += 2f * MathF.PI;
        return (ushort)MathF.Min(MathF.Round(n / (2f * MathF.PI) * 65535f), 65535f);
    }
    public static float DeYaw(ushort q) => q / 65535f * 2f * MathF.PI;

    // Angle borné (pitch, lean) -> sbyte
    public static sbyte QAngle(float a, float max) =>
        (sbyte)Math.Clamp(MathF.Round(a / max * 127f), -127f, 127f);
    public static float DeAngle(sbyte q, float max) => q / 127f * max;
}

public class Packets
{
    // ========================================================
    // FAMILLE 2 : LE FLUX D'ÉTAT 64 Hz (structs quantifiées)
    // ========================================================

    // L'état d'UN joueur à UN tick donné : 14 octets sur le câble.
    // C'est la brique commune du ClientStatePacket et du WorldSnapshotPacket.
    public struct PlayerSnap
    {
        public byte PlayerId;
        public short QX, QY, QZ;   // position quantifiée (cm)
        public ushort QYaw;        // regard horizontal
        public sbyte QPitch;       // regard vertical (±90°)
        public sbyte QLean;        // penchement wall-run
        public byte Health;
        public byte WeaponIndex;
        public byte Flags;         // bit0 = vise, bit1 = recharge

        public const byte FlagVise = 1;
        public const byte FlagRecharge = 2;

        // --- Accès dé-quantifié (confort côté gameplay) ---
        public Vector3 Position => new Vector3(NetQuant.DePos(QX), NetQuant.DePos(QY), NetQuant.DePos(QZ));
        public float Yaw => NetQuant.DeYaw(QYaw);
        public float Pitch => NetQuant.DeAngle(QPitch, NetConfig.PitchMax);
        public float Lean => NetQuant.DeAngle(QLean, NetConfig.LeanMax);
        public bool Vise => (Flags & FlagVise) != 0;
        public bool Recharge => (Flags & FlagRecharge) != 0;

        // Fabrique une snap à partir des valeurs "monde" du jeu
        public static PlayerSnap Depuis(int playerId, Vector3 pos, float yaw, float pitch, float lean,
                                        int health, int weaponIndex, bool vise, bool recharge)
        {
            return new PlayerSnap
            {
                PlayerId = (byte)Math.Clamp(playerId, 0, 255),
                QX = NetQuant.QPos(pos.X),
                QY = NetQuant.QPos(pos.Y),
                QZ = NetQuant.QPos(pos.Z),
                QYaw = NetQuant.QYaw(yaw),
                QPitch = NetQuant.QAngle(pitch, NetConfig.PitchMax),
                QLean = NetQuant.QAngle(lean, NetConfig.LeanMax),
                Health = (byte)Math.Clamp(health, 0, 255),
                WeaponIndex = (byte)Math.Clamp(weaponIndex, 0, 255),
                Flags = (byte)((vise ? FlagVise : 0) | (recharge ? FlagRecharge : 0))
            };
        }

        public void Ecrire(NetDataWriter w)
        {
            w.Put(PlayerId);
            w.Put(QX); w.Put(QY); w.Put(QZ);
            w.Put(QYaw);
            w.Put(QPitch);
            w.Put(QLean);
            w.Put(Health);
            w.Put(WeaponIndex);
            w.Put(Flags);
        }

        public static PlayerSnap Lire(NetDataReader r)
        {
            PlayerSnap s;
            s.PlayerId = r.GetByte();
            s.QX = r.GetShort(); s.QY = r.GetShort(); s.QZ = r.GetShort();
            s.QYaw = r.GetUShort();
            s.QPitch = r.GetSByte();
            s.QLean = r.GetSByte();
            s.Health = r.GetByte();
            s.WeaponIndex = r.GetByte();
            s.Flags = r.GetByte();
            return s;
        }
    }

    // CLIENT -> HÔTE, à chaque tick (Sequenced : si un paquet se perd, le suivant corrige).
    // L'hôte IGNORE le PlayerId contenu dedans : l'identité vient du peer (S2 !).
    public struct ClientStatePacket : INetSerializable
    {
        public ushort Tick;        // le tick local du client (monotone)
        public PlayerSnap Etat;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Tick);
            Etat.Ecrire(writer);
        }

        public void Deserialize(NetDataReader reader)
        {
            Tick = reader.GetUShort();
            Etat = PlayerSnap.Lire(reader);
        }
    }

    // HÔTE -> TOUS, une seule fois par tick : TOUS les joueurs dans UN paquet.
    // (Remplace le relais paquet-par-paquet : règle le O(n²) et le bug du canal Sequenced partagé.)
    public struct WorldSnapshotPacket : INetSerializable
    {
        public ushort ServerTick;
        public ushort RemainingSec;   // temps restant du match (0xFFFF = pas de limite)
        public PlayerSnap[] Joueurs;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(ServerTick);
            writer.Put(RemainingSec);
            int n = Joueurs?.Length ?? 0;
            writer.Put((byte)Math.Min(n, NetConfig.MaxPlayersAbsolu));
            for (int i = 0; i < n && i < NetConfig.MaxPlayersAbsolu; i++)
                Joueurs[i].Ecrire(writer);
        }

        public void Deserialize(NetDataReader reader)
        {
            ServerTick = reader.GetUShort();
            RemainingSec = reader.GetUShort();
            int n = reader.GetByte();
            // S3 : on borne AVANT d'allouer (un paquet forgé ne fera pas exploser la mémoire)
            if (n > NetConfig.MaxPlayersAbsolu) n = NetConfig.MaxPlayersAbsolu;
            Joueurs = new PlayerSnap[n];
            for (int i = 0; i < n; i++) Joueurs[i] = PlayerSnap.Lire(reader);
        }
    }

    // ========================================================
    // FAMILLE 3 : LE CYCLE DE VIE DE LA SESSION (Phase 1)
    // Machine à états host-authoritative : Lobby -> Loading -> Playing -> PostMatch -> Lobby
    // ========================================================

    // Une ligne de score (partagée par ScoreSnapshot et MatchEnd)
    public struct ScoreEntry
    {
        public byte Id;
        public string Nom;
        public ushort Kills;
        public ushort Deaths;
    }

    // Signaux de contrôle sans données (un seul type pour éviter d'en multiplier)
    public class SessionSignalPacket
    {
        public const byte MapChargee = 1;      // Client -> Hôte : "ma map est prête, envoie la baseline"
        public const byte RejoindreMatch = 2;  // Client -> Hôte : "laisse-moi entrer dans le match en cours"
        public const byte QuitterMatch = 3;    // Client -> Hôte : "je retourne au salon (mais je reste connecté)"
        public const byte RetourAuSalon = 4;   // Hôte -> Tous : "fin de l'écran de score, retour au salon"

        public byte Signal { get; set; }
    }

    // HÔTE -> CLIENT (hot-join) : la config du match en cours, pour que le retardataire
    // charge la bonne map avec les bonnes règles avant de recevoir la baseline.
    public class MatchInfoPacket
    {
        public int MapIndex { get; set; }
        public int ScoreLimit { get; set; }
        public int TimeLimitSec { get; set; }
        public int ElapsedSec { get; set; }
    }

    // HÔTE -> CLIENT (hot-join) : les murs déjà posés, par lots (S4 : borné à 30/paquet)
    public struct WallBatchPacket : INetSerializable
    {
        public struct Mur
        {
            public float X, Y, Z;
            public float QX, QY, QZ, QW;
            public byte Couleur;   // couleur de skin du poseur (index dans couleursSkin)
        }
        public Mur[] Murs;

        public void Serialize(NetDataWriter writer)
        {
            int n = Murs?.Length ?? 0;
            writer.Put((byte)Math.Min(n, 30));
            for (int i = 0; i < n && i < 30; i++)
            {
                writer.Put(Murs[i].X); writer.Put(Murs[i].Y); writer.Put(Murs[i].Z);
                writer.Put(Murs[i].QX); writer.Put(Murs[i].QY); writer.Put(Murs[i].QZ); writer.Put(Murs[i].QW);
                writer.Put(Murs[i].Couleur);
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            int n = reader.GetByte();
            if (n > 30) n = 30;
            Murs = new Mur[n];
            for (int i = 0; i < n; i++)
            {
                Murs[i].X = reader.GetFloat(); Murs[i].Y = reader.GetFloat(); Murs[i].Z = reader.GetFloat();
                Murs[i].QX = reader.GetFloat(); Murs[i].QY = reader.GetFloat(); Murs[i].QZ = reader.GetFloat(); Murs[i].QW = reader.GetFloat();
                Murs[i].Couleur = reader.GetByte();
            }
        }
    }

    // HÔTE -> CLIENT : la table des scores actuelle (hot-join baseline + fin de match)
    public struct ScoreSnapshotPacket : INetSerializable
    {
        public ScoreEntry[] Scores;

        public void Serialize(NetDataWriter writer)
        {
            int n = Scores?.Length ?? 0;
            writer.Put((byte)Math.Min(n, NetConfig.MaxPlayersAbsolu));
            for (int i = 0; i < n && i < NetConfig.MaxPlayersAbsolu; i++)
            {
                writer.Put(Scores[i].Id);
                writer.Put(NetConfig.NettoyerNom(Scores[i].Nom, "???"));
                writer.Put(Scores[i].Kills);
                writer.Put(Scores[i].Deaths);
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            int n = reader.GetByte();
            if (n > NetConfig.MaxPlayersAbsolu) n = NetConfig.MaxPlayersAbsolu;
            Scores = new ScoreEntry[n];
            for (int i = 0; i < n; i++)
            {
                Scores[i].Id = reader.GetByte();
                Scores[i].Nom = NetConfig.NettoyerNom(reader.GetString(64), "???");
                Scores[i].Kills = reader.GetUShort();
                Scores[i].Deaths = reader.GetUShort();
            }
        }
    }

    // HÔTE -> CLIENT (hot-join) : dernier colis de la baseline -> le client peut spawn et jouer
    public class BaselineEndPacket
    {
        public float SpawnX { get; set; }
        public float SpawnY { get; set; }
        public float SpawnZ { get; set; }
    }

    // HÔTE -> TOUS : le match est terminé, voici le gagnant + le tableau final
    public struct MatchEndPacket : INetSerializable
    {
        public int WinnerId;
        public ScoreEntry[] Scores;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(WinnerId);
            int n = Scores?.Length ?? 0;
            writer.Put((byte)Math.Min(n, NetConfig.MaxPlayersAbsolu));
            for (int i = 0; i < n && i < NetConfig.MaxPlayersAbsolu; i++)
            {
                writer.Put(Scores[i].Id);
                writer.Put(NetConfig.NettoyerNom(Scores[i].Nom, "???"));
                writer.Put(Scores[i].Kills);
                writer.Put(Scores[i].Deaths);
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            WinnerId = reader.GetInt();
            int n = reader.GetByte();
            if (n > NetConfig.MaxPlayersAbsolu) n = NetConfig.MaxPlayersAbsolu;
            Scores = new ScoreEntry[n];
            for (int i = 0; i < n; i++)
            {
                Scores[i].Id = reader.GetByte();
                Scores[i].Nom = NetConfig.NettoyerNom(reader.GetString(64), "???");
                Scores[i].Kills = reader.GetUShort();
                Scores[i].Deaths = reader.GetUShort();
            }
        }
    }

    // ========================================================
    // FAMILLE 1 : LES ÉVÉNEMENTS (fiables, rares)
    // ========================================================

    // 1. LE PASSEPORT D'ENTRÉE (Envoyé quand on rejoint le lobby)
    // Porte aussi les infos de RECONNEXION : si le client revient après une coupure
    // réseau pendant un match, il présente son ancien Id + son jeton pour récupérer
    // sa place et son score (grâce de 30 s côté hôte — Task 3.4).
    public class JoinPacket
    {
        public string PlayerName { get; set; } = "";
        public int ReconnectId { get; set; } = -1;   // -1 = nouvelle connexion (pas une reconnexion)
        public int ReconnectToken { get; set; } = 0; // jeton secret reçu au 1er join
    }

    // 1bis. LA CARTE D'IDENTITÉ (Le serveur répond au client avec son numéro unique)
    // + un jeton de reconnexion à garder pour pouvoir reprendre sa place après une coupure.
    public class YourIdPacket
    {
        public int PlayerId { get; set; }
        public int ReconnectToken { get; set; }
    }

    // 1ter. LA FICHE D'IDENTITÉ D'UN JOUEUR (pseudo + skin), envoyée UNE fois
    // au lieu de voyager dans chaque paquet d'état 64 fois par seconde !
    //  - Client -> Hôte : "voilà mon skin" (l'hôte ignore PlayerId, S2)
    //  - Hôte -> Tous : "voilà qui est le joueur N"
    public class PlayerInfoPacket
    {
        public int PlayerId { get; set; }
        public string Name { get; set; } = "";
        public int SkinColor { get; set; }
        public int SkinHat { get; set; } = -1;
        public int SkinFace { get; set; } = -1;
    }

    // 2bis. UN MUR CONSTRUIT (Touche F) — diffusé pour que TOUT le monde
    // ait le mur en visuel ET en physique (sinon on passe à travers !)
    public class WallPlacedPacket
    {
        public int PlayerId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float QX { get; set; }
        public float QY { get; set; }
        public float QZ { get; set; }
        public float QW { get; set; }
        public byte SkinColor { get; set; }  // couleur de skin du poseur (index dans couleursSkin)
    }

    // 3. LES ACTIONS PONCTUELLES (Tirer)
    // Envoyé IMMÉDIATEMENT au clic (jamais mis en attente du prochain tick !)
    public class ShootPacket
    {
        public int PlayerId { get; set; }
        public float OriginX { get; set; }
        public float OriginY { get; set; }
        public float OriginZ { get; set; }
        public float DirX { get; set; }
        public float DirY { get; set; }
        public float DirZ { get; set; }
        public float Distance { get; set; }
        public byte WeaponIndex { get; set; }  // index dans weapons (plus de string sur le câble)
    }

    // 3bis. UN JOUEUR EN A TOUCHÉ UN AUTRE (Tireur -> Serveur -> Victime)
    public class PlayerHitPacket
    {
        public int TargetId { get; set; }
        public int ShooterId { get; set; }
        public int Damage { get; set; }
    }

    // 3ter. UN JOUEUR EST MORT (Victime -> Serveur -> Tout le monde, pour le score)
    public class PlayerDiedPacket
    {
        public int VictimId { get; set; }
        public int KillerId { get; set; }
    }

    // 3quater. UN JOUEUR A QUITTÉ LA PARTIE
    public class PlayerLeftPacket
    {
        public int PlayerId { get; set; }
    }

    // 4. SYNCHRONISATION DU LOBBY (Serveur -> tout le monde à chaque changement)
    // Version STRUCTURÉE (fini le bricolage "Nom,Pret,Id;Nom,Pret,Id" !)
    public struct LobbyStatePacket : INetSerializable
    {
        public struct Entree
        {
            public byte Id;
            public string Nom;
            public bool Pret;
            public bool EstHote;
        }

        public string MatchName;
        public byte MapIndex;
        public Entree[] Joueurs;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(NetConfig.NettoyerNom(MatchName, "Salon"));
            writer.Put(MapIndex);
            int n = Joueurs?.Length ?? 0;
            writer.Put((byte)Math.Min(n, NetConfig.MaxPlayersAbsolu));
            for (int i = 0; i < n && i < NetConfig.MaxPlayersAbsolu; i++)
            {
                writer.Put(Joueurs[i].Id);
                writer.Put(NetConfig.NettoyerNom(Joueurs[i].Nom, "???"));
                byte flags = (byte)((Joueurs[i].Pret ? 1 : 0) | (Joueurs[i].EstHote ? 2 : 0));
                writer.Put(flags);
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            // S3 : chaque chaîne est bornée à la lecture, chaque compteur est plafonné
            MatchName = NetConfig.NettoyerNom(reader.GetString(64), "Salon");
            MapIndex = reader.GetByte();
            int n = reader.GetByte();
            if (n > NetConfig.MaxPlayersAbsolu) n = NetConfig.MaxPlayersAbsolu;
            Joueurs = new Entree[n];
            for (int i = 0; i < n; i++)
            {
                Joueurs[i].Id = reader.GetByte();
                Joueurs[i].Nom = NetConfig.NettoyerNom(reader.GetString(64), "???");
                byte flags = reader.GetByte();
                Joueurs[i].Pret = (flags & 1) != 0;
                Joueurs[i].EstHote = (flags & 2) != 0;
            }
        }
    }

    // 5. SIGNAL "JE SUIS PRÊT" (Envoyé par un Client au Serveur)
    public class ToggleReadyPacket
    {
        public bool IsReady { get; set; }
    }

    // 6. SIGNAL DE LANCEMENT DE PARTIE (Envoyé par le Serveur à tous les Clients)
    // Porte désormais les règles du match (limite de kills / de temps).
    public class StartGamePacket
    {
        public int MapIndex { get; set; }
        public int ScoreLimit { get; set; } = 20;
        public int TimeLimitSec { get; set; } = 600; // 0 = pas de limite de temps
    }
}
