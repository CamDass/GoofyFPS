global using System.Net;
global using LiteNetLib;

using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;
using System.Linq;

// Moteur Physique
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;

//reseau (LiteNetLib est déjà en global using en haut du fichier)
using LiteNetLib.Utils;


public partial class Program
{
    // ========================================================
    // VARIABLES DU JEU ET DES ARMES
    // ========================================================
 
    public static Player localPlayer = new Player(100);
    public static float hitmarkerTimer = 0f;

    // ==========================================
    // NOUVEAU : DÉCLENCHEUR UI MULTIJOUEUR
    // ==========================================
    public static void TriggerHitmarker(bool isKill)
    {
        // 1. On déclenche le visuel (l'icône au centre de l'écran)
        hitmarkerTimer = 0.3f; 

        // 2. On joue le son correspondant exactement à la même frame !
        if (isKill)
        {
            PlaySoundWithPriority(killSound, SoundPriority.Critical);
        }
        else
        {
            PlaySoundWithPriority(hitmarkerSound, SoundPriority.High);
        }
    }


    // 1. La base de données de tous les spawns de joueur selon la map
    static Vector3[][] mapPlayerSpawns = new Vector3[][]
    {
        // Map 0 : Tutoriel
        [new Vector3(-13, 2, -48),
        new Vector3(-44, 13, 46),
        new Vector3(-44, 13, -30),
        new Vector3(46, 13, -45),
        new Vector3(46, 13, 46),
        new Vector3(15, 2, 33),
        
        
        ],


        // Map 1 : La Ville (Tes anciennes coordonnées)
        [new Vector3(3, 0, -21),
        new Vector3(-104, 21, 45), 
        new Vector3(-107, 0, 155), 
        new Vector3(-8, 10, 96), 
        new Vector3(-39, 31, 85)],
        
        
        // Map 2 : Sandbox
        [
        new Vector3(-30f, 0.12f,  70f), new Vector3(90f, 0.12f,  70f),
        new Vector3( 90f, 0.12f,-130f), new Vector3(-40f,0.12f,-128f),
        new Vector3( 28f, 0.12f,  -2f), new Vector3(-38f,18.1f, -24f),  // plateforme P2
        new Vector3( 20f, 20.1f, -32f), new Vector3(78f, 6.2f,  47f)   // PE / landing couloir
        ],


        // Map 3 : Blocs (sol Y=0, X:[-43.4..94.3] Z:[-40.7..25.4]) — dérivé de blockmap.glb
        [
        new Vector3(-9, 1.1f,  -2f),
        new Vector3(-30, 1.1f,  -19f),
        new Vector3(20, 1.1f,  8f),
        new Vector3(68, 1.1f,  -20f),
        new Vector3(89, 1.1f,  3f),
        new Vector3(39, 1.1f,  -36f),
        ]
    };
    
    // 2. La liste active (qui sera remplie par le jeu)
    public static List<Vector3> listeSpawns = new List<Vector3> { new Vector3(0, 5, 0) }; // Coordonnée par défaut pour le démarrage


    static int FPS = 60;
    static int HauteurFenetre = 1080;
    static int LargeurFenetre = 1920;
    static int X_carre;
    static int Y_carre;

    //On remplace "isMenuGameOpen" par un système plus pro
    public static bool isPaused = false;
    
    public static bool isOptionsMenuOpen = false;
    

    static List<Texture2D> ListeTexture = new List<Texture2D>();

    enum ActiveMusic
    {
        None,
        Menu,
        Game
    }
    
    // Images & HUD
    static Texture2D Logo, startimg, clic1, clic2, clic3, sniperaim, cibleTexture, ImageMapTest, ImageMapVille, Imageville2, ImageMapBlocs;
    static Texture2D play_button, play_active, option_button, option_active, quit_button, quit_active, background, BlurBackground, imageexplosion;
    static float tempsAffichage = 3.0f; 
    static float opaciteImage = 255.0f; 
    static List<EffetClic> ListeEffets = new List<EffetClic>();

    // Modèles 3D et Sons
    static Model mapModel, sniper, karambit, bazooka, sword, shotgun, pistol, revolver, barrelModel;

    
    // Variables pour gérer la mémoire de la map et éviter les crashs
    public static BepuPhysics.Collidables.Mesh bepuMapMesh;
    public static bool mapDejaChargee = false;
    // NOUVEAU : Pour mémoriser et supprimer proprement la map !
    public static StaticHandle mapStaticHandle;



    public static Sound[] deathSounds = new Sound[4]; // Pool de 4 sons de death pour éviter les conflits
    public static int deathSoundIndex = 0;
    static Sound snipershot, karambitshot, bazookashot, shotgunshot, pistolshot, revolvershot, swordslash, select, unselect, survole, swoosh, explosion, wallSound;
    static Music menuMusic, gameMusic;
    static float musicVolume = 0.4f;
    static ActiveMusic currentMusicState = ActiveMusic.None;
    static Shader lightShader;
    static int lightPosLoc;
    static int lightColorLoc;
    static int viewPosLoc;
    public static int applyFogLoc;

    // Overlay de dégâts
    public static float damageOverlayOpacity = 0f;
    public static Sound hitSound, weaponSwitchSound, reloadSound, noAmmoSound, groundImpactSound;
    public static Sound hitmarkerSound, killSound;
    public static Sound dashNotifSound, wallNotifSound;

    //Le système de pas
    public static Sound[] footstepLeft = new Sound[3];
    public static Sound[] footstepRight = new Sound[3];
    public static float footstepTimer = 0f;
    public static bool isLeftStep = true;

    public static Sound windSound, heartbeatSound;

    public static float targetWindVolume = 0f;
    public static float currentWindVolume = 0f;


    // Gestion des sons pour éviter les chevauchements
    public enum SoundPriority
    {
        Low,      // Sons UI, ambiance
        Medium,   // Sons de dégâts, effets secondaires
        High,     // Sons critiques : tirs, explosions, changements d'armes
        Critical  // Sons vitaux : mort, game over
    }



    // NOUVEAU : Le Moteur Audio Avancé
    public static float duckingTimer = 0f;
    public static float duckingStrength = 1f; // 1f = Volume normal, 0.2f = Volume assourdi


    static Vector3 lightPosition = new Vector3(0.0f, 10.0f, 0.0f);
    static float mapScale = 1f;
    static Vector3 mapPosition = new Vector3(0.0f, 0.0f, 0.0f);


    // Ennemis
    public static Model ennemiModel;
    public static List<Enemy> enemiesList = new List<Enemy>();

    public class DamageText
    {
        public Vector3 position;
        public string text;
        public float timer;
        public float maxTimer;

        public DamageText(Vector3 pos, int damage)
        {
            position = pos; 
            text = damage.ToString(); // On transforme le nombre en texte
            timer = 0.8f;     // Le texte vivra pendant 0.8 seconde
            maxTimer = 0.8f;
        }
    }
    public static List<DamageText> activeDamageTexts = new List<DamageText>();

    public static TypedIndex formeBarilIndex;
    public static Vector3 barrelPhysicsCenterOffset;

    




    // ========================================================
    // [ZONE BEPU] VARIABLES DE LA PHYSIQUE ET DE LA CAMÉRA
    // ========================================================
    static Camera3D camera;
    
    // Le Cerveau de la physique
    public static Simulation simulation;
    public static BufferPool pool;
    public static BodyHandle PlayerId;
    public static TypedIndex PlayerTicket;
    public static TypedIndex PlayerTicketAccroupi;
    public static BodyInertia inertieCube;
    public static TypedIndex ticketCube;
    static List<BodyHandle> ListeObjets = new List<BodyHandle>();

    // Variables de mouvement
    static int NbJump = 2; 
    static int NbJumpMax = 1; 

    static bool CanDash = true;
    static float dashChrono = 0;


    static float SpeedCoef = 1;
    
    // Paramètres Caméra FPS
    static float CameraYaw = 0f;
    static float CameraPitch = 0f;
    static float MouseSensi = 0.003f;
    static float actualFov = 60f;
    static float boostFov = 0f;

    public static float hauteurVoulue = 0.8f;
    public static float rollActuel = 0f;



    // ========================================================
    // GESTIONNAIRE DE SURVIE ET SPAWNS
    // ========================================================
    public static float survivalTime = 0f;
    // Solo : active/désactive l'apparition des zombies (réglé par l'interrupteur du menu Solo).
    public static bool zombiesActifs = true;
    // Nombre max de zombies simultanés (40 en solo normal, 3 dans le tuto).
    public static int maxZombies = 40;
    public static int killCount = 0;
    public static int deathCount = 0;
    public static float enemySpawnTimer = 0f;
    public static float timeBetweenSpawns = 3.0f; // 1 ennemi toutes les 3 secondes
    public static List<Vector3> enemySpawnPoints = new List<Vector3>();




    // ========================================================
    // SYSTÈME DE MURS (BUILD MODE)
    // ========================================================
    public static bool modeConstruction = false;
    public static Color couleurMurTransparent = new Color(255, 130, 50, 150); // Bleu holographique transparent
    
    // La classe qui définit un mur posé
    public class MurPose
    {
        public Vector3 position;
        public Quaternion rotation; // NOUVEAU : Sauvegarde la rotation 3D !
        public StaticHandle handlePhysique;
        public int couleurIdx;      // index dans couleursSkin : le mur porte la couleur du skin de son poseur

        public MurPose(Vector3 pos, Quaternion angle, StaticHandle handle, int couleur = 0)
        {
            position = pos;
            rotation = angle;
            handlePhysique = handle;
            couleurIdx = couleur;
        }
    }
    public static List<MurPose> listeMur = new List<MurPose>();

    public static TypedIndex formeMurIndex;
    public static Model visuelMur;

    public static int wallChrono;




    // ==========================================
    // VARIABLES RÉSEAU (MULTIJOUEUR)
    // ==========================================
    public static bool isOnline = false; // Vrai si on joue en LAN
    public static bool isServer = false; // Vrai si C'EST NOUS l'Hôte (Le Dieu de la physique)
    
    public static NetManager netManager;           // Le gestionnaire de la carte réseau
    public static EventBasedNetListener netListener; // Celui qui "écoute" les messages arriver

    public static NetPacketProcessor netProcessor = new NetPacketProcessor();

    // --- Reconnexion client (Task 3.4) ---
    public static bool clientEnReconnexion = false;   // vrai pendant la fenêtre de reconnexion
    public static float reconnexionRestant = 0f;      // secondes restantes avant abandon
    static float reconnexionRetryTimer = 0f;          // cadence des tentatives
    static IPEndPoint hostEndpoint = null;            // l'hôte à recontacter


    // ==========================================
    // LA MACHINE À ÉTATS DU JEU
    // ==========================================
    public enum GameState 
    {
        MainMenu,           // L'écran titre avec tes textures
        Options,            // Ton menu d'options avec les sliders
        ModeSelection,      // Choix Solo / Multi (Sur fond flou)
        ChoiceMap,          // TA page de sélection (Tuto, Ville, Sandbox)
        NetworkHub,         // Choix Héberger / Rejoindre
        Lobby,              // La salle d'attente
        Loading,            // Chargement de la map (hot-join : on attend la baseline)
        Playing,             // En jeu
        PostMatch,          // Écran de score de fin de match (avant retour au salon)
        ServerBrowser,
        CreateMatch,
        Customization       // Le menu de personnalisation du personnage (skins)

    }
    public static GameState currentState = GameState.MainMenu;



    // ==========================================
    // MÉMOIRE DU RADAR RÉSEAU
    // ==========================================
    public class InfoServeur 
    {
        public string NomDuSalon;
        public int JoueursActuels;
        public int JoueursMax;
        public float TempsDepuisDerniereReponse; // Pour supprimer les serveurs morts
        public IPEndPoint EndPoint; // L'adresse IP + Port (La cible exacte)
    }

    // Le dictionnaire stocke l'IP comme "Clé", et les infos comme "Valeur"
    public static Dictionary<IPEndPoint, InfoServeur> serveursDisponibles = new Dictionary<IPEndPoint, InfoServeur>();
    public static float chronoRecherche = 0f;



    // ==========================================
    // VARIABLES DU SALON MULTIJOUEUR (LOBBY)
    // ==========================================
    public static string hostMatchName = "Serveur de Test";
    public static string matchNameInput = "";
    public static string directIPInput = ""; 
    
    // NOUVEAU : Variables pour le Pseudo et le Focus des boîtes de texte
    public static string playerNameInput = "";
    public static int activeTextBox = 1; // 1 = Boîte Pseudo, 2 = Boîte Match/IP, 0 = Rien

    // Mots de passe de salon (Task 2.9) — optionnels
    public static string hostRoomPassword = "";   // défini par l'hôte à la création (vide = pas de mdp)
    public static string joinPassword = "";       // saisi par le client pour rejoindre un salon protégé

    // Connexion à un hôte AVEC les données de connexion (clé de version + mot de passe).
    // Toutes les connexions (IP directe, salon LAN, code, reconnexion) passent par ici.
    public static void ConnecterAHote(IPEndPoint ep)
    {
        NetDataWriter data = new NetDataWriter();
        data.Put(NetConfig.ConnectionKey);
        data.Put(joinPassword ?? "");
        netManager.Connect(ep, data);
    }

    public static int mapChoisieIndex = 1;
    public static bool afficherIPDuServeur = false;
    public static string adresseIPLocaleServeur = "127.0.0.1";
    
    // Structure d'un joueur dans le Lobby
    public class LobbyPlayer
    {
        public string Name;
        public bool IsReady;
        public bool IsHost;
        public int Id; // Numéro unique attribué par le serveur (0 = l'hôte)
        public NetPeer Peer; // Null si c'est Toi (l'hôte local)
    }
    public static List<LobbyPlayer> currentLobbyPlayers = new List<LobbyPlayer>();
    private static readonly object _lobbyLock = new object();


    public static void AllumerMoteurReseau(bool host)
    {
        // Si un ancien moteur tournait encore, on le coupe proprement avant d'en créer un neuf
        if (netManager != null && netManager.IsRunning) netManager.Stop();

        netProcessor = new NetPacketProcessor();

        netListener = new EventBasedNetListener();
        netManager = new NetManager(netListener);
        isServer = host;

        // ==========================================
        // SÉCURITÉ S6 : les messages non-connectés (radar LAN) sont FERMÉS par défaut.
        // La boucle principale ne les ouvre QUE dans les états où ils servent :
        //  - hôte en Lobby (répondre aux "GOOFY_REQ")
        //  - client en ServerBrowser (recevoir les "GOOFY_RES")
        // Sur internet, un inconnu qui scanne le port ne déclenche AUCUN traitement.
        // ==========================================
        netManager.BroadcastReceiveEnabled = false;
        netManager.UnconnectedMessagesEnabled = false;

        // Statistiques réseau pour le HUD F3 (débits, paquets/s)
        netManager.EnableStatistics = true;

        // GOOFY_NETSIM=latence:gigue:perte (ex: "150:30:5") -> simulation WAN en local
        AppliquerSimulationReseau();

        // Remise à zéro de la session (IDs, joueurs distants, etc.)
        ReinitialiserSessionReseau(host);
        serveursDisponibles.Clear();

        // ==========================================
        // LES OREILLES DU RADAR (Unconnected Messages)
        // ==========================================
        netListener.NetworkReceiveUnconnectedEvent += (endPoint, reader, messageType) =>
        {
            // S1/S3 : un datagramme forgé (tronqué, chaîne géante...) ne doit JAMAIS
            // faire tomber le processus -> lectures bornées + filet de sécurité global.
            try
            {
                // 1. SI ON EST L'HÔTE : On entend quelqu'un chercher une partie
                if (isServer && messageType == UnconnectedMessageType.Broadcast)
                {
                    string entete = reader.GetString(16);
                    if (entete == "GOOFY_REQ") // Sécurité : On vérifie que c'est bien notre jeu qui parle
                    {
                        // On fabrique notre réponse
                        NetDataWriter writer = new NetDataWriter();
                        writer.Put("GOOFY_RES"); // Je suis une réponse GoofyFPS
                        writer.Put(NetConfig.NettoyerNom(Program.hostMatchName, "Salon")); // Le VRAI nom de la partie
                        int nbJoueurs;
                        lock (_lobbyLock) { nbJoueurs = currentLobbyPlayers.Count; }
                        writer.Put(nbJoueurs); // Joueurs actuels (le vrai nombre !)
                        writer.Put(NetConfig.MaxPlayers); // Joueurs max (fini le 4 en dur)

                        // On renvoie la réponse directement à l'IP qui a posé la question !
                        netManager.SendUnconnectedMessage(writer, endPoint);
                    }
                }

                // 2. SI ON EST CLIENT : On reçoit la réponse d'un Hôte !
                if (!isServer && messageType == UnconnectedMessageType.BasicMessage)
                {
                    string entete = reader.GetString(16);
                    if (entete == "GOOFY_RES")
                    {
                        // On lit les données dans L'ORDRE EXACT où l'Hôte les a écrites
                        string nomSalon = NetConfig.NettoyerNom(reader.GetString(64), "Salon");
                        int jActuels = Math.Clamp(reader.GetInt(), 0, NetConfig.MaxPlayersAbsolu);
                        int jMax = Math.Clamp(reader.GetInt(), 1, NetConfig.MaxPlayersAbsolu);

                        Console.WriteLine($"salon trouvé : {nomSalon}");

                        // On met à jour notre Dictionnaire
                        if (!serveursDisponibles.ContainsKey(endPoint))
                        {
                            serveursDisponibles[endPoint] = new InfoServeur();
                        }

                        serveursDisponibles[endPoint].NomDuSalon = nomSalon;
                        serveursDisponibles[endPoint].JoueursActuels = jActuels;
                        serveursDisponibles[endPoint].JoueursMax = jMax;
                        serveursDisponibles[endPoint].EndPoint = endPoint;
                        serveursDisponibles[endPoint].TempsDepuisDerniereReponse = 0f; // On remet le chrono de mort à 0
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RÉSEAU-SÉCU] Datagramme non-connecté invalide de {endPoint} ignoré : {ex.GetType().Name}");
            }
        };

        // ==========================================
        // LES PORTES DU SERVEUR (Connexion & Déconnexion)
        // ==========================================
        
        // 1. QUELQU'UN TOQUE À LA PORTE (S7 : la douane)
        // On lit la clé nous-mêmes au lieu d'AcceptIfKey : ça permet de renvoyer
        // une VRAIE raison de rejet que le client affichera (version, plein, en partie).
        netListener.ConnectionRequestEvent += request =>
        {
            if (!isServer) { request.Reject(); return; }

            string cle = "";
            string motDePasse = "";
            try
            {
                cle = request.Data.GetString(64);
                if (!request.Data.EndOfData) motDePasse = request.Data.GetString(64); // Task 2.9
            }
            catch { /* données forgées : cle/mdp restent vides */ }

            if (cle != NetConfig.ConnectionKey)
            {
                RejeterAvecRaison(request, NetConfig.RejetVersion);
            }
            else if (!string.IsNullOrEmpty(hostRoomPassword) && motDePasse != hostRoomPassword)
            {
                RejeterAvecRaison(request, NetConfig.RejetMotDePasse); // Task 2.9
            }
            else if (netManager.ConnectedPeersCount >= NetConfig.MaxPlayers - 1) // -1 : l'hôte compte aussi
            {
                RejeterAvecRaison(request, NetConfig.RejetPlein);
            }
            else if (currentState != GameState.Lobby && currentState != GameState.Playing)
            {
                // Lobby OU partie en cours (hot-join) sont acceptés. Loading/PostMatch sont
                // des transitions courtes : on refuse temporairement (le client réessaiera).
                RejeterAvecRaison(request, NetConfig.RejetEnPartie);
            }
            else
            {
                request.Accept();
            }
        };

        // 2. QUELQU'UN EST BIEN ENTRÉ (Ou Toi tu as réussi à entrer chez l'hôte)
        netListener.PeerConnectedEvent += peer =>
        {
            Console.WriteLine($"[RÉSEAU] Connexion réussie avec {peer.Address}:{peer.Port}");
            if (!isServer)
            {
                Packets.JoinPacket passeport = new Packets.JoinPacket();

                // On utilise le pseudo tapé dans le menu, nettoyé par la règle commune (S3)
                string pseudoFinal = NetConfig.NettoyerNom(Program.playerNameInput, "Anonyme_" + Raylib.GetRandomValue(10, 99));
                passeport.PlayerName = pseudoFinal;
                myLobbyName = pseudoFinal; // On mémorise NOTRE pseudo pour se retrouver dans la liste

                // Task 3.4 : si c'est une RECONNEXION après coupure, on présente notre ancien
                // ID + jeton pour récupérer notre place et notre score.
                if (clientEnReconnexion && myPlayerId > 0)
                {
                    passeport.ReconnectId = myPlayerId;
                    passeport.ReconnectToken = myReconnectToken;
                }

                NetDataWriter writer = new NetDataWriter();
                netProcessor.Write(writer, passeport);
                peer.Send(writer, DeliveryMethod.ReliableOrdered);

                // On mémorise l'hôte pour pouvoir le recontacter en cas de coupure (Task 3.4)
                hostEndpoint = new IPEndPoint(peer.Address, peer.Port);
                clientEnReconnexion = false; // (re)connexion réussie

                // Une reconnexion réussie enchaîne sur le hot-join (MatchInfo) : on reste en
                // Lobby une fraction de seconde, puis Loading -> Playing.
                if (currentState != GameState.Playing && currentState != GameState.Loading)
                    currentState = GameState.Lobby;
            }
        };

        netListener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
        {
            Console.WriteLine($"[RÉSEAU] Déconnexion de {peer.Address}");
            if (isServer)
            {
                // On retrouve QUI vient de partir
                LobbyPlayer partant;
                lock (_lobbyLock) { partant = currentLobbyPlayers.Find(p => p.Peer == peer); }

                // Task 3.4 : en PLEIN MATCH, on réserve sa place 30 s (reconnexion possible)
                // au lieu de le purger. Sa place/score restent, il n'est juste plus dessiné.
                if (partant != null && HoteReserverSlot(partant))
                {
                    MettreAJourEtDiffuserLobby();
                }
                else if (partant != null)
                {
                    // Hors match (ou pas de grâce) : purge normale immédiate
                    int idParti = partant.Id;
                    lock (_lobbyLock) currentLobbyPlayers.RemoveAll(p => p.Id == idParti);
                    string nomParti = ObtenirNomJoueur(idParti);
                    peersParId.Remove(idParti);
                    remotePlayers.Remove(idParti);
                    peersEnBaseline.Remove(idParti);
                    tokensParId.Remove(idParti);
                    AjouterToast($"{nomParti} a quitté la partie");

                    Packets.PlayerLeftPacket depart = new Packets.PlayerLeftPacket { PlayerId = idParti };
                    NetDataWriter writerDepart = new NetDataWriter();
                    netProcessor.Write(writerDepart, depart);
                    netManager.SendToAll(writerDepart, DeliveryMethod.ReliableOrdered);
                    MettreAJourEtDiffuserLobby();
                }
            }
            else
            {
                // S7 : si l'hôte a fourni une raison (rejet version/plein/en partie),
                // on la décode pour l'afficher dans le menu au lieu d'un silence radio.
                derniereErreurReseau = "";
                try
                {
                    // Rejet à la connexion (version/plein) OU exclusion en cours de partie (kick,
                    // triche) : dans les deux cas l'hôte a joint un octet-raison qu'on affiche.
                    if ((disconnectInfo.Reason == DisconnectReason.ConnectionRejected
                         || disconnectInfo.Reason == DisconnectReason.RemoteConnectionClose)
                        && disconnectInfo.AdditionalData != null && !disconnectInfo.AdditionalData.EndOfData)
                    {
                        derniereErreurReseau = NetConfig.MessageRejet(disconnectInfo.AdditionalData.GetByte());
                    }
                    else if (disconnectInfo.Reason == DisconnectReason.ConnectionFailed)
                    {
                        derniereErreurReseau = "Impossible de joindre l'hôte.";
                    }
                    else if (disconnectInfo.Reason == DisconnectReason.Timeout)
                    {
                        derniereErreurReseau = "Connexion perdue avec l'hôte.";
                    }
                }
                catch { /* données de rejet forgées : on garde le message vide */ }
                if (derniereErreurReseau != "") Console.WriteLine($"[RÉSEAU] {derniereErreurReseau}");

                // Task 3.4 — RECONNEXION : une simple COUPURE réseau (Timeout) en pleine
                // partie ne renvoie plus au menu. On tente de se rebrancher pendant 30 s ;
                // l'hôte garde notre place et notre score le temps qu'on revienne.
                bool coupureReseau = disconnectInfo.Reason == DisconnectReason.Timeout;
                if (coupureReseau && (currentState == GameState.Playing || currentState == GameState.Loading)
                    && hostEndpoint != null && myPlayerId > 0)
                {
                    clientEnReconnexion = true;
                    reconnexionRestant = GraceReconnexionSec;
                    reconnexionRetryTimer = 0f;
                    derniereErreurReseau = "";
                    Console.WriteLine("[RÉSEAU] Coupure détectée : tentative de reconnexion (30 s)...");
                    // On NE coupe PAS netManager : il sert à reconnecter. On garde la map chargée.
                }
                else if (currentState == GameState.Playing)
                {
                    // Déconnexion volontaire / kick / hôte fermé : retour propre au menu
                    CouperReseau();
                    Raylib.EnableCursor();
                    isPaused = false;
                    currentState = GameState.NetworkHub;
                }
                else
                {
                    remotePlayers.Clear();
                    currentState = GameState.NetworkHub;
                }
            }
        };

        // ==========================================
        // LA DOUANE (Traitement des paquets connectés)
        // ==========================================
        
        // 1. Dès qu'un paquet officiel arrive, on le donne au traducteur automatique (NetProcessor)
        // SÉCURITÉ S1 : un paquet malformé (tronqué, forgé, type inconnu) lève une exception
        // dans le NetProcessor. Sans ce filet, UN datagramme pourri = crash de l'hôte pour
        // tout le monde. Ici : on journalise et on déconnecte l'expéditeur fautif, point.
        netListener.NetworkReceiveEvent += (peer, reader, deliveryMethod, channel) =>
        {
            try
            {
                netProcessor.ReadAllPackets(reader, peer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RÉSEAU-SÉCU] Paquet invalide de {peer.Address} ({ex.GetType().Name}) : déconnexion du peer.");
                peer.Disconnect();
            }
        };

        // 2. On abonne le traducteur à notre "Passeport" (JoinPacket)
        // Ça veut dire : "Si tu lis un JoinPacket, déclenche la fonction OnJoinPacketReceived"
        netProcessor.SubscribeReusable<Packets.JoinPacket, NetPeer>(OnJoinPacketReceived);
        // Abonnements aux nouveaux paquets du Lobby
        netProcessor.SubscribeReusable<Packets.ToggleReadyPacket, NetPeer>(OnReadyPacketReceived);
        netProcessor.SubscribeNetSerializable<Packets.LobbyStatePacket, NetPeer>(OnLobbyStateReceived);
        netProcessor.SubscribeReusable<Packets.StartGamePacket, NetPeer>(OnStartGameReceived);
        // Abonnements aux paquets de la partie en cours (positions, tirs, dégâts...)
        EnregistrerPaquetsEnJeu();


        // Lancement effectif de la carte réseau
        if (host) 
        {
            netManager.Start(NetConfig.GamePort); // Le serveur ouvre sa porte fixe (7777)

            // ==========================================
            // LOGIQUE DE RÉCUPÉRATION DE L'IP LOCALE (Calculé 1 seule fois)
            // ==========================================
            try
            {
                var dnsHost = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in dnsHost.AddressList)
                {
                    // On ne prend QUE les adresses IPv4 classiques (InterNetwork), pas l'IPv6
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        adresseIPLocaleServeur = ip.ToString();
                        break; // On a trouvé notre IP locale principale, on s'arrête
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[RÉSEAU - ERROR] Impossible de lire l'IP locale : " + ex.Message);
                adresseIPLocaleServeur = "IP Introuvable";
            }
        }
        else
        {
            netManager.Start(); // Le client prend un port au hasard
        }

        // Le module de perçage NAT (Phase 2) : hôte ET client en ont besoin
        InitNatPunch();
    }



    // ==========================================
    // LES OUTILS DE LA DOUANE (S7) + SIMULATION WAN
    // ==========================================
    // Le dernier message d'erreur réseau, affiché dans les menus (rejet, timeout...)
    public static string derniereErreurReseau = "";

    // Rejette une demande de connexion AVEC un code de raison que le client saura lire
    static void RejeterAvecRaison(ConnectionRequest request, byte code)
    {
        NetDataWriter writer = new NetDataWriter();
        writer.Put(code);
        request.Reject(writer);
        Console.WriteLine($"[SERVEUR] Connexion de {request.RemoteEndPoint} rejetée : {NetConfig.MessageRejet(code)}");
    }

    // GOOFY_NETSIM=latence:gigue:perte (ms:ms:%) — pour tester le WAN sans quitter le LAN.
    // Ex: GOOFY_NETSIM=150:30:5 -> 150 ms ±30 de latence + 5 % de perte de paquets.
    static void AppliquerSimulationReseau()
    {
        string config = Environment.GetEnvironmentVariable("GOOFY_NETSIM");
        if (string.IsNullOrEmpty(config)) return;

        string[] parts = config.Split(':');
        int latence = parts.Length > 0 && int.TryParse(parts[0], out int l) ? l : 0;
        int gigue = parts.Length > 1 && int.TryParse(parts[1], out int g) ? g : 0;
        int perte = parts.Length > 2 && int.TryParse(parts[2], out int p) ? p : 0;

        if (latence > 0)
        {
            netManager.SimulateLatency = true;
            netManager.SimulationMinLatency = Math.Max(latence - gigue, 0);
            netManager.SimulationMaxLatency = latence + gigue;
        }
        if (perte > 0)
        {
            netManager.SimulatePacketLoss = true;
            netManager.SimulationPacketLossChance = Math.Clamp(perte, 0, 100);
        }
        Console.WriteLine($"[NETSIM] Simulation active : {latence} ms ±{gigue}, {perte} % de perte");
    }

    // 1. QUAND LE SERVEUR REÇOIT UN PASSEPORT (JoinPacket)
    public static void OnJoinPacketReceived(Packets.JoinPacket passeport, NetPeer peer)
    {
        if (isServer)
        {
            string adresseIP = (peer != null) ? peer.Address.ToString() : "127.0.0.1 (SIMULATION)";

            // Sécurité S3 : pseudo nettoyé par la règle commune (longueur, séparateurs, contrôle)
            string nomPropre = NetConfig.NettoyerNom(passeport.PlayerName, "Anonyme_" + Raylib.GetRandomValue(10, 99));

            // RECONNEXION (Task 3.4) : ce joueur revient-il sur une place réservée ?
            if (peer != null)
            {
                int idRepris = HoteTenterReconnexion(peer, passeport);
                if (idRepris >= 0)
                {
                    // On lui renvoie sa carte d'identité (même ID + même jeton) et sa baseline.
                    tokensParId.TryGetValue(idRepris, out int tokenRepris);
                    Packets.YourIdPacket carteR = new Packets.YourIdPacket { PlayerId = idRepris, ReconnectToken = tokenRepris };
                    NetDataWriter wR = new NetDataWriter();
                    netProcessor.Write(wR, carteR);
                    peer.Send(wR, DeliveryMethod.ReliableOrdered);

                    // Trombinoscope + relance de la baseline (le match tourne forcément)
                    EnvoyerInfoJoueurA(peer, 0, myLobbyName, skinCouleur, skinHat, skinFace);
                    foreach (RemotePlayer autre in remotePlayers.Values)
                    {
                        if (autre.Id == idRepris) continue;
                        EnvoyerInfoJoueurA(peer, autre.Id, autre.Name, autre.SkinColor, autre.SkinHat, autre.SkinFace);
                    }
                    if (currentState == GameState.Playing) HoteDemarrerBaseline(peer, idRepris);
                    MettreAJourEtDiffuserLobby();
                    return;
                }
            }

            // Le serveur attribue un numéro unique à ce joueur
            int nouvelId = nextPlayerId++;

            Console.WriteLine($"[SERVEUR] Le joueur '{nomPropre}' (ID {nouvelId}) rejoint le salon ({adresseIP})");

            // On ajoute le vrai joueur à la liste du serveur !
            lock (_lobbyLock)
            {
                currentLobbyPlayers.Add(new LobbyPlayer {
                    Name = nomPropre,
                    IsReady = false,
                    IsHost = false,
                    Id = nouvelId,
                    Peer = peer
                });
            }

            if (peer != null)
            {
                // SÉCURITÉ S2 : l'identité de ce peer est scellée ICI, une fois pour toutes.
                // Tous les handlers déduiront ensuite "qui parle" de ce Tag, jamais des paquets.
                peer.Tag = nouvelId;
                peersParId[nouvelId] = peer;

                // Jeton de reconnexion (Task 3.4) : gardé côté hôte, renvoyé au client.
                int token = rngToken.Next(1, int.MaxValue);
                tokensParId[nouvelId] = token;

                // On envoie au nouveau venu sa carte d'identité (son numéro unique + jeton)
                Packets.YourIdPacket carte = new Packets.YourIdPacket { PlayerId = nouvelId, ReconnectToken = token };
                NetDataWriter writerId = new NetDataWriter();
                netProcessor.Write(writerId, carte);
                peer.Send(writerId, DeliveryMethod.ReliableOrdered);

                // Et le trombinoscope : la fiche de l'hôte + celle de chaque joueur déjà présent
                // (le nouveau venu recevra les skins ; les siens arriveront via SON PlayerInfo)
                EnvoyerInfoJoueurA(peer, 0, myLobbyName, skinCouleur, skinHat, skinFace);
                foreach (RemotePlayer rp in remotePlayers.Values)
                {
                    if (rp.Id == nouvelId) continue;
                    EnvoyerInfoJoueurA(peer, rp.Id, rp.Name, rp.SkinColor, rp.SkinHat, rp.SkinFace);
                }

                // HOT-JOIN : si l'hôte est déjà en pleine partie, on lance la baseline
                // (le retardataire va charger la map puis recevoir murs + scores + spawn).
                if (currentState == GameState.Playing)
                    HoteDemarrerBaseline(peer, nouvelId);
                else
                    AjouterToast($"{nomPropre} a rejoint le salon");
            }

            // On diffuse la mise à jour à tout le monde
            MettreAJourEtDiffuserLobby();
        }
    }

    // 2. QUAND LE SERVEUR REÇOIT UN CHANGEMENT DE STATUT "PRÊT"
    public static void OnReadyPacketReceived(Packets.ToggleReadyPacket packet, NetPeer peer)
    {
        if (isServer)
        {
            // On cherche le joueur correspondant au peer qui a envoyé le message
            var joueur = currentLobbyPlayers.Find(p => p.Peer == peer);
            if (joueur != null)
            {
                joueur.IsReady = packet.IsReady;
                MettreAJourEtDiffuserLobby();
            }
        }
    }

    // 3. QUAND LE CLIENT REÇOIT LA MISE À JOUR DU SERVEUR
    // (Paquet STRUCTURÉ : la sérialisation borne déjà les compteurs et les chaînes)
    public static void OnLobbyStateReceived(Packets.LobbyStatePacket packet, NetPeer peer)
    {
        if (!isServer)
        {
            hostMatchName = packet.MatchName;
            mapChoisieIndex = packet.MapIndex;

            lock (_lobbyLock)
            {
                currentLobbyPlayers.Clear();
                if (packet.Joueurs != null)
                {
                    foreach (var entree in packet.Joueurs)
                    {
                        currentLobbyPlayers.Add(new LobbyPlayer
                        {
                            Name = entree.Nom,
                            IsReady = entree.Pret,
                            IsHost = entree.EstHote,
                            Id = entree.Id
                        });
                    }
                }
            }
        }
    }

    // 4. QUAND LE SERVEUR REÇOIT L'ORDRE DE LANCEMENT OU QUE LE CLIENT LE REÇOIT

    
    public static bool lancerPartieEnAttente = false;
    public static int mapIndexEnAttente = 0;
    public static int scoreLimitEnAttente = 20;
    public static int timeLimitEnAttente = 600;

    public static void OnStartGameReceived(Packets.StartGamePacket packet, NetPeer peer)
    {
        // On mémorise juste la commande, sans rien exécuter (le vrai lancement se fait dans la boucle)
        mapIndexEnAttente = packet.MapIndex;
        scoreLimitEnAttente = packet.ScoreLimit;
        timeLimitEnAttente = packet.TimeLimitSec;
        lancerPartieEnAttente = true;
    }

    // FONCTION UTILITAIRE : Le serveur emballe le Lobby et l'envoie à tout le monde
    // (Version structurée : chaque joueur est un vrai champ, plus de bricolage de chaînes)
    public static void MettreAJourEtDiffuserLobby()
    {
        if (!isServer) return;

        Packets.LobbyStatePacket pack = new Packets.LobbyStatePacket
        {
            MatchName = hostMatchName,
            MapIndex = (byte)Math.Clamp(mapChoisieIndex, 0, 255)
        };

        lock (_lobbyLock)
        {
            pack.Joueurs = new Packets.LobbyStatePacket.Entree[currentLobbyPlayers.Count];
            for (int i = 0; i < currentLobbyPlayers.Count; i++)
            {
                var p = currentLobbyPlayers[i];
                pack.Joueurs[i] = new Packets.LobbyStatePacket.Entree
                {
                    Id = (byte)Math.Clamp(p.Id, 0, 255),
                    Nom = p.Name,
                    Pret = p.IsReady,
                    EstHote = p.IsHost
                };
            }
        }

        NetDataWriter writer = new NetDataWriter();
        netProcessor.WriteNetSerializable(writer, ref pack);

        // On envoie le colis à tous les clients connectés
        netManager.SendToAll(writer, DeliveryMethod.ReliableOrdered);
    }


    public static void ChargerMapReseau(int mapIndex, bool spawnAleatoire = true)
{
    // Nettoyage (une seule fois, pas en double) — partagé avec le mode solo
    DechargerMapActuelle();
    foreach (Enemy e in enemiesList) if (e.isAlive) simulation.Bodies.Remove(e.bodyId);
    enemiesList.Clear();
    activeExplosions.Clear();
    activeDamageTexts.Clear();
    remoteShots.Clear();
    killCount = 0; deathCount = 0;
    modeTuto = false;      // sécurité : on sort du mode tuto en rejoignant une partie en ligne
    maxZombies = 40;
    localPlayer.Respawn(); // On repart avec toute sa vie

    // Les chemins de tes maps
    string[] ModelMapPath = { "assets/models/maps/test.glb", "assets/models/maps/map.glb", "assets/models/maps/sandbox.glb", "assets/models/maps/blockmap.glb" };
    mapModel = Raylib.LoadModel(ModelMapPath[mapIndex]);

    // CORRECTION DU "CRASH 2" : avant, la map réseau n'avait AUCUNE collision physique
    // (le bloc d'extraction manquait), donc les joueurs tombaient dans le vide et
    // mapStaticHandle restait invalide pour le nettoyage suivant.
    ExtraireTrianglesMap();

    unsafe { for (int j = 0; j < mapModel.MaterialCount; j++) mapModel.Materials[j].Shader = lightShader; }
    ApplyMapTextures();

    LoadMapSpawns(mapIndex);
    InitBarrels();
    InitEnnemis();
    EquiperArmeDeDepart();

    // Hot-join : on NE choisit PAS de spawn ici (l'hôte nous en donne un via BaselineEnd).
    if (spawnAleatoire)
    {
        int idx = Raylib.GetRandomValue(0, listeSpawns.Count - 1);
        BodyReference joueur = simulation.Bodies.GetBodyReference(PlayerId);
        joueur.Pose.Position = listeSpawns[idx];
        joueur.Velocity.Linear = Vector3.Zero;
        joueur.Velocity.Angular = Vector3.Zero;
        Raylib.DisableCursor();
    }
}

    // ========================================================
    // MIPMAPS + FILTRAGE TRILINÉAIRE sur les textures de la map.
    // Sans mipmaps, les textures tuilées scintillent et donnent un effet de
    // "répétition à l'infini" vues de loin. On génère les mips puis on passe en
    // trilinéaire pour un rendu propre à toutes les distances.
    // ========================================================
    static unsafe void ApplyMapTextures()
    {
        for (int i = 0; i < mapModel.MaterialCount; i++)
        {
            Texture2D tex = mapModel.Materials[i].Maps[(int)MaterialMapIndex.Albedo].Texture;
            if (tex.Id > 0 && tex.Width > 1) // on ignore la texture blanche 1x1 par défaut
            {
                Raylib.GenTextureMipmaps(ref tex);
                Raylib.SetTextureFilter(tex, TextureFilter.Trilinear);
                mapModel.Materials[i].Maps[(int)MaterialMapIndex.Albedo].Texture = tex;
            }
        }
    }

    // ========================================================
    // Décharge la map actuelle : modèle GPU, collision BEPU, murs posés
    // et colliders de barils encore vivants. À appeler AVANT de charger
    // une nouvelle map (solo ET réseau) — sinon les collisions de
    // l'ancienne map restent actives (murs/sols fantômes invisibles).
    // ========================================================
    public static void DechargerMapActuelle()
    {
        // Murs posés par les joueurs (leur handle physique vit dans listeMur)
        foreach (MurPose mur in listeMur) simulation.Statics.Remove(mur.handlePhysique);
        listeMur.Clear();

        // Barils : LoadMapSpawns fait barrelSpots.Clear(), donc si on ne
        // retire pas leurs statics ICI, les handles sont perdus à jamais.
        foreach (BarrelSpot spot in barrelSpots)
            if (spot.estSolide) { simulation.Statics.Remove(spot.handlePhysique); spot.estSolide = false; }

        // La map elle-même (visuel + collision)
        if (mapDejaChargee)
        {
            simulation.Statics.Remove(mapStaticHandle);
            Raylib.UnloadModel(mapModel);
            bepuMapMesh.Dispose(pool);
            mapDejaChargee = false;
        }
    }

    // ========================================================
    // LE PONT RAYLIB -> BEPU : transforme le modèle 3D de la map (mapModel)
    // en mesh de collision physique. Utilisé par le mode SOLO (ChoiceMap)
    // ET par le mode RÉSEAU (ChargerMapReseau).
    // Remplit : bepuMapMesh, mapStaticHandle, mapDejaChargee.
    // ========================================================
    public static void ExtraireTrianglesMap()
    {
        Console.WriteLine($"[DEBUG] Extraction des triangles de la map ({mapModel.MeshCount} parties détectées)...");

        unsafe
        {
            int totalTriangles = 0;
            for (int i = 0; i < mapModel.MeshCount; i++) totalTriangles += mapModel.Meshes[i].TriangleCount;

            pool.Take<Triangle>(totalTriangles, out var bepuTriangles);
            int triangleActuel = 0;

            for (int m = 0; m < mapModel.MeshCount; m++)
            {
                Raylib_cs.Mesh raylibMesh = mapModel.Meshes[m];
                float* vertices = raylibMesh.Vertices;

                // Raylib stocke les indices en 16 bits : au-delà de 65 535 sommets
                // par mesh, le glTF est DÉJÀ tronqué au chargement (rendu et
                // collisions faux). On prévient clairement au lieu de laisser
                // la map se corrompre en silence.
                if (raylibMesh.VertexCount > ushort.MaxValue)
                    Console.WriteLine($"[ATTENTION] Mesh {m} : {raylibMesh.VertexCount} sommets > 65535. " +
                        "Limite des indices 16 bits de Raylib dépassée, géométrie corrompue : " +
                        "découpe cet objet en plusieurs meshes dans Blender.");
                ushort* indices = raylibMesh.Indices;

                for (int t = 0; t < raylibMesh.TriangleCount; t++)
                {
                    int index1, index2, index3;

                    // Si Blender a bien fait son travail (Mesh indexé)
                    if (indices != null)
                    {
                        index1 = indices[t * 3 + 0];
                        index2 = indices[t * 3 + 1];
                        index3 = indices[t * 3 + 2];
                    }
                    // Si Blender a exporté "en vrac" (Vertices purs)
                    else
                    {
                        index1 = t * 3 + 0;
                        index2 = t * 3 + 1;
                        index3 = t * 3 + 2;
                    }

                    Vector3 point1 = new Vector3(vertices[index1 * 3], vertices[index1 * 3 + 1], vertices[index1 * 3 + 2]) * mapScale;
                    Vector3 point2 = new Vector3(vertices[index2 * 3], vertices[index2 * 3 + 1], vertices[index2 * 3 + 2]) * mapScale;
                    Vector3 point3 = new Vector3(vertices[index3 * 3], vertices[index3 * 3 + 1], vertices[index3 * 3 + 2]) * mapScale;

                    bepuTriangles[triangleActuel] = new Triangle(point1, point3, point2);
                    triangleActuel++;
                }
            }

            // On utilise notre variable globale au lieu de "var"
            bepuMapMesh = new BepuPhysics.Collidables.Mesh(bepuTriangles, Vector3.One, pool);
            TypedIndex ticketCarte = simulation.Shapes.Add(bepuMapMesh);

            // CORRECTION : On SAUVEGARDE le handle dans notre variable globale
            mapStaticHandle = simulation.Statics.Add(new StaticDescription(mapPosition, ticketCarte));

            // On valide qu'une map est en mémoire pour le prochain nettoyage !
            mapDejaChargee = true;

            Console.WriteLine($"[DEBUG] Map chargée avec {totalTriangles} triangles ! (Vérifie que ce chiffre n'est pas zéro)");
        }
    }


    
public static void GenererPhysiqueMap(Model modele)
{
    Console.WriteLine($"[DEBUG] Extraction des triangles de la map ({modele.MeshCount} parties détectées)...");
    unsafe 
    {
        int totalTriangles = 0;
        for (int i = 0; i < modele.MeshCount; i++) totalTriangles += modele.Meshes[i].TriangleCount;

        pool.Take<Triangle>(totalTriangles, out var bepuTriangles);
        int triangleActuel = 0;

        for (int m = 0; m < modele.MeshCount; m++)
        {
            Raylib_cs.Mesh raylibMesh = modele.Meshes[m];
            float* vertices = raylibMesh.Vertices;
            ushort* indices = raylibMesh.Indices; 

            for (int t = 0; t < raylibMesh.TriangleCount; t++)
            {
                int index1 = (indices != null) ? indices[t * 3 + 0] : t * 3 + 0;
                int index2 = (indices != null) ? indices[t * 3 + 1] : t * 3 + 1;
                int index3 = (indices != null) ? indices[t * 3 + 2] : t * 3 + 2;

                Vector3 point1 = new Vector3(vertices[index1 * 3], vertices[index1 * 3 + 1], vertices[index1 * 3 + 2]) * mapScale;
                Vector3 point2 = new Vector3(vertices[index2 * 3], vertices[index2 * 3 + 1], vertices[index2 * 3 + 2]) * mapScale;
                Vector3 point3 = new Vector3(vertices[index3 * 3], vertices[index3 * 3 + 1], vertices[index3 * 3 + 2]) * mapScale;

                bepuTriangles[triangleActuel] = new Triangle(point1, point3, point2);
                triangleActuel++;
            }
        }

        bepuMapMesh = new BepuPhysics.Collidables.Mesh(bepuTriangles, Vector3.One, pool);
        TypedIndex ticketCarte = simulation.Shapes.Add(bepuMapMesh);
        mapStaticHandle = simulation.Statics.Add(new StaticDescription(mapPosition, ticketCarte));
        mapDejaChargee = true;
    }
}






    // ========================================================
    // INITIALISATION PRINCIPALE
    // ========================================================
    static void Main(string[] args)
    {
        // Auto-tests des zombies (fenêtre cachée, pas de partie) :
        // dotnet run -- --navtest  : le pathfinding A* trouve-t-il les chemins ?
        // dotnet run -- --aitest   : le comportement runtime (anti-gel, fluidité) est-il bon ?
        if (args.Length > 0 && args[0] == "--navtest") { LancerNavTest(); return; }
        if (args.Length > 0 && args[0] == "--aitest") { LancerAiTest(); return; }
        // dotnet run -- --skintest [filtre] : aligne tous les chapeaux/visages et screenshot
        // (le filtre ajoute un gros plan du chapeau correspondant, ex: --skintest robot)
        if (args.Length > 0 && args[0] == "--skintest") { LancerSkinTest(args.Length > 1 ? args[1] : ""); return; }

        //Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(LargeurFenetre, HauteurFenetre, "GoofyFPS");

        // Par défaut Raylib fait de ÉCHAP la touche qui FERME le jeu (WindowShouldClose).
        // On la désactive : ÉCHAP sert maintenant à mettre en pause (comme TAB), pas à quitter.
        Raylib.SetExitKey(KeyboardKey.Null);

        Image ico = Raylib.LoadImage("assets/textures/icons/GoofyFPS-small.png");
        Raylib.SetWindowIcon(ico);

        // --- 1. CHARGEMENT COLLÈGUE (Assets) ---
        Logo = Raylib.LoadTexture("assets/textures/ui/GoofyFPS.png");
        clic1 = Raylib.LoadTexture("assets/textures/ui/click/clic-blanc.png");
        clic2 = Raylib.LoadTexture("assets/textures/ui/click/clic2-blanc.png");
        clic3 = Raylib.LoadTexture("assets/textures/ui/click/clic3-blanc.png");
        startimg = Raylib.LoadTexture("assets/textures/hud/epsteintrump.png");
        imageexplosion = Raylib.LoadTexture("assets/textures/hud/exploimage.png");

        play_active = Raylib.LoadTexture("assets/textures/ui/buttons/play-active.png");
        play_button = Raylib.LoadTexture("assets/textures/ui/buttons/play-base.png");
        option_button = Raylib.LoadTexture("assets/textures/ui/buttons/option-base.png");
        option_active = Raylib.LoadTexture("assets/textures/ui/buttons/option-active.png");
        quit_active = Raylib.LoadTexture("assets/textures/ui/buttons/quit-active.png");
        quit_button = Raylib.LoadTexture("assets/textures/ui/buttons/quit-base.png");

        background = Raylib.LoadTexture("assets/textures/ui/Background3.png");

        Image BlurBackgroundImg = Raylib.LoadImage("assets/textures/ui/Background3.png");
        Raylib.ImageBlurGaussian(ref BlurBackgroundImg, 10);
        BlurBackground = Raylib.LoadTextureFromImage(BlurBackgroundImg);
        Raylib.UnloadImage(BlurBackgroundImg);


        ImageMapTest = Raylib.LoadTexture("assets/textures/maps/testMap.png");
        ImageMapVille = Raylib.LoadTexture("assets/textures/maps/ville.png");
        Imageville2 = Raylib.LoadTexture("assets/textures/maps/ville2.png");
        ImageMapBlocs = Raylib.LoadTexture("assets/textures/maps/blockmap.png");


        ennemiModel = Raylib.LoadModel("assets/models/entities/ennemy.glb");
        sniper = Raylib.LoadModel("assets/models/weapons/sniper.glb");
        sniperrifle.modelname = sniper;
        karambit = Raylib.LoadModel("assets/models/weapons/karambit.glb");
        karambitknife.modelname = karambit;
        bazooka = Raylib.LoadModel("assets/models/weapons/bazooka.glb");
        bazookaWeapon.modelname = bazooka;
        sword = Raylib.LoadModel("assets/models/weapons/sword.glb");
        swordWeapon.modelname = sword;
        shotgun = Raylib.LoadModel("assets/models/weapons/shotgun.glb");
        shotgunWeapon.modelname = shotgun;
        pistol = Raylib.LoadModel("assets/models/weapons/pistol.glb");
        pistolWeapon.modelname = pistol;
        revolver = Raylib.LoadModel("assets/models/weapons/revolver.glb");
        revolverWeapon.modelname = revolver;
        barrelModel = Raylib.LoadModel("assets/models/entities/barril.glb");
        // Calculer la bounding box du modèle pour une hitbox précise et l'aligner sur le modèle
        BoundingBox barrelBB = Raylib.GetModelBoundingBox(barrelModel);
        Vector3 barrelSize = barrelBB.Max - barrelBB.Min;
        Vector3 barrelHalfExtents = barrelSize * 0.5f;
        barrelPhysicsCenterOffset = (barrelBB.Max + barrelBB.Min) * 0.5f;
        float barrelHitboxInflation = 2f; // Agrandit la box de détection des tirs
        Box formeBaril = new Box(barrelHalfExtents.X * barrelHitboxInflation, barrelHalfExtents.Y * barrelHitboxInflation, barrelHalfExtents.Z * barrelHitboxInflation);
        sniperaim = Raylib.LoadTexture("assets/textures/hud/sniperaim.png");
        cibleTexture = Raylib.LoadTexture("assets/textures/hud/cible.png");

        // --- LES COSMÉTIQUES : chapeaux (.glb) + têtes (.png), scannés depuis assets/ ---
        ChargerCosmetiques();

        Raylib.InitAudioDevice();
        menuMusic = Raylib.LoadMusicStream("assets\\sounds\\menuMusic.mp3");
        gameMusic = Raylib.LoadMusicStream("assets\\sounds\\gameMusic.mp3");
        Raylib.SetMusicVolume(menuMusic, Settings.MasterVolume * Settings.MusicVolume);
        Raylib.SetMusicVolume(gameMusic, Settings.MasterVolume * Settings.MusicVolume);
        explosion = Raylib.LoadSound("assets\\sounds\\fahh-bomb.mp3");
        shotgunshot = Raylib.LoadSound("assets\\sounds\\sniper_shot.wav");
        karambitshot = Raylib.LoadSound("assets\\sounds\\fouchette-1.mp3");
        bazookashot = Raylib.LoadSound("assets\\sounds\\loud-explosion.mp3");
        snipershot = Raylib.LoadSound("assets\\sounds\\awp_02.mp3");
        pistolshot = Raylib.LoadSound("assets\\sounds\\gunshots-mixed.mp3");
        revolvershot = Raylib.LoadSound("assets\\sounds\\lobotomy.mp3");
        swoosh = Raylib.LoadSound("assets\\sounds\\swoosh.mp3");
        swordslash = Raylib.LoadSound("assets\\sounds\\batteuse.mp3");
        select = Raylib.LoadSound("assets\\sounds\\select.mp3");
        unselect = Raylib.LoadSound("assets\\sounds\\unselect.mp3");
        survole = Raylib.LoadSound("assets\\sounds\\survole.mp3");
        hitSound = Raylib.LoadSound("assets\\sounds\\hit.mp3");
        hitmarkerSound = Raylib.LoadSound("assets\\sounds\\hitmarker.mp3");
        killSound = Raylib.LoadSound("assets\\sounds\\kill-sound.mp3");
        dashNotifSound = Raylib.LoadSound("assets\\sounds\\dash-notif.mp3");
        wallNotifSound = Raylib.LoadSound("assets\\sounds\\wall-notif.mp3");
        windSound = Raylib.LoadSound("assets\\sounds\\Wind-howl-loop.mp3");
        heartbeatSound = Raylib.LoadSound("assets\\sounds\\Heartbeat.mp3");
        weaponSwitchSound = Raylib.LoadSound("assets\\sounds\\swoosh.mp3");
        reloadSound = Raylib.LoadSound("assets\\sounds\\reload.mp3");
        noAmmoSound = Raylib.LoadSound("assets\\sounds\\no-ammo.mp3");
        wallSound = Raylib.LoadSound("assets\\sounds\\wall.mp3");
        for (int i = 0; i < 3; i++)
        {
            footstepLeft[i] = Raylib.LoadSound($"assets\\sounds\\pas-gauche-{i + 1}.mp3");
            footstepRight[i] = Raylib.LoadSound($"assets\\sounds\\pas-droit-{i + 1}.mp3");
        }
        
        // Charger 4 instances du son de death pour permettre plusieurs lectures simultanées
        for (int i = 0; i < deathSounds.Length; i++)
        {
            deathSounds[i] = Raylib.LoadSound("assets\\sounds\\death.mp3");
        }

        // Affectation des sons aux armes après le chargement
        sniperrifle.soundname = snipershot;
        karambitknife.soundname = karambitshot;
        bazookaWeapon.soundname = bazookashot;
        shotgunWeapon.soundname = shotgunshot;
        pistolWeapon.soundname = pistolshot;
        revolverWeapon.soundname = revolvershot;
        swordWeapon.soundname = swordslash;

        lightShader = Raylib.LoadShader("assets/shaders/lighting.vs", "assets/shaders/lighting.fs");
        lightPosLoc = Raylib.GetShaderLocation(lightShader, "lightPos");
        lightColorLoc = Raylib.GetShaderLocation(lightShader, "lightColor");
        viewPosLoc = Raylib.GetShaderLocation(lightShader, "viewPos");
        applyFogLoc = Raylib.GetShaderLocation(lightShader, "applyFog");
        
        // APPLICATION DE LA LUMIÈRE SUR TOUTE LA CARTE ET LES ARMES
        unsafe
        {
            // 1. Pour la map
            for (int i = 0; i < mapModel.MaterialCount; i++)
            {
                mapModel.Materials[i].Shader = lightShader;
            }

            // 2. CORRECTION : Pour le sniper
            for (int i = 0; i < sniper.MaterialCount; i++)
            {
                sniper.Materials[i].Shader = lightShader;
            }

            // 3. CORRECTION : Pour le karambit
            for (int i = 0; i < karambit.MaterialCount; i++)
            {
                karambit.Materials[i].Shader = lightShader;
            }
            for (int i = 0; i < bazooka.MaterialCount; i++)
            {
                bazooka.Materials[i].Shader = lightShader;
            }
            for (int i = 0; i < sword.MaterialCount; i++)
            {
                sword.Materials[i].Shader = lightShader;
            }
            for (int i = 0; i < shotgun.MaterialCount; i++)
            {
                shotgun.Materials[i].Shader = lightShader;
            }
            for (int i = 0; i < pistol.MaterialCount; i++)
            {
                pistol.Materials[i].Shader = lightShader;
            }
            for (int i = 0; i < revolver.MaterialCount; i++)
            {
                revolver.Materials[i].Shader = lightShader;
            }

            // 4. (Bonus) Pour les ennemis si tu veux qu'ils réagissent aussi à la lumière
            for (int i = 0; i < ennemiModel.MaterialCount; i++)
            {
                ennemiModel.Materials[i].Shader = lightShader;
            }

            // 5. Barreils
            for (int i = 0; i < barrelModel.MaterialCount; i++)
            {
                barrelModel.Materials[i].Shader = lightShader;
            }

        }

        ListeTexture.Add(Logo);
        ListeTexture.Add(startimg);
        ListeTexture.Add(clic1);
        ListeTexture.Add(clic2);
        ListeTexture.Add(clic3);
        ListeTexture.Add(play_active);
        ListeTexture.Add(play_button);
        ListeTexture.Add(option_active);
        ListeTexture.Add(option_button);
        ListeTexture.Add(quit_active);
        ListeTexture.Add(quit_button);
        ListeTexture.Add(background);
        ListeTexture.Add(BlurBackground);
        ListeTexture.Add(imageexplosion);
        ListeTexture.Add(ImageMapTest);
        ListeTexture.Add(ImageMapVille);
        ListeTexture.Add(Imageville2);
        ListeTexture.Add(sniperaim);
        ListeTexture.Add(cibleTexture);


        // --- 2. CONFIGURATION DE LA CAMÉRA ---
        camera = new Camera3D();
        camera.Position = new Vector3(0.0f, 15.0f, 5.0f); 
        camera.Target = new Vector3(0.0f, 2.0f, 0.0f);   
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);       
        camera.FovY = 60.0f;                                          
        camera.Projection = CameraProjection.Perspective;


        // --- 3. CHARGEMENT BEPU (Le monde physique) ---
        pool = new BufferPool();
        Vector3 gravity = new Vector3(0, -10f, 0); // Ta gravité de la sandbox
        simulation = Simulation.Create(pool, new NarrowPhaseCallbacks(), new PoseIntegratorCallbacks(gravity), new SolveDescription(8, 1));



        // Le moule du mur (Largeur 2m, Hauteur 2m, Épaisseur 0.5m)
        Box formeMur = new Box(2.0f, 2.0f, 0.5f);
        formeMurIndex = simulation.Shapes.Add(formeMur);
        // Le visuel Raylib (Un cube qu'on pourra faire tourner)
        visuelMur = Raylib.LoadModelFromMesh(Raylib.GenMeshCube(2.0f, 2.0f, 0.5f));

        unsafe { visuelMur.Materials[0].Shader = lightShader; }



        // ========================================================
        // LE PONT RAYLIB -> BEPU (Création de la carte physique)
        // ========================================================

        

        // Le Joueur
        Capsule PlayerFrom = new Capsule(0.5f, 1f);
        Capsule PlayerFromAccroupi = new Capsule(0.5f, 0.5f);
        PlayerTicket = simulation.Shapes.Add(PlayerFrom);
        PlayerTicketAccroupi = simulation.Shapes.Add(PlayerFromAccroupi);

        BodyInertia PlayerInertie = new BodyInertia { InverseMass = 1f / 10f, InverseInertiaTensor = new BepuUtilities.Symmetric3x3() };


        int indexAleatoire = Raylib.GetRandomValue(0, listeSpawns.Count - 1);
        Vector3 pointDeDepart = listeSpawns[indexAleatoire];

        BodyDescription Playerdescription = BodyDescription.CreateDynamic(pointDeDepart, PlayerInertie, PlayerTicket, 0.01f);
        PlayerId = simulation.Bodies.Add(Playerdescription);

        // Cube Spawner
        Box Cube = new Box(1f, 1f, 1f);
        ticketCube = simulation.Shapes.Add(Cube);
        inertieCube = Cube.ComputeInertia(1f);

        // La forme du baril est maintenant définie plus tôt avec la bounding box
        formeBarilIndex = simulation.Shapes.Add(formeBaril);

        
        

        UpdateAllVolumes();

        ChargerSkin(); // On recharge le skin sauvegardé (couleur, chapeau, tête)

        Raylib.SetTargetFPS(FPS);
        X_carre = Raylib.GetScreenWidth()/2;
        Y_carre = Raylib.GetScreenHeight()/2;

        // ==========================================
        // MODE TEST AUTOMATIQUE (réservé au debug réseau)
        // Lancer avec la variable d'environnement GOOFY_AUTOHOST=1 pour
        // héberger un salon instantanément sans cliquer dans les menus.
        // Aucun impact en usage normal.
        // ==========================================
        bool modeTestAuto = Environment.GetEnvironmentVariable("GOOFY_AUTOHOST") == "1";
        // Réglages de match pour les tests auto (limite de temps / de score courtes)
        if (int.TryParse(Environment.GetEnvironmentVariable("GOOFY_MATCHTIME"), out int mt)) matchTimeLimitConfig = mt;
        if (int.TryParse(Environment.GetEnvironmentVariable("GOOFY_SCORELIMIT"), out int sl)) matchScoreLimitConfig = sl;
        // Mots de passe de test (Task 2.9) : GOOFY_ROOMPASS (hôte) / GOOFY_JOINPASS (client)
        hostRoomPassword = Environment.GetEnvironmentVariable("GOOFY_ROOMPASS") ?? "";
        joinPassword = Environment.GetEnvironmentVariable("GOOFY_JOINPASS") ?? "";
        if (modeTestAuto)
        {
            hostMatchName = "Salon de Test Auto";
            myLobbyName = "HoteTest";
            AllumerMoteurReseau(true);
            lock (_lobbyLock)
            {
                currentLobbyPlayers.Clear();
                currentLobbyPlayers.Add(new LobbyPlayer { Name = "HoteTest", IsHost = true, IsReady = true, Id = 0, Peer = null });
            }
            currentState = GameState.Lobby;
            // Si un serveur maître est configuré (GOOFY_MASTER_URL), on enregistre le salon
            // pour tester le chemin "code de salon" bout-en-bout.
            if (Environment.GetEnvironmentVariable("GOOFY_MASTER_URL") != null) HoteCreerSalon();
            Console.WriteLine("[TEST-AUTO] Salon hébergé automatiquement sur le port 7777");
        }

        // GOOFY_JOINCODE=XXXXX : rejoindre par CODE via le serveur maître (test du perçage NAT)
        string joinCodeTest = Environment.GetEnvironmentVariable("GOOFY_JOINCODE");
        if (!string.IsNullOrEmpty(joinCodeTest))
        {
            playerNameInput = "ClientCode";
            AllumerMoteurReseau(false);
            ClientRejoindreParCode(joinCodeTest);
            Console.WriteLine($"[TEST-AUTO] Tentative de connexion par code {joinCodeTest}.");
        }

        // GOOFY_AUTOJOIN=1 : l'inverse, on cherche un salon sur le réseau et on le rejoint tout seul
        bool modeTestJoin = Environment.GetEnvironmentVariable("GOOFY_AUTOJOIN") == "1";
        // GOOFY_JOINIP=127.0.0.1 : connexion DIRECTE (sans découverte broadcast) — indispensable
        // pour tester le hot-join, car le radar LAN est sourd pendant une partie (S6).
        string joinIpTest = Environment.GetEnvironmentVariable("GOOFY_JOINIP");
        bool testReadyEnvoye = false;
        if (modeTestJoin)
        {
            playerNameInput = "ClientTest";
            AllumerMoteurReseau(false);
            if (!string.IsNullOrEmpty(joinIpTest))
            {
                ConnecterAHote(new IPEndPoint(IPAddress.Parse(joinIpTest), NetConfig.GamePort));
                currentState = GameState.Lobby;
                Console.WriteLine($"[TEST-AUTO] Connexion directe à {joinIpTest} (test hot-join).");
            }
            else
            {
                currentState = GameState.ServerBrowser;
                Console.WriteLine("[TEST-AUTO] Mode client auto : recherche d'un salon...");
            }
        }

        // --- BOUCLE DE JEU ---
        while (!Raylib.WindowShouldClose())
    {

        // 1. LE COEUR DU RÉSEAU : On lit les paquets en attente !
        if (netManager != null)
        {
            netManager.PollEvents();
            netManager.NatPunchModule.PollEvents(); // événements de perçage NAT (Phase 2)

            // Annuaire + rendez-vous : heartbeat hôte / machine à états de perçage client
            float dtMaster = Raylib.GetFrameTime();
            TickMasterHote(dtMaster);
            TickMasterClient(dtMaster);

            // SÉCURITÉ S6 : les oreilles "non-connectées" (radar LAN) ne sont ouvertes
            // QUE dans les états où elles servent. Partout ailleurs (et surtout en
            // pleine partie exposée à internet), un datagramme non sollicité est sourd.
            bool radarHote = isServer && currentState == GameState.Lobby;
            bool radarClient = !isServer && currentState == GameState.ServerBrowser;
            netManager.BroadcastReceiveEnabled = radarHote;
            netManager.UnconnectedMessagesEnabled = radarHote || radarClient;
        }

        if (lancerPartieEnAttente)
        {
            lancerPartieEnAttente = false;
            mapChoisieIndex = mapIndexEnAttente;
            isOnline = true;
            ChargerMapReseau(mapChoisieIndex); // Le "crash 2" est corrigé : la map a maintenant sa physique !
            DemarrerHorlogeMatch(scoreLimitEnAttente, timeLimitEnAttente); // règles + horloge du match
            currentState = GameState.Playing;
        }

        // LE TICK DE SESSION (toasts + détection de fin de match + compte à rebours PostMatch)
        if (isOnline) TickSessionFrame(Raylib.GetFrameTime());

        // [TEST-AUTO] En mode autojoin : on se connecte au premier salon découvert
        if (modeTestJoin && currentState == GameState.ServerBrowser && serveursDisponibles.Count > 0)
        {
            var salonTest = serveursDisponibles.Values.First();
            Console.WriteLine($"[TEST-AUTO] Salon '{salonTest.NomDuSalon}' trouvé, connexion à {salonTest.EndPoint.Address}...");
            ConnecterAHote(new IPEndPoint(salonTest.EndPoint.Address, NetConfig.GamePort));
            lock (_lobbyLock) { currentLobbyPlayers.Clear(); }
            currentState = GameState.Lobby;
        }

        // [TEST-AUTO] En mode autojoin : on se met "PRÊT" dès que le serveur nous a donné un ID
        if (modeTestJoin && currentState == GameState.Lobby && myPlayerId > 0 && !testReadyEnvoye)
        {
            testReadyEnvoye = true;
            Packets.ToggleReadyPacket readyTest = new Packets.ToggleReadyPacket { IsReady = true };
            NetDataWriter writerReady = new NetDataWriter();
            netProcessor.Write(writerReady, readyTest);
            if (netManager.FirstPeer != null) netManager.FirstPeer.Send(writerReady, DeliveryMethod.ReliableOrdered);
            Console.WriteLine("[TEST-AUTO] PRÊT envoyé !");
        }

        // [TEST-AUTO] En mode autohost, on lance la partie dès qu'un client est prêt
        if (modeTestAuto && isServer && currentState == GameState.Lobby)
        {
            bool unClientPret;
            lock (_lobbyLock) { unClientPret = currentLobbyPlayers.Count >= 2 && currentLobbyPlayers.Where(p => !p.IsHost).All(p => p.IsReady); }
            if (unClientPret)
            {
                Console.WriteLine("[TEST-AUTO] Client prêt détecté : lancement de la partie !");
                HoteDemarrerMatch(); // utilise matchScoreLimitConfig / matchTimeLimitConfig
            }
        }

        // 2. LA LOGIQUE DU CLIENT : Crier périodiquement et Nettoyer
        if (netManager != null && !isServer && currentState == GameState.ServerBrowser)
        {
            float deltaTime = Raylib.GetFrameTime();
            chronoRecherche -= deltaTime;
            
            // A. Envoyer une onde radar toutes les 1.5 secondes (Broadcast)
            if (chronoRecherche <= 0)
            {
                NetDataWriter writer = new NetDataWriter();
                writer.Put("GOOFY_REQ");
                netManager.SendBroadcast(writer, NetConfig.GamePort);
                chronoRecherche = 1.5f; // Reset du timer
            }

            // B. CORRECTION : Nettoyer les serveurs fantômes (Qui ont fermé)
            List<IPEndPoint> serveursMorts = new List<IPEndPoint>();
            foreach (var keyValuePair in serveursDisponibles)
            {
                // On incrémente le temps écoulé depuis la dernière frame !
                keyValuePair.Value.TempsDepuisDerniereReponse += deltaTime;
                if (keyValuePair.Value.TempsDepuisDerniereReponse > 3.0f) 
                {
                    serveursMorts.Add(keyValuePair.Key);
                }
            }
            // Purge effective du dictionnaire
            foreach (var mort in serveursMorts) serveursDisponibles.Remove(mort);
        }




        // Si on est dans n'importe quel menu...
        if (currentState != GameState.Playing)
        {
            Draw(); // On dessine le menu et ses boutons
        }
        else
        {
            // C'EST ICI QU'ON JOUE
            // (Ton code actuel : Raylib.BeginDrawing(), camera, Jeu.BouclePrincipale, etc...)
            BouclePrincipale();
        }
    }

        // --- NETTOYAGE ---
        foreach(var texture in ListeTexture) Raylib.UnloadTexture(texture);
        Raylib.UnloadModel(mapModel);
        Raylib.UnloadModel(ennemiModel);
        Raylib.UnloadModel(sniper);
        Raylib.UnloadModel(karambit);
        Raylib.UnloadModel(bazooka);
        Raylib.UnloadModel(sword);
        Raylib.UnloadModel(shotgun);
        Raylib.UnloadModel(pistol);
        Raylib.UnloadModel(revolver);
        Raylib.UnloadModel(barrelModel);
        Raylib.UnloadShader(lightShader);
        Raylib.UnloadSound(snipershot);
        Raylib.UnloadSound(karambitshot);
        Raylib.UnloadSound(hitSound);
        Raylib.UnloadSound(weaponSwitchSound);
        Raylib.UnloadSound(reloadSound);
        Raylib.UnloadSound(noAmmoSound);
        Raylib.UnloadSound(groundImpactSound);

        Raylib.UnloadSound(windSound);
        Raylib.UnloadSound(heartbeatSound);
        
        Raylib.UnloadMusicStream(menuMusic);
        Raylib.UnloadMusicStream(gameMusic);
        
        // Unload death sounds
        for (int i = 0; i < deathSounds.Length; i++)
        {
            Raylib.UnloadSound(deathSounds[i]);
        }
        
        simulation.Dispose();
        pool.Clear();
        Raylib.CloseWindow();
    }

    static void SetActiveMusic(ActiveMusic desired)
    {
        if (currentMusicState == desired) return;

        if (currentMusicState == ActiveMusic.Menu)
        {
            Raylib.StopMusicStream(menuMusic);
        }
        else if (currentMusicState == ActiveMusic.Game)
        {
            Raylib.StopMusicStream(gameMusic);
        }

        currentMusicState = desired;

        if (currentMusicState == ActiveMusic.Menu)
        {
            Raylib.SetMusicVolume(menuMusic, Settings.MasterVolume * Settings.MusicVolume);
            Raylib.PlayMusicStream(menuMusic);
        }
        else if (currentMusicState == ActiveMusic.Game)
        {
            Raylib.SetMusicVolume(gameMusic, Settings.MasterVolume * Settings.MusicVolume);
            Raylib.PlayMusicStream(gameMusic);
        }
    }

    static void UpdateActiveMusicStream()
    {
        if (currentMusicState == ActiveMusic.Menu)
        {
            Raylib.UpdateMusicStream(menuMusic);
        }
        else if (currentMusicState == ActiveMusic.Game)
        {
            Raylib.UpdateMusicStream(gameMusic);
        }
    }
    static void UpdateMusicVolume()
        {
            // On ne multiplie plus par le Master, Raylib le fait tout seul
            if (currentMusicState == ActiveMusic.Menu) Raylib.SetMusicVolume(menuMusic, Settings.MusicVolume);
            else if (currentMusicState == ActiveMusic.Game) Raylib.SetMusicVolume(gameMusic, Settings.MusicVolume);
        }

    // NOUVEAU : On ajoute 'float volumeMultiplier = 1f' (par défaut, il vaut 1, donc il ne change rien pour les autres sons)
    public static void PlaySoundWithPriority(Sound sound, SoundPriority priority, float volumeMultiplier = 1f)
    {
        // 1. On part du volume choisi par le joueur, et on le multiplie par ton coefficient "brut" !
        float volumeFinal = Settings.SFXVolume * volumeMultiplier;

        // 2. EFFET DUCKING : Si ce n'est PAS un son critique, il est assourdi par les explosions
        if (priority != SoundPriority.Critical)
        {
            volumeFinal *= duckingStrength;
        }

        // On baisse un peu les sons de priorité "Low" pour équilibrer le mixage global
        if (priority == SoundPriority.Low)
        {
            volumeFinal *= 0.6f; 
        }

        // 3. On applique les réglages à ce son précis
        Raylib.SetSoundVolume(sound, volumeFinal);
        Raylib.SetSoundPitch(sound, 1f);

        // 4. ON JOUE TOUT INSTANTANÉMENT !
        Raylib.PlaySound(sound);
    }



    // ==========================================
    // NOUVEAU : LE MOTEUR DE SON 3D SPATIALISÉ
    // ==========================================
    public static void PlaySound3D(Sound sound, Vector3 sourcePosition, float maxDistance)
    {
        // 1. Calcul de la distance entre la caméra (tes oreilles) et le son
        float dist = Vector3.Distance(camera.Position, sourcePosition);
        
        // 2. Si on est trop loin, on n'entend rien du tout !
        if (dist > maxDistance) return;

        // 3. Calcul du volume (plus on est loin, plus ça tend vers 0)
        float distanceVolume = 1f - (dist / maxDistance);
        
        // On applique le volume (en prenant en compte les paramètres du joueur ET le ducking d'explosion)
        Raylib.SetSoundVolume(sound, distanceVolume * Settings.SFXVolume * duckingStrength);
        
        // 4. L'effet de distance (L'air absorbe les aigus, donc on baisse légèrement le pitch)
        float pitch = 1f - ((dist / maxDistance) * 0.3f); 
        Raylib.SetSoundPitch(sound, pitch);

        // 5. On joue le son !
        Raylib.PlaySound(sound);
    }


    public static void UpdateAllVolumes()
    {
        // 1. LE MASTER VOLUME : Raylib s'occupe du coefficient multiplicateur pour nous !
        Raylib.SetMasterVolume(Settings.MasterVolume);

        // 2. LA MUSIQUE (Plus besoin de multiplier par le Master ici)
        if (currentMusicState == ActiveMusic.Menu) Raylib.SetMusicVolume(menuMusic, Settings.MusicVolume);
        else if (currentMusicState == ActiveMusic.Game) Raylib.SetMusicVolume(gameMusic, Settings.MusicVolume);

        // 3. LES SFX (On définit le volume de TOUTES les variables Sound du jeu)
        // -> Les Armes
        Raylib.SetSoundVolume(snipershot, Settings.SFXVolume);
        Raylib.SetSoundVolume(karambitshot, Settings.SFXVolume);
        Raylib.SetSoundVolume(bazookashot, Settings.SFXVolume);
        Raylib.SetSoundVolume(shotgunshot, Settings.SFXVolume);
        Raylib.SetSoundVolume(pistolshot, Settings.SFXVolume);
        Raylib.SetSoundVolume(revolvershot, Settings.SFXVolume);
        Raylib.SetSoundVolume(swordslash, Settings.SFXVolume);
        
        // -> Les Effets, UI et Mouvements
        Raylib.SetSoundVolume(explosion, Settings.SFXVolume);
        Raylib.SetSoundVolume(select, Settings.SFXVolume);
        Raylib.SetSoundVolume(unselect, Settings.SFXVolume);
        Raylib.SetSoundVolume(survole, Settings.SFXVolume);
        Raylib.SetSoundVolume(hitSound, Settings.SFXVolume);
        Raylib.SetSoundVolume(hitmarkerSound, Settings.SFXVolume);
        Raylib.SetSoundVolume(killSound, Settings.SFXVolume);
        Raylib.SetSoundVolume(dashNotifSound, Settings.SFXVolume);
        Raylib.SetSoundVolume(wallNotifSound, Settings.SFXVolume);
        Raylib.SetSoundVolume(weaponSwitchSound, Settings.SFXVolume);
        Raylib.SetSoundVolume(reloadSound, Settings.SFXVolume);
        Raylib.SetSoundVolume(noAmmoSound, Settings.SFXVolume);
        Raylib.SetSoundVolume(groundImpactSound, Settings.SFXVolume);
        Raylib.SetSoundVolume(swoosh, Settings.SFXVolume); // Même les swoosh sont pris en compte !
        Raylib.SetSoundVolume(wallSound, Settings.SFXVolume);
        for (int i = 0; i < 3; i++)
        {
            Raylib.SetSoundVolume(footstepLeft[i], Settings.SFXVolume * 0.5f);
            Raylib.SetSoundVolume(footstepRight[i], Settings.SFXVolume * 0.5f);
        }

        // -> Le pool de sons de mort
        for (int i = 0; i < deathSounds.Length; i++)
        {
            if (deathSounds[i].Stream.Buffer != null) 
                Raylib.SetSoundVolume(deathSounds[i], Settings.SFXVolume);
        }

        // -> Les cris des ennemis déjà présents sur la carte
        foreach (Enemy enemy in enemiesList)
        {
            Raylib.SetSoundVolume(enemy.attackSound, Settings.SFXVolume);
        }
    }


    // ==========================================
    // MATHÉMATIQUES : HITBOX VISUELLE VIRTUELLE 
    // ==========================================
    public static bool RayIntersectsSphere(Vector3 rayOrigin, Vector3 rayDir, Vector3 sphereCenter, float sphereRadius, out float hitDistance)
    {
        hitDistance = 0f;
        Vector3 oc = rayOrigin - sphereCenter;
        float a = Vector3.Dot(rayDir, rayDir); 
        float b = 2.0f * Vector3.Dot(oc, rayDir);
        float c = Vector3.Dot(oc, oc) - (sphereRadius * sphereRadius);
        float discriminant = (b * b) - (4 * a * c);

        if (discriminant < 0) return false; // La balle est passée à côté !

        // On calcule le premier point d'entrée de la balle dans la sphère
        float numerator = -b - (float)Math.Sqrt(discriminant);
        if (numerator > 0)
        {
            hitDistance = numerator / (2.0f * a);
            return true;
        }
        return false;
    }



}