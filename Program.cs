using Raylib_cs;
using System.Numerics; // Très utile pour les vecteurs plus tard

class Program
{
    //==== VARIABLES ====
    static int FPS = 60;
    static int HauteurFenetre = 1920;
    static int LargeurFenetre = 1080;

    static int X_carre;
    static int Y_carre;

    static string endroit = "menu";

    //pour avoir acces aux textures partout dans le code
    //fonctionne en 2 temps, ici elles sont reconnu pour tous, ensuite elles sont chargés
    static List<Texture2D> ListeTexture = new List<Texture2D>();
    
    // ===== images =====
    static Texture2D Logo;

    //clic
    static Texture2D clic1;
    static Texture2D clic2;
    static Texture2D clic3;

    //boutons
    static Texture2D play_button;
    static Texture2D play_active;
    static Texture2D option_button;
    static Texture2D option_active;
    static Texture2D quit_button;
    static Texture2D quit_active;

    static Texture2D background;


    static Model mapModel;
    static Shader lightShader;
    

    // la liste des clic en cours sur le menu
    static List<EffetClic> ListeEffets = new List<EffetClic>();

    // --- 2. CONFIGURATION DE LA CAMÉRA (LE JOUEUR) ---
    static Camera3D camera;
    static Vector3 playerSize = new Vector3(1.0f, 2.0f, 1.0f);

    // --- 3. VARIABLES DE PHYSIQUE (SAUT ET GRAVITÉ) ---
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

        play_active = Raylib.LoadTexture("src\\boutons\\play-active.png");
        play_button = Raylib.LoadTexture("src\\boutons\\play-base.png");
        option_button = Raylib.LoadTexture("src\\boutons\\option-base.png");
        option_active = Raylib.LoadTexture("src\\boutons\\option-active.png");
        quit_active = Raylib.LoadTexture("src\\boutons\\quit-active.png");
        quit_button = Raylib.LoadTexture("src\\boutons\\quit-base.png");

        background = Raylib.LoadTexture("src\\Background3.png");

        // On charge l'unique fichier map.glb pour le visuel ET la physique
        mapModel = Raylib.LoadModel("map.glb");
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
            
        }

        // 3. Fermeture propre pour libérer la mémoire
        foreach(var texture in ListeTexture)
        {
            Raylib.UnloadTexture(texture);
        }
        
        // N'oublions pas de décharger la 3D
        Raylib.UnloadModel(mapModel);
        Raylib.UnloadShader(lightShader);

        Raylib.CloseWindow();
    }


    //===== BOUCLE DU JEU =====

    public static void BouclePrincipale()
    {
        // ==========================================
        // --- BOUCLE PRINCIPALE DU JEU ---
        // ==========================================
            
        if (Raylib.IsKeyDown(KeyboardKey.Tab)) 
        {
            Raylib.EnableCursor();
            endroit = "menu";
        }

        float deltaTime = Raylib.GetFrameTime(); 

        // --- A. GESTION DES MOUVEMENTS HORIZONTAUX (X & Z) ---
        Vector3 oldPosition = camera.Position;

        Raylib.UpdateCamera(ref camera, CameraMode.FirstPerson);
        Vector3 desiredMovement = camera.Position - oldPosition;
        
        // Tester chaque axe séparément pour les collisions
        camera.Position = oldPosition; 
        camera.Target -= desiredMovement;

        // Collision X
        camera.Position.X += desiredMovement.X;
        camera.Target.X += desiredMovement.X;
        BoundingBox playerBoxX = new BoundingBox(camera.Position - playerSize / 2, camera.Position + playerSize / 2);
        
        foreach (BoundingBox wall in walls)
        {
            if (Raylib.CheckCollisionBoxes(playerBoxX, wall))
            {
                camera.Position.X -= desiredMovement.X;
                camera.Target.X -= desiredMovement.X;
                break;
            }
        }

        // Collision Z
        camera.Position.Z += desiredMovement.Z;
        camera.Target.Z += desiredMovement.Z;
        BoundingBox playerBoxZ = new BoundingBox(camera.Position - playerSize / 2, camera.Position + playerSize / 2);
        
        foreach (BoundingBox wall in walls)
        {
            if (Raylib.CheckCollisionBoxes(playerBoxZ, wall))
            {
                camera.Position.Z -= desiredMovement.Z;
                camera.Target.Z -= desiredMovement.Z;
                break;
            }
        }

        // --- B. GESTION DES MOUVEMENTS VERTICAUX (AXE Y) ---
        if (isGrounded && Raylib.IsKeyPressed(KeyboardKey.Space))
            velocityY = jumpStrength;

        velocityY -= gravity * deltaTime;

        float moveY = velocityY * deltaTime;
        camera.Position.Y += moveY;
        camera.Target.Y += moveY;
        BoundingBox playerBoxY = new BoundingBox(camera.Position - playerSize / 2, camera.Position + playerSize / 2);
        isGrounded = false; 

        foreach (BoundingBox wall in walls)
        {
            if (Raylib.CheckCollisionBoxes(playerBoxY, wall))
            {
                if (velocityY < 0) 
                {
                    // Collision en tombant (atterrissage)
                    float correction = wall.Max.Y - (camera.Position.Y - playerSize.Y / 2);
                    camera.Position.Y += correction;
                    camera.Target.Y += correction;
                    velocityY = 0.0f;
                    isGrounded = true;
                }
                else if (velocityY > 0)
                {
                    // Collision en montant (plafond)
                    float correction = (camera.Position.Y + playerSize.Y / 2) - wall.Min.Y;
                    camera.Position.Y -= correction;
                    camera.Target.Y -= correction;
                    velocityY = 0.0f;
                }
                break; 
            }
        }

        // --- C. MISE À JOUR DE LA LUMIÈRE ---
        Raylib.SetShaderValue(lightShader, lightPosLoc, lightPosition, ShaderUniformDataType.Vec3);

        // --- D. RENDU GRAPHIQUE ---
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.SkyBlue);

        Raylib.BeginMode3D(camera);
            // On dessine la map (qui contient maintenant aussi les collisions)
            // On garde la couleur blanche pour que les textures du .glb s'affichent correctement
            Raylib.DrawModel(mapModel, mapPosition, mapScale, Color.White); 
            
            // Lignes de debug pour voir les boîtes
            foreach (BoundingBox wall in walls)
                Raylib.DrawBoundingBox(wall, Color.Red);
        Raylib.EndMode3D();

        // --- E. INTERFACE 2D (UI) ---
        Raylib.DrawCircle(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2, 3, Color.Green);
        Raylib.DrawFPS(10, 10);

        Raylib.EndDrawing();
    }


    // ===== BOUCLE MENU =====

    public static void Menu()
    {
        // --- RENDU (Draw) ---
            Raylib.BeginDrawing();

            // pour "quitter"
            if (Raylib.IsKeyDown(KeyboardKey.Space)) 
            {
                endroit = "boucle";
            }

            // pour gerer les clic 
            // --- ÉTAPE 3 : LA CRÉATION (Quand on clique) ---
            // On utilise Pressed (1 seule fois) et non Down (en continu)
            if (Raylib.IsMouseButtonPressed(MouseButton.Left)) 
            {
                int randValue = Raylib.GetRandomValue(1, 3);
                Texture2D textureChoisie = clic1; // Par défaut
                
                if (randValue == 2) textureChoisie = clic2;
                if (randValue == 3) textureChoisie = clic3;

                // calcule position
                float x_clic = textureChoisie.Width * 0.2f;
                float y_clic = textureChoisie.Height * 0.2f;
                Vector2 tailleImage = new Vector2(x_clic, y_clic);
                Vector2 positionCentree = Raylib.GetMousePosition() - (tailleImage / 2f);

                // ajouter à la liste le nouveau clic
                ListeEffets.Add(new EffetClic(positionCentree, textureChoisie));
            }


            // ===== Vecteurs =====

            Vector2 souris = Raylib.GetMousePosition();
            //float echelle_background = 0.2f;

            // background 
            Vector2 BackgroundPos = new Vector2(0,0);

            // === LOGO === (test vecteur)
            int posX_logo = Raylib.GetScreenHeight() / 2 + 200;
            int posY_logo = 100;

            Vector2 positionLogo = new Vector2(posX_logo, posY_logo);

            float rotation = 0.0f;
            float echelle = 0.2f; // 20% de la taille



             // === BOUTON MENU ===
            float echelleBoutons = 0.2f;
            float echelleBoutonActif = 0.205f;
            float echelleQuit = 0.190f;
            int posX_boutons = Raylib.GetScreenHeight()/2 + 250;
            
            int posY_play = 450;
            Vector2 positionPlay = new Vector2(posX_boutons, posY_play);

            int posY_option = 600;
            Vector2 positionOption = new Vector2(posX_boutons, posY_option);

            int posY_quit = 750;
            Vector2 positionQuit = new Vector2(posX_boutons, posY_quit);

            Vector2 ajustement = new Vector2(0,20);
            Vector2 ajustement_draw = new Vector2(5,5);
            Vector2 ajustement_quit = new Vector2(10,5);
            //collisions 
            Rectangle boxPlay = new Rectangle(positionPlay+ajustement, 300,120);
            Rectangle boxOption = new Rectangle(positionOption+ajustement, 300,120);
            Rectangle boxQuit = new Rectangle(positionQuit+ajustement, 300,120);



            // ==== debut du draw ====

            // On nettoie l'écran à chaque frame avec une couleur de fond
            Raylib.ClearBackground(Color.DarkGray);
            Raylib.DrawTextureEx(background, BackgroundPos, rotation, 1f,Color.White);

            //logo
            Raylib.DrawTextureEx(Logo, positionLogo, rotation, echelle, Color.White);
        

            //======= vrai boutons =======
            
            //play
            if (Raylib.CheckCollisionPointRec(souris, boxPlay))
            {
                Raylib.DrawTextureEx(play_active, positionPlay-ajustement_draw, rotation, echelleBoutonActif, Color.White);
                //Console.WriteLine("play");

                if (Raylib.IsMouseButtonReleased(MouseButton.Left))
                {
                    Raylib.DisableCursor();
                    endroit = "boucle";
                }
            }
            else 
            {
                Raylib.DrawTextureEx(play_button, positionPlay, rotation, echelleBoutons, Color.White);
            }



            //option
            if (Raylib.CheckCollisionPointRec(souris, boxOption))
            {
                Raylib.DrawTextureEx(option_active, positionOption-ajustement_draw, rotation, echelleBoutonActif, Color.White);
                //Console.WriteLine("option");

                if (Raylib.IsMouseButtonReleased(MouseButton.Left))
                {
                    Console.WriteLine("option");
                }
            }
            else 
            {
                Raylib.DrawTextureEx(option_button, positionOption, rotation, echelleBoutons, Color.White);
            }



            //quit
            if (Raylib.CheckCollisionPointRec(souris, boxQuit))
            {
                //bouton quitter activé
                Raylib.DrawTextureEx(quit_active, positionQuit+ajustement_quit, rotation, echelleQuit, Color.White);
                //Console.WriteLine("quit");

                if (Raylib.IsMouseButtonReleased(MouseButton.Left))
                {
                    //quitter le jeu
                    foreach(var texture in ListeTexture)
                    {
                        Raylib.UnloadTexture(texture);
                    }
                    
                    Raylib.CloseWindow();
                }
            }
            else 
            {
                Raylib.DrawTextureEx(quit_button, positionQuit, rotation, echelleBoutons, Color.White);
            }

            /*
            Raylib.DrawTextureEx(play_button, positionPlay, rotation, echelleBoutons, Color.White);
            Raylib.DrawTextureEx(option_button, positionOption, rotation, echelleBoutons, Color.White);
            Raylib.DrawTextureEx(quit_button, positionQuit, rotation, echelleBoutons, Color.White);


            
            //boutons place holders
            Raylib.DrawRectangle(LargeurFenetre/2 + 150, 500, 300, 100, Color.Gray);
            Raylib.DrawRectangle(LargeurFenetre/2 + 150, 650, 300, 100, Color.Gray);
            Raylib.DrawRectangle(LargeurFenetre/2 + 150, 800, 300, 100, Color.Gray);
            */

        
            // === AFFICHAGE CLIC ===

            for (int i = ListeEffets.Count - 1; i >= 0; i--)
            {
                // effet fondu
                ListeEffets[i].Opacite -= 5; 

                if (ListeEffets[i].Opacite <= 0)
                {
                    ListeEffets.RemoveAt(i);
                }
                else 
                {
                    //ici la couleur marche comme un masque sur photoshop : de blanc visible à noir invisible, donc on modifie l'alpha pour faire l'effet "opacité"
                    Color couleurFondu = new Color(255, 255, 255, ListeEffets[i].Opacite);
                    
                    Raylib.DrawTextureEx(ListeEffets[i].Texture, ListeEffets[i].Position, 0.0f, 0.2f, couleurFondu);
                }
            }


            Raylib.EndDrawing();
    }

}

public class EffetClic
{
    public Vector2 Position;
    public Texture2D Texture;
    public int Opacite;

    // Le Constructeur : ce qui se passe quand le clic naît
    public EffetClic(Vector2 pos, Texture2D tex)
    {
        Position = pos;
        Texture = tex;
        Opacite = 255; // 255 = Totalement opaque (visible)
    }
}