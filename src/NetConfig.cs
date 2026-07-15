using System;

// ========================================================
// LA CONFIGURATION RÉSEAU CENTRALE (Phase 0 de la roadmap WAN)
// Toutes les constantes réseau vivent ICI et nulle part ailleurs.
// Voir docs/NETWORK_ROADMAP.md pour le pourquoi de chaque valeur.
// ========================================================
public static class NetConfig
{
    // --- Identité du protocole ---
    // À INCRÉMENTER à chaque changement de format de paquet !
    // La clé de connexion en dépend : deux versions différentes ne peuvent
    // pas se connecter entre elles (rejet propre avec message).
    public const int ProtocolVersion = 2;
    public static readonly string ConnectionKey = $"GoofyFPS/{ProtocolVersion}";

    // --- Ports ---
    public const int GamePort = 7777;

    // --- Serveur maître (Phase 2 : annuaire + rendez-vous NAT) ---
    // À REMPLACER par le vrai sous-domaine OVH une fois déployé.
    public static string MasterUrl = Environment.GetEnvironmentVariable("GOOFY_MASTER_URL") ?? "https://gfps-api.example.com";
    // Endpoint du rendez-vous NAT (host:port UDP). Surchargé pour les tests locaux.
    public static string PunchHost = Environment.GetEnvironmentVariable("GOOFY_PUNCH_HOST") ?? "gfps-api.example.com";
    public const int PunchPort = 7790;
    public const float HeartbeatIntervalSec = 30f;   // battement de cœur REST
    public const float PunchKeepaliveSec = 25f;      // ré-introduction NAT (garde le mapping ouvert)

    // --- Capacité du salon (D2 : 12 par défaut, extensible jusqu'à 20) ---
    public static int MaxPlayers = 12;
    public const int MaxPlayersAbsolu = 20;

    // --- Le tick unifié 64 Hz (D5) : physique + réseau sur la même horloge ---
    public const int TickRate = 64;
    public const float TickDt = 1f / TickRate;
    // Retard d'interpolation des joueurs distants, en ticks (3 ≈ 47 ms)
    public const int InterpTicks = 3;
    // Extrapolation maximale quand le buffer est à sec (perte de paquets)
    public const float ExtrapolationMaxSec = 0.15f;
    // Au-delà de cette distance entre deux états, c'est une téléportation
    // (respawn) : on saute directement au lieu de glisser à travers la map.
    public const float SeuilTeleportation = 5f;

    // --- Quantification (voir §4.2 de la roadmap) ---
    // Position : int16 au centimètre près -> ±327 m de portée map
    public const float PrecisionPosition = 100f;   // 1 unité = 1 cm
    public const float PitchMax = MathF.PI / 2f;    // pitch encodé sur ±90°
    public const float LeanMax = 0.6f;              // penchement wall-run (radians)

    // --- Plafonds de ressources (S4 : borner la mémoire face à un client fou) ---
    public const int MaxMursParJoueur = 30;
    public const int MaxMursTotal = 200;
    public const int MaxTirsVisuels = 64;
    public const int MaxNomLongueur = 24;
    // Taille max du buffer de snapshots par joueur distant (~1.5 s à 64 Hz)
    public const int MaxSnapshotsBuffer = 96;

    // --- Raisons de rejet / déconnexion (S7 : le client affiche un vrai message) ---
    public const byte RejetVersion = 1;   // Version du jeu différente
    public const byte RejetPlein = 2;     // Salon plein
    public const byte RejetEnPartie = 3;  // Partie déjà lancée (levé en Phase 1 par le hot-join)
    public const byte RejetKick = 4;      // Exclu par l'hôte
    public const byte RejetTricheur = 5;  // Comportement réseau incohérent (Phase 3)
    public const byte RejetMotDePasse = 6;// Mauvais mot de passe de salon (Task 2.9)

    public static string MessageRejet(byte code) => code switch
    {
        RejetVersion => "Version du jeu différente de celle de l'hôte !",
        RejetPlein => "Le salon est plein !",
        RejetEnPartie => "La partie est déjà lancée !",
        RejetKick => "Tu as été exclu par l'hôte.",
        RejetTricheur => "Déconnecté : comportement réseau incohérent.",
        RejetMotDePasse => "Mot de passe incorrect.",
        _ => "Connexion refusée par l'hôte."
    };

    // --- Contrôles de cohérence Phase 3 (PAS de l'anti-cheat : juste borner un client modifié/bugué) ---
    public const float ToleranceCadenceTir = 0.8f;    // on accepte un tir à 80 % de la cadence (gigue réseau)
    // Vitesse horizontale "impossible" (le dash/wall-run montent haut : on met un plafond très large).
    // Au-delà = suspect ; il faut BEAUCOUP de violations d'affilée avant d'exclure (zéro faux positif).
    public const float VitesseHorizSuspecte = 80f;    // unités/seconde
    public const int ViolationsAvantKick = 40;        // ~0.6 s de téléportations continues à 64 Hz

    // Nettoyage commun des pseudos et noms de salon (S3) :
    // longueur bornée + caractères de contrôle et séparateurs protocole interdits.
    public static string NettoyerNom(string brut, string secours)
    {
        if (string.IsNullOrWhiteSpace(brut)) return secours;
        var propre = new System.Text.StringBuilder(MaxNomLongueur);
        foreach (char c in brut)
        {
            if (propre.Length >= MaxNomLongueur) break;
            if (char.IsControl(c) || c == ',' || c == ';') continue;
            propre.Append(c);
        }
        return propre.Length == 0 ? secours : propre.ToString();
    }
}
