using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;

partial class Program
{
    //==== VARIABLES ====
    static int FPS = 60;
    static int HauteurFenetre = 1920;
    static int LargeurFenetre = 1080;

    static int X_carre;
    static int Y_carre;

    static string endroit = "menu";

    // Variable pour gérer l'état du menu en jeu (pause)
    static bool isMenuGameOpen = false;

    //pour avoir acces aux textures partout dans le code
    //fonctionne en 2 temps, ici elles sont reconnu pour tous, ensuite elles sont chargés
    static List<Texture2D> ListeTexture = new List<Texture2D>();
    
    // ===== images =====
    static Texture2D Logo;
    static Texture2D startimg; // juste en dessous les variables pour afficher l'image de start
    static float tempsAffichage = 3.0f; // ici
    static float opaciteImage = 255.0f; // et ici

    //clic
    static Texture2D clic1;
    static Texture2D clic2;
    static Texture2D clic3;
    static Texture2D sniperaim;

    //boutons
    static Texture2D play_button;
    static Texture2D play_active;
    static Texture2D option_button;
    static Texture2D option_active;
    static Texture2D quit_button;
    static Texture2D quit_active;

    static Texture2D background;

    // modeles 3d
    static Model mapModel;
    static Model sniper;

    // sons
    static Sound snipershot;
    static Sound select;
    static Sound unselect;
    static Sound survole;
    static Sound swoosh;


    static Shader lightShader;
    

    // la liste des clic en cours sur le menu
    static List<EffetClic> ListeEffets = new List<EffetClic>();

    // --- 2. CONFIGURATION DE LA CAMÉRA (LE JOUEUR) ---
    static Camera3D camera;
    static Vector3 playerSize = new Vector3(1.0f, 2.0f, 1.0f);

    // --- 3. VARIABLES DE PHYSIQUE (SAUT ET GRAVITÉ) ---
    static bool IsDashing = false; // il faudra ajouter un chrono de cb de temps le dash dur et faire avancer la personne extremeent plus vite pednant ce court laps de temps
    static int CountDash = 0;
    static int NbJump = 2;
    static float gravity = 25.0f;
    static float jumpStrength = 10.0f;
    static float velocityY = 0.0f;     // Vitesse verticale
    static bool isGrounded = true;     // Touche le sol ?

    // --- 4. CHARGEMENT DES ASSETS (MAP ET SHADERS) ---
    static Vector3 mapPosition = new Vector3(0.0f, 0.0f, 0.0f);
    static float mapScale = 1f;
    static int lightPosLoc;
    static Vector3 lightPosition = new Vector3(0.0f, 10.0f, 0.0f);

    // --- 5. GÉNÉRATION AUTOMATIQUE DES MURS (AUTO-BOUNDING BOX) ---
    static List<BoundingBox> walls = new List<BoundingBox>();


    static void Main()
    {

        // 1. Initialisation de la fenêtre (Largeur, Hauteur, Titre)
        //Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);

        Raylib.InitWindow(HauteurFenetre, LargeurFenetre, "GoofyFPS - Moteur de test");

        // ==== IMAGES ====

        Logo = Raylib.LoadTexture("src\\GoofyFPS.png");
        clic1 = Raylib.LoadTexture("src\\img\\clic-blanc.png");
        clic2 = Raylib.LoadTexture("src\\img\\clic2-blanc.png");
        clic3 = Raylib.LoadTexture("src\\img\\clic3-blanc.png");
        startimg = Raylib.LoadTexture("assets\\2D\\epsteintrump.png");

        play_active = Raylib.LoadTexture("src\\boutons\\play-active.png");
        play_button = Raylib.LoadTexture("src\\boutons\\play-base.png");
        option_button = Raylib.LoadTexture("src\\boutons\\option-base.png");
        option_active = Raylib.LoadTexture("src\\boutons\\option-active.png");
        quit_active = Raylib.LoadTexture("src\\boutons\\quit-active.png");
        quit_button = Raylib.LoadTexture("src\\boutons\\quit-base.png");

        background = Raylib.LoadTexture("src\\Background3.png");

        mapModel = Raylib.LoadModel("map.glb");
        sniper = Raylib.LoadModel("assets\\3D\\sniper.glb");
        sniperaim = Raylib.LoadTexture("assets\\2D\\sniperaim.png");

        Raylib.InitAudioDevice();
        snipershot = Raylib.LoadSound("assets\\sounds\\sniper_shot.wav");

        select = Raylib.LoadSound("assets\\sounds\\select.mp3");
        unselect = Raylib.LoadSound("assets\\sounds\\unselect.mp3");
        survole = Raylib.LoadSound("assets\\sounds\\survole.mp3");
        swoosh = Raylib.LoadSound("assets\\sounds\\swoosh.mp3");


        // Nécessite lighting.vs et lighting.fs
        lightShader = Raylib.LoadShader("lighting.vs", "lighting.fs");

        ListeTexture.Add(Logo); // on ajoute a la list afin de pouvoir unload facilement
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
        

        // ==== CONFIGURATION DE LA SCÈNE 3D ====
        camera = new Camera3D();
        camera.Position = new Vector3(0.0f, 15.0f, 5.0f); 
        camera.Target = new Vector3(0.0f, 2.0f, 0.0f);   
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);       
        camera.FovY = 60.0f;                                          
        camera.Projection = CameraProjection.Perspective;

        lightPosLoc = Raylib.GetShaderLocation(lightShader, "lightPos");

        // Appliquer le shader aux matériaux de la map
        // Note: map.glb peut avoir plusieurs matériaux, il faudrait idéalement boucler dessus.
        // On le garde sur le premier pour le moment selon ton code original.
        unsafe
        {
            if (mapModel.MaterialCount > 0)
            {
                mapModel.Materials[0].Shader = lightShader;
            }
        }

        Console.WriteLine($"[DEBUG] Nombre de Mesh détectés dans map.glb pour les collisions : {mapModel.MeshCount}");

        unsafe 
        {
            // Cette boucle parcourt chaque partie (Mesh) de ton fichier map.glb
            for (int i = 0; i < mapModel.MeshCount; i++)
            {
                // Récupère la boîte locale pour la partie 'i'
                BoundingBox wallBox = Raylib.GetMeshBoundingBox(mapModel.Meshes[i]);
                
                // 1. On applique l'échelle (Scale)
                wallBox.Min *= mapScale;
                wallBox.Max *= mapScale;

                // 2. On applique la position dans le monde (Translation)
                wallBox.Min += mapPosition;
                wallBox.Max += mapPosition;

                walls.Add(wallBox);
            }
        }

        // On bloque le jeu à 60 FPS pour éviter que la boucle tourne trop vite
        Raylib.SetTargetFPS(FPS); 

        X_carre = Raylib.GetScreenWidth()/2;
        Y_carre = Raylib.GetScreenHeight()/2;

        // 2. La Boucle de Jeu (Game Loop)
        // Elle tourne en continu tant qu'on n'appuie pas sur Echap ou la croix rouge
        while (!Raylib.WindowShouldClose())
        {
            // --- LOGIQUE (Update) ---
            // C'est ici qu'on mettra les calculs de déplacement plus tard
            if (endroit == "menu") {
                Menu();
            }
            else if (endroit == "boucle")
            {
                BouclePrincipale();
            }
            else if (endroit == "option")
            {
                Menugame();
            }
            
        }

        // 3. Fermeture propre pour libérer la mémoire
        foreach(var texture in ListeTexture)
        {
            Raylib.UnloadTexture(texture);
        }
        
        // on décharge la 3d
        Raylib.UnloadModel(mapModel);
        Raylib.UnloadModel(sniper);
        Raylib.UnloadShader(lightShader);
        Raylib.UnloadSound(snipershot);

        Raylib.CloseWindow();
    }
}