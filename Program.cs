using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;
using System.Linq;

// Moteur Physique
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;

public partial class Program
{
    // ========================================================
    // VARIABLES DU JEU ET DES ARMES
    // ========================================================
 
    public static Player localPlayer = new Player(100);
    public static float hitmarkerTimer = 0f;

    public static List<Vector3> listeSpawns = new List<Vector3>
    {
        new Vector3(3, 0, -21),     
        new Vector3(-104, 21, 45),
        new Vector3(-107, 0, 155),    
        new Vector3(-8, 10, 96), 
        new Vector3(-39, 31, 85), 
    };


    static int FPS = 60;
    static int HauteurFenetre = 1080;
    static int LargeurFenetre = 1920;
    static int X_carre;
    static int Y_carre;
    static string endroit = "menu";
    static bool isMenuGameOpen = false;

    static List<Texture2D> ListeTexture = new List<Texture2D>();

    enum ActiveMusic
    {
        None,
        Menu,
        Game
    }
    
    // Images & HUD
    static Texture2D Logo, startimg, clic1, clic2, clic3, sniperaim, ImageMapTest, ImageMapVille, Imageville2;
    static Texture2D play_button, play_active, option_button, option_active, quit_button, quit_active, background, BlurBackground, imageexplosion;
    static float tempsAffichage = 3.0f; 
    static float opaciteImage = 255.0f; 
    static List<EffetClic> ListeEffets = new List<EffetClic>();

    // Modèles 3D et Sons
    static Model mapModel, sniper, karambit, bazooka, sword, shotgun, pistol, revolver, barrelModel;
    public static Sound[] deathSounds = new Sound[4]; // Pool de 4 sons de death pour éviter les conflits
    public static int deathSoundIndex = 0;
    static Sound snipershot, karambitshot, bazookashot, shotgunshot, pistolshot, revolvershot, swordslash, select, unselect, survole, swoosh, explosion;
    static Music menuMusic, gameMusic;
    static float musicVolume = 0.4f;
    static ActiveMusic currentMusicState = ActiveMusic.None;
    static Shader lightShader;
    static int lightPosLoc;
    static int lightColorLoc;
    static int viewPosLoc;
    public static int applyFogLoc;


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



    // ========================================================
    // GESTIONNAIRE DE SURVIE ET SPAWNS
    // ========================================================
    public static float survivalTime = 0f;
    public static float enemySpawnTimer = 0f;
    public static float timeBetweenSpawns = 3.0f; // 1 ennemi toutes les 3 secondes
    public static List<Vector3> enemySpawnPoints = new List<Vector3>();




    // ========================================================
    // INITIALISATION PRINCIPALE
    // ========================================================
    static void Main()
    {
        Raylib.InitWindow(LargeurFenetre, HauteurFenetre, "GoofyFPS");

        Image ico = Raylib.LoadImage("src\\GoofyFPS-small.png");
        Raylib.SetWindowIcon(ico);

        // --- 1. CHARGEMENT COLLÈGUE (Assets) ---
        Logo = Raylib.LoadTexture("src\\GoofyFPS.png");
        clic1 = Raylib.LoadTexture("src\\img\\clic-blanc.png");
        clic2 = Raylib.LoadTexture("src\\img\\clic2-blanc.png");
        clic3 = Raylib.LoadTexture("src\\img\\clic3-blanc.png");
        startimg = Raylib.LoadTexture("assets\\2D\\epsteintrump.png");
        imageexplosion = Raylib.LoadTexture("assets\\2D\\exploimage.png");

        play_active = Raylib.LoadTexture("src\\boutons\\play-active.png");
        play_button = Raylib.LoadTexture("src\\boutons\\play-base.png");
        option_button = Raylib.LoadTexture("src\\boutons\\option-base.png");
        option_active = Raylib.LoadTexture("src\\boutons\\option-active.png");
        quit_active = Raylib.LoadTexture("src\\boutons\\quit-active.png");
        quit_button = Raylib.LoadTexture("src\\boutons\\quit-base.png");

        background = Raylib.LoadTexture("src\\Background3.png");
        
        Image BlurBackgroundImg = Raylib.LoadImage("src\\Background3.png");
        Raylib.ImageBlurGaussian(ref BlurBackgroundImg, 10);
        BlurBackground = Raylib.LoadTextureFromImage(BlurBackgroundImg);
        Raylib.UnloadImage(BlurBackgroundImg);


        ImageMapTest = Raylib.LoadTexture("src\\testMap.png");
        ImageMapVille = Raylib.LoadTexture("src\\ville.png");
        Imageville2 = Raylib.LoadTexture("src\\ville2.png");


        ennemiModel = Raylib.LoadModel("assets\\3D\\ennemy.glb");
        sniper = Raylib.LoadModel("assets\\3D\\sniper.glb");
        sniperrifle.modelname = sniper;
        karambit = Raylib.LoadModel("assets\\3D\\karambit.glb");
        karambitknife.modelname = karambit;
        bazooka = Raylib.LoadModel("assets\\3D\\bazooka.glb");
        bazookaWeapon.modelname = bazooka; 
        sword = Raylib.LoadModel("assets\\3D\\sword.glb");
        swordWeapon.modelname = sword;
        shotgun = Raylib.LoadModel("assets\\3D\\shotgun.glb");
        shotgunWeapon.modelname = shotgun;
        pistol = Raylib.LoadModel("assets\\3D\\pistol.glb");
        pistolWeapon.modelname = pistol;
        revolver = Raylib.LoadModel("assets\\3D\\revolver.glb");
        revolverWeapon.modelname = revolver;
        barrelModel = Raylib.LoadModel("assets\\3D\\barril.glb");
        // Calculer la bounding box du modèle pour une hitbox précise et l'aligner sur le modèle
        BoundingBox barrelBB = Raylib.GetModelBoundingBox(barrelModel);
        Vector3 barrelSize = barrelBB.Max - barrelBB.Min;
        Vector3 barrelHalfExtents = barrelSize * 0.5f;
        barrelPhysicsCenterOffset = (barrelBB.Max + barrelBB.Min) * 0.5f;
        float barrelHitboxInflation = 2f; // Agrandit la box de détection des tirs
        Box formeBaril = new Box(barrelHalfExtents.X * barrelHitboxInflation, barrelHalfExtents.Y * barrelHitboxInflation, barrelHalfExtents.Z * barrelHitboxInflation);
        sniperaim = Raylib.LoadTexture("assets\\2D\\sniperaim.png");

        Raylib.InitAudioDevice();
        menuMusic = Raylib.LoadMusicStream("assets\\sounds\\menuMusic.mp3");
        gameMusic = Raylib.LoadMusicStream("assets\\sounds\\gameMusic.mp3");
        Raylib.SetMusicVolume(menuMusic, musicVolume);
        Raylib.SetMusicVolume(gameMusic, musicVolume);
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

        lightShader = Raylib.LoadShader("lighting.vs", "lighting.fs");
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

        /*
        // Sol
        float taillePlatforme = 200f;
        Box Sol = new Box(taillePlatforme, taillePlatforme, taillePlatforme);
        TypedIndex SolShapeIndex = simulation.Shapes.Add(Sol);
        simulation.Statics.Add(new StaticDescription(new Vector3(0, -taillePlatforme/2, 0), SolShapeIndex));

        // Murs de test
        Vector3 PosMur = new Vector3(-9.5f, 2.5f, 0);
        Vector3 PosMur2 = new Vector3(-9.5f, 20f, 0);
        Box Mur = new Box(1f, 5f, 20f);
        TypedIndex MurShapeIndex = simulation.Shapes.Add(Mur);
        simulation.Statics.Add(new StaticDescription(PosMur, MurShapeIndex));
        simulation.Statics.Add(new StaticDescription(PosMur2, MurShapeIndex));

        // Plateformes (de ta sandbox)
        Box Platforme = new Box(10f, 1f, 10f);
        TypedIndex PlatformeShapeIndex = simulation.Shapes.Add(Platforme);
        simulation.Statics.Add(new StaticDescription(new Vector3(25, 4, 0), PlatformeShapeIndex));
        simulation.Statics.Add(new StaticDescription(new Vector3(22, 10, -15), PlatformeShapeIndex));
        simulation.Statics.Add(new StaticDescription(new Vector3(10, 18, -20), PlatformeShapeIndex));
        */





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

        
        

        

        Raylib.SetTargetFPS(FPS); 
        X_carre = Raylib.GetScreenWidth()/2;
        Y_carre = Raylib.GetScreenHeight()/2;

        // --- BOUCLE DE JEU ---
        while (!Raylib.WindowShouldClose())
        {
            if (endroit == "menu") Menu();
            else if (endroit == "boucle") BouclePrincipale();
            else if (endroit == "option") {
                Console.WriteLine("option");
                endroit = "menu";
            }
            else if (endroit == "choice map") ChoiceMap();
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
            Raylib.SetMusicVolume(menuMusic, musicVolume);
            Raylib.PlayMusicStream(menuMusic);
        }
        else if (currentMusicState == ActiveMusic.Game)
        {
            Raylib.SetMusicVolume(gameMusic, musicVolume);
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
}