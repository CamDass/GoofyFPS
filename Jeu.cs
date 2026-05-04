using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;
using System.Linq;

// Moteur Physique
using BepuPhysics;
using BepuPhysics.Collidables;

partial class Program
{   
    // ========================================================
    // VARIABLES DES ARMES (ILIAN)
    // ========================================================
    static Weapon sniperrifle = new Weapon("Sniper", 100, 100, 1.0f, 5, 3, sniper, snipershot);
    static Weapon karambitknife = new Weapon("Karambit", 75, 3, 0.4f, 1, 0, karambit, karambitshot);

    static List<Weapon> weapons = new List<Weapon> { sniperrifle, karambitknife };
    static Weapon currentWeapon = karambitknife;
    static float laserTimer = 0.0f;
    static Vector3 laserStart = new Vector3();
    static Vector3 laserEnd = new Vector3();
    static float recoilAngle = 0.0f;

    static bool debugInfo = false;

    //===== BOUCLE DU JEU =====
    public static void BouclePrincipale()
    {
        // ========================================================
        // [ZONE ILIAN] 1. GESTION DES MENUS ET TIMERS
        // ========================================================
        if (Raylib.IsKeyPressed(KeyboardKey.LeftAlt))
        {
            isMenuGameOpen = !isMenuGameOpen; 
            if (isMenuGameOpen) { Raylib.EnableCursor(); Raylib.PlaySound(unselect); }
            else { Raylib.DisableCursor(); Raylib.PlaySound(select); }
        }

        if (isMenuGameOpen) { Menugame(); return; }

        if (Raylib.IsKeyPressed(KeyboardKey.G))
        {
            Random rand = new Random();
            currentWeapon = weapons[rand.Next(weapons.Count)];
        }

        if (Raylib.IsKeyDown(KeyboardKey.Tab))
        {
            Raylib.EnableCursor();
            Raylib.PlaySound(unselect);
            endroit = "menu";
        }

        float deltaTime = Raylib.GetFrameTime(); 
        laserTimer -= deltaTime;
        if (laserTimer < 0) laserTimer = 0;

        if (recoilAngle < 0) recoilAngle += deltaTime * 30.0f;
        if (recoilAngle > 0) recoilAngle = 0;


        // ========================================================
        // [ZONE BEPU] 2. PHYSIQUE ET MOUVEMENTS (TON CODE COMPLET)
        // ========================================================
        float hauteurVoulue = 0.8f; 
        bool IsWallRunning = false;

        BodyReference espionCube = simulation.Bodies.GetBodyReference(PlayerId);
        espionCube.Awake = true;
        Vector3 posCube = espionCube.Pose.Position; 

        GroundSensor capteurSol = new GroundSensor(espionCube.CollidableReference);
        Vector3 directionLaser = new Vector3(0, -1f, 0);
        float longueurLaser = 0.6f + 0.5f;
        simulation.RayCast(posCube, directionLaser, longueurLaser, ref capteurSol);

        GroundSensor capteurGlissade = new GroundSensor(espionCube.CollidableReference);
        float longueurLaserGlissade = 1.2f;
        simulation.RayCast(posCube, directionLaser, longueurLaserGlissade, ref capteurGlissade);

        if (capteurSol.toucheSol) NbJump = NbJumpMax + 1;

        // LE CERVEAU DE LA CAMÉRA FPS
        Vector2 mouseDelta = Raylib.GetMouseDelta();
        CameraYaw -= mouseDelta.X * MouseSensi;
        CameraPitch -= mouseDelta.Y * MouseSensi;

        float PitchLimit = 1.55f;
        if (CameraPitch > PitchLimit) CameraPitch = PitchLimit;
        if (CameraPitch < -PitchLimit) CameraPitch = -PitchLimit;

        Vector3 CamFroward = new Vector3(
            MathF.Cos(CameraPitch) * MathF.Sin(CameraYaw),
            MathF.Sin(CameraPitch),
            MathF.Cos(CameraPitch) * MathF.Cos(CameraYaw)
        );

        // LES JAMBES DU JOUEUR 
        Vector3 GroundForward = new Vector3(CamFroward.X, 0, CamFroward.Z);
        if (GroundForward.LengthSquared() > 0.0001f) GroundForward = Vector3.Normalize(GroundForward); 

        Vector3 GroundRight = Vector3.Cross(GroundForward, new Vector3(0, 1f, 0));
        GroundRight = Vector3.Normalize(GroundRight);
        Vector3 deplacementVoulu = Vector3.Zero; 

        GroundSensor capteurMurDroit = new GroundSensor(espionCube.CollidableReference);
        GroundSensor capteurMurGauche = new GroundSensor(espionCube.CollidableReference);
        float longueurLaserMur = 0.8f;
        simulation.RayCast(posCube, GroundRight, longueurLaserMur, ref capteurMurDroit);
        simulation.RayCast(posCube, -GroundRight, longueurLaserMur, ref capteurMurGauche);



        //vide
        if (Raylib.IsKeyPressed(KeyboardKey.P))
            {
                // 1. On modifie les coordonnées instantanément (ex: on le remet à 10m de haut au centre)
                espionCube.Pose.Position = new Vector3(0, 10f, 0); 
                
                // 2. On remet l'inertie et la vitesse à zéro pour un arrêt net !
                espionCube.Velocity.Linear = Vector3.Zero;  // Stop le déplacement
                espionCube.Velocity.Angular = Vector3.Zero; // Stop la rotation sur lui-même

            }

        if (espionCube.Pose.Position.Y < -50f)
        {
            // 1. On modifie les coordonnées instantanément (ex: on le remet à 10m de haut au centre)
                espionCube.Pose.Position = new Vector3(0, 10f, 0); 
                
                // 2. On remet l'inertie et la vitesse à zéro pour un arrêt net !
                espionCube.Velocity.Linear = Vector3.Zero;  // Stop le déplacement
                espionCube.Velocity.Angular = Vector3.Zero; // Stop la rotation sur lui-même

        }






        // Saut
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            if (capteurMurGauche.toucheSol || capteurMurDroit.toucheSol) espionCube.Velocity.Linear.Y = 5f;
            else if (NbJump > NbJumpMax)
            {
                NbJump--;
                if (espionCube.Velocity.Linear.Y > 0) { espionCube.Velocity.Linear.Y += 5f; } else { espionCube.Velocity.Linear.Y = 5f; }
            }
        }

        // Déplacements & WallRun
        if (!capteurSol.toucheSol)
        {
            if ((capteurMurDroit.toucheSol && Raylib.IsKeyDown(KeyboardKey.D)) || (capteurMurGauche.toucheSol && Raylib.IsKeyDown(KeyboardKey.A))) IsWallRunning = true; 
        }

        if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) deplacementVoulu += GroundForward;
        if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) deplacementVoulu -= GroundForward;
        if ((Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) && !capteurMurGauche.toucheSol) deplacementVoulu -= GroundRight; 
        if ((Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) && !capteurMurDroit.toucheSol) deplacementVoulu += GroundRight;

        if (deplacementVoulu.LengthSquared() > 0) deplacementVoulu = Vector3.Normalize(deplacementVoulu);

        bool IsSprinting = Raylib.IsKeyDown(KeyboardKey.LeftShift);
        float SpeedCoef = IsSprinting ? 1.7f : 1f;
        float vMax = 8f; 
        float fAcceleration = 0.2f; 
        float rollActuel = 0f;

        if (IsWallRunning)
        {
            NbJump = NbJumpMax + 1;
            fAcceleration = 0.1f;
            if (espionCube.Velocity.Linear.Y < 0) espionCube.Velocity.Linear.Y = -1f;
            if (Raylib.IsKeyDown(KeyboardKey.A)) rollActuel = 0.25f;
            else if (Raylib.IsKeyDown(KeyboardKey.D)) rollActuel = -0.25f;
        } 
        else { rollActuel = 0f; fAcceleration = 0.2f; }

        // Accroupir & Glissade
        float vitesseHorizontale = new Vector2(espionCube.Velocity.Linear.X, espionCube.Velocity.Linear.Z).Length();
        float vitesseVerticale = MathF.Abs(espionCube.Velocity.Linear.Y);
        float vitesseDescente = 0f;

        if (Raylib.IsKeyDown(KeyboardKey.C) && !Raylib.IsKeyDown(KeyboardKey.Space))
        {
            if (!capteurGlissade.toucheSol) 
            {
                vitesseDescente--;
                if (vitesseDescente < -20) vitesseDescente = -20; 
                espionCube.Velocity.Linear.Y += vitesseDescente;
            } 
            else 
            {
                hauteurVoulue = 0.5f;
                if (vitesseVerticale > 0) { espionCube.Velocity.Linear += GroundForward * (vitesseVerticale / 2); espionCube.Velocity.Linear.Y = 0.1f; }
                espionCube.SetShape(PlayerTicketAccroupi);
                if (vitesseHorizontale > 4) fAcceleration = 0.01f; else vMax = 3f;
            }
            if (Raylib.IsKeyPressed(KeyboardKey.C) && !Raylib.IsKeyDown(KeyboardKey.Space) && capteurSol.toucheSol && vitesseHorizontale > 4) espionCube.Velocity.Linear += GroundForward * 5;
        } 
        else 
        {
            espionCube.SetShape(PlayerTicket);
            hauteurVoulue = 0.8f; vMax = 8f; fAcceleration = 0.2f; vitesseDescente = 0;
        }

        Vector3 targetVelocity = deplacementVoulu * vMax * SpeedCoef;
        espionCube.Velocity.Linear.X += (targetVelocity.X - espionCube.Velocity.Linear.X) * fAcceleration;        
        espionCube.Velocity.Linear.Z += (targetVelocity.Z - espionCube.Velocity.Linear.Z) * fAcceleration;

        // Dash
        if (Raylib.IsKeyPressed(KeyboardKey.LeftControl)) espionCube.Velocity.Linear += GroundForward * 30;

        // Jump pad (Sandbox)
        if (posCube.X > 9 && posCube.X < 11 && posCube.Z > -1 && posCube.Z < 1 && capteurSol.toucheSol) espionCube.Velocity.Linear.Y += 20f;

        // On fait passer le temps
        simulation.Timestep(1f / 60f);

        // Application finale de la Caméra
        camera.Position = new Vector3(posCube.X, posCube.Y + hauteurVoulue, posCube.Z);
        camera.Up = Vector3.Transform(new Vector3(0, 1f, 0), Matrix4x4.CreateFromAxisAngle(CamFroward, rollActuel));
        camera.Target = camera.Position + CamFroward;



        if (Raylib.IsKeyPressed(KeyboardKey.F2)){
            if (debugInfo)
            {
                debugInfo = false;
            } else
            {
                debugInfo = true;
            }
        }





        // ========================================================
        // [ZONE MIXTE] 3. RENDU GRAPHIQUE (RAYLIB)
        // ========================================================
        Raylib.SetShaderValue(lightShader, lightPosLoc, lightPosition, ShaderUniformDataType.Vec3);

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.SkyBlue);

        Raylib.BeginMode3D(camera);
            
            // NOTE : On ne dessine pas mapModel car BEPU ne le calcule pas encore !
            // On dessine tes objets de test physique à la place :

            //sol 
            float taillePlatforme = 200f;
            Raylib.DrawCube(new Vector3(0,-taillePlatforme/2,0), taillePlatforme, taillePlatforme, taillePlatforme, Color.Gray);
            Raylib.DrawGrid((int)taillePlatforme, 1f); 


            //vide 
            int nbrCouche = 50;
            float espaceCouche = 1f;
            float hauteurVide = -30f;

            

            //COUCHES TRANSPARENTES (bas vers le haut)
            Color gazRouge = new Color(255, 50, 50, 8); 

            //boucle commence au fond (i = 50) et remonte jusqu'à la surface (i = 1)
            if (espionCube.Pose.Position.Y <= 0)
            {
                for (int i = nbrCouche; i > 0; i--)
                {
                    float hauteur = hauteurVide - (i * espaceCouche);
                    
                    // On dessine une plaque très fine (épaisseur 0.1f au lieu de 1f)
                    Raylib.DrawCube(new Vector3(0, hauteur, 0), 1000f, 0.1f, 1000f, gazRouge);
                }
            }
                

            
            //mur 
            Vector3 PosMur = new Vector3(-9.5f,2.5f,0);
            Vector3 PosMur2 = new Vector3(-9.5f,20f,0);
            Raylib.DrawCube(PosMur,1f,5f,20f,Color.Gray);
            Raylib.DrawCubeWires(PosMur,1f,5f,20f,Color.White);

            Raylib.DrawCube(PosMur2,1f,5f,20f,Color.Gray);
            Raylib.DrawCubeWires(PosMur2,1f,5f,20f,Color.White);

            Raylib.DrawCube(new Vector3(10f,-0.3f,0), 2f, 1f, 2f, Color.Blue); // Jump Pad


            Vector3 PosPlatforme1 = new Vector3(25,4,0);
            Vector3 PosPlatforme2 = new Vector3(22,10,-15);
            Vector3 PosPlatforme3 = new Vector3(10,18,-20);
            //Box Platforme = new Box(10f, 1f , 10f)
            Raylib.DrawCube(PosPlatforme1,10f, 1f , 10f,Color.Gray);
            Raylib.DrawCubeWires(PosPlatforme1,10f, 1f , 10f,Color.White);

            Raylib.DrawCube(PosPlatforme2,10f, 1f , 10f,Color.Gray);
            Raylib.DrawCubeWires(PosPlatforme2,10f, 1f , 10f,Color.White);

            Raylib.DrawCube(PosPlatforme3,10f, 1f , 10f,Color.Gray);
            Raylib.DrawCubeWires(PosPlatforme3,10f, 1f , 10f,Color.White);

            //dessiner le model du joueur
            Vector3 PointHaut = new Vector3(posCube.X, posCube.Y + 0.5f, posCube.Z);
            Vector3 PointBas = new Vector3(posCube.X, posCube.Y - 0.5f, posCube.Z);
            Raylib.DrawCapsule(PointHaut,PointBas,0.5f,8,8,Color.White); 




            // Dessiner les ennemis (Code ILIAN)
            foreach (Enemy enemy in enemies)
            {
                Raylib.DrawBoundingBox(enemy.GetBoundingBox(), Color.Blue);
                Raylib.DrawModel(enemyModel, enemy.position, 1.0f, Color.White);
            }
            
            // Dessiner le laser (Code ILIAN)
            if (laserTimer > 0)
            {
                byte alpha = (byte)(laserTimer * 255);
                Color laserColor = new Color((byte)255, (byte)0, (byte)0, alpha);
                Raylib.DrawLine3D(laserStart, laserEnd, laserColor);
                Raylib.DrawSphere(laserEnd, 0.2f, laserColor);
            }
            
        Raylib.EndMode3D();

        // ========================================================
        // [ZONE ILIAN] 4. HUD ET ARMES 2D
        // ========================================================
        Camera3D weaponCamera = new Camera3D();
        weaponCamera.Position = new Vector3(0,0,0);
        weaponCamera.Target = new Vector3(0, 0, 1);
        weaponCamera.Up = new Vector3(0, 1, 0);
        weaponCamera.FovY = 45.0f;
        weaponCamera.Projection = CameraProjection.Perspective;

        bool hasWeapon = true;   
        bool hasAmmo = currentWeapon.ammo > 0;    
        bool isAiming = Raylib.IsMouseButtonDown(MouseButton.Right) && hasWeapon;
        bool showweapon = !isAiming;
        Model actualWeapon = currentWeapon.modelname;
        Sound actualSound = currentWeapon.soundname;
        Vector2 positionViseurSniper = new Vector2(0,0);

        if (isAiming && currentWeapon == sniperrifle)
        {
            Raylib.DrawTextureEx(sniperaim, positionViseurSniper, 0, 1, Color.White);
            camera.FovY = 20.0f;
        }
        else camera.FovY = 60.0f;

        if (showweapon)
        {
            Raylib.BeginMode3D(weaponCamera);
                Vector3 weaponPos = new Vector3(0.5f, -0.4f, 1.2f);
                Raylib.DrawModelEx(actualWeapon, weaponPos, Vector3.UnitX, recoilAngle, Vector3.One, Color.White);
            Raylib.EndMode3D();
            Raylib.DrawCircle(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2, 3, Color.Green);
            
            string texteMunitions = $"Munitions: {currentWeapon.ammo}/{currentWeapon.maxammo}";
            int posX = Raylib.GetScreenWidth() - 400;
            int posY = Raylib.GetScreenHeight() - 100;
            Raylib.DrawText(texteMunitions, posX, posY, 30, Color.Black);
        }

        // ========================================================
        // [ZONE ILIAN] 5. LOGIQUE DES TIRS
        // ========================================================
        if (hasWeapon && hasAmmo && !currentWeapon.isReloading && Raylib.IsMouseButtonDown(MouseButton.Left) && ((float)Raylib.GetTime() - currentWeapon.lastShotTime >= currentWeapon.fireRate))
        {
            Raylib.PlaySound(actualSound);
            laserTimer = 1.0f;
            recoilAngle = -15.0f;
            currentWeapon.lastShotTime = (float)Raylib.GetTime();
            currentWeapon.ammo--;
            
            // LA SEULE MODIFICATION POUR LE ILIAN : On utilise le regard BEPU (CamFroward)
            Vector3 direction = CamFroward; 
            Vector3 right = Vector3.Normalize(Vector3.Cross(direction, camera.Up));
            laserStart = camera.Position + direction * 0.5f + right * 0.25f;
            laserEnd = laserStart + direction * currentWeapon.range;

            foreach (Enemy enemy in enemies.ToList())
            {
                float distanceToEnemy = Vector3.Distance(laserStart, enemy.position);
                if (distanceToEnemy <= currentWeapon.range)
                {
                    Vector3 toEnemy = Vector3.Normalize(enemy.position - laserStart);
                    float dot = Vector3.Dot(direction, toEnemy);
                    if (dot > 0.995) // Hitbox précise
                    {
                        enemy.health -= currentWeapon.damage;
                        if (enemy.health <= 0) enemies.Remove(enemy);
                    }
                }
            }
        }

        currentWeapon.Reload();

        if (debugInfo)
        {
            //infos 
            Raylib.DrawText("le moteur tourne.", 10,10,20, Color.DarkGreen);
            Raylib.DrawText($"Hauteur du cube : {posCube.Y:F2}", 10,40,20,Color.DarkGreen);
            if (NbJump >NbJumpMax)
            {
                Raylib.DrawText("Jump allowed", 10,80,20,Color.DarkGreen);
            }
            else
            {
                Raylib.DrawText("Jump not allowed", 10,80,20,Color.Red);
            }

            //wall jump 
            if (capteurMurDroit.toucheSol)
            {
                Raylib.DrawText("saut droit", 10,110,20,Color.DarkGreen);
            }
            else
            {
                Raylib.DrawText("saut droit",10,110,20,Color.Red);
            }
            if (capteurMurGauche.toucheSol)
            {
                Raylib.DrawText("saut gauche", 10,140,20,Color.DarkGreen);
            }
            else
            {
                Raylib.DrawText("saut gauche",10,140,20,Color.Red);
            }

            //sprint 
            if (IsSprinting)
            {
                Raylib.DrawText("sprint", 10,170,20,Color.DarkGreen);
            }
            else
            {
                Raylib.DrawText("sprint",10,170,20,Color.Red);
            }

            if (IsWallRunning)
            {
                Raylib.DrawText("WallRun", 10,200,20,Color.DarkGreen);
            }
            else
            {
                Raylib.DrawText("WallRun",10,200,20,Color.Red);
            }

            Raylib.DrawText($"Vitesse horizontale: {vitesseHorizontale:F2}", 10,240,20,Color.DarkGreen);
            Raylib.DrawText($"Vitesse verticale : {vitesseVerticale:F2}", 10,270,20,Color.DarkGreen);
            
            if (capteurGlissade.toucheSol) Raylib.DrawText("touche sol glissade", 10,300,20,Color.DarkGreen);
            else Raylib.DrawText("touche sol glissade", 10,300,20,Color.Red);

            if (laserTimer > 0) Raylib.DrawText($"Laser actif: {laserTimer:F2}", 10, 380, 20, Color.Red);

            // Vitesse Debug BEPU
            Raylib.DrawText($"Vitesse horizontale: {vitesseHorizontale:F2}", 10, 340, 20, Color.DarkGreen);
        }


        //wall run 
        if (capteurMurDroit.toucheSol)
        {
            Raylib.DrawRectangle(LargeurFenetre/2+50,HauteurFenetre/2-5,3,10,Color.White);
        }
        if (capteurMurGauche.toucheSol)
        {
            Raylib.DrawRectangle(LargeurFenetre/2-50,HauteurFenetre/2-5,3,10,Color.White);
        }

        //crosshair 
        Raylib.DrawCircle(LargeurFenetre/2,HauteurFenetre/2,3f,Color.White);



        Color missingLife = new Color(255, 50, 50, 40);
        float life = 80;
        int lifePixel = (int)(400*life/100);
        Raylib.DrawRectangle(100, HauteurFenetre - 150, 400,80,missingLife); //arriere plan pour si on enleve la vie on voit encore la barre
        if (life > 20)
        {
            Raylib.DrawRectangle(100, HauteurFenetre - 150, lifePixel,80,Color.White);
        } else
        {
            Raylib.DrawRectangle(100, HauteurFenetre - 150, lifePixel,80,Color.Red);
        }

        Raylib.DrawText($"{life}",100+10, HauteurFenetre - 150 + 25,50,Color.Black);
        

        
        Raylib.DrawFPS(LargeurFenetre-90,10);
        Raylib.EndDrawing();
    }
}