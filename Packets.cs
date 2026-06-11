using System;

// Ce fichier contient uniquement les "Colis" de données qui vont voyager sur le réseau.
// IMPORTANT : NetPacketProcessor (LiteNetLib) ne sérialise QUE les propriétés publiques { get; set; },
// jamais les champs. Tous les paquets doivent donc utiliser des propriétés !
public class Packets
{
    // 1. LE PASSEPORT D'ENTRÉE (Envoyé quand on rejoint le lobby)
    public class JoinPacket
    {
        public string PlayerName { get; set; } = "";
    }

    // 1bis. LA CARTE D'IDENTITÉ (Le serveur répond au client avec son numéro unique)
    public class YourIdPacket
    {
        public int PlayerId { get; set; }
    }

    // 2. LA POSITION DU JOUEUR (Envoyé 60 fois par seconde !)
    public class PlayerStatePacket
    {
        public int PlayerId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Pitch { get; set; }
        public float Yaw { get; set; }
        public int Health { get; set; }
    }

    // 3. LES ACTIONS PONCTUELLES (Tirer)
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
        public string WeaponName { get; set; } = "";
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

    // 4. SYNCHRONISATION DU LOBBY (Envoyé par le Serveur à tout le monde à chaque changement)
    public class LobbyStatePacket
    {
        public string MatchName { get; set; } = "";
        public int MapIndex { get; set; }
        // Les joueurs fusionnés en une seule chaîne : "Nom,Pret,Id;Nom,Pret,Id;..."
        public string SerializedPlayers { get; set; } = "";
    }

    // 5. SIGNAL "JE SUIS PRÊT" (Envoyé par un Client au Serveur)
    public class ToggleReadyPacket
    {
        public bool IsReady { get; set; }
    }

    // 6. SIGNAL DE LANCEMENT DE PARTIE (Envoyé par le Serveur à tous les Clients)
    public class StartGamePacket
    {
        public int MapIndex { get; set; }
    }
}
