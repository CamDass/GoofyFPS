using System;

// Ce fichier contient uniquement les "Colis" de données qui vont voyager sur le réseau
public class Packets
{
    // 1. LE PASSEPORT D'ENTRÉE (Envoyé quand on rejoint le lobby)
    public class JoinPacket // <-- CORRECTION ICI
    {
        public string PlayerName;
    }

    // 2. LA POSITION DU JOUEUR (Envoyé 60 fois par seconde !)
    public class PlayerStatePacket // <-- CORRECTION ICI
    {
        public int PlayerId; 
        public float X, Y, Z;
        public float Pitch, Yaw;
    }

    // 3. LES ACTIONS PONCTUELLES (Tirer)
    public class ShootPacket // <-- CORRECTION ICI
    {
        public int PlayerId;
        public float OriginX, OriginY, OriginZ;
        public float DirX, DirY, DirZ;
        public string WeaponName; 
    }
}