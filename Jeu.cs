using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;
using System.Linq;

partial class Program
{   


    static Weapon sniperrifle = new Weapon("Sniper", 100, 100, 1.0f, 5, 3, sniper, snipershot);

    static Weapon karambitknife = new Weapon("Karambit", 75, 3, 0.4f, 1, 0, karambit, karambitshot);

    static List<Weapon> weapons = new List<Weapon> { sniperrifle, karambitknife };
    static Weapon currentWeapon = karambitknife;
    static float laserTimer = 0.0f;
    static Vector3 laserStart = new Vector3();
    static Vector3 laserEnd = new Vector3();
    static float recoilAngle = 0.0f;
    //===== BOUCLE DU JEU =====

    public static void BouclePrincipale()
    {
        // ==========================================
        // --- BOUCLE PRINCIPALE DU JEU ---
        // ==========================================

        /*
        static bool IsDashing = false;
        static int CountDash = 0;
        */

        if (Raylib.IsKeyPressed(KeyboardKey.LeftAlt))
        {
            isMenuGameOpen = !isMenuGameOpen; // Bascule l'état du menu
            if (isMenuGameOpen)
            {
                Raylib.EnableCursor();
                Raylib.PlaySound(unselect);
            }
            else
            {
                Raylib.DisableCursor();
                Raylib.PlaySound(select);
            }
        }

        // Si le menu en jeu est ouvert, on l'affiche et on bloque le jeu
        if (isMenuGameOpen)
        {
            Menugame();
            return; // Sort de la boucle principale pour ne pas mettre à jour le jeu
        }

        // Changement d'arme aléatoire avec la touche G
        if (Raylib.IsKeyPressed(KeyboardKey.G))
        {
            Random rand = new Random();
            int randomIndex = rand.Next(weapons.Count);
            currentWeapon = weapons[randomIndex];
        }

        
            
        if (Raylib.IsKeyDown(KeyboardKey.Tab))
        {
            Raylib.EnableCursor();
            Raylib.PlaySound(unselect);
            endroit = "menu";
        }

        int DashSpeed = 1;

        if (IsDashing) //si t'es en train de dash tu va bien plus vite
        {
            CountDash ++;
            DashSpeed = 8; //vitesse du dash
        }

        if (CountDash >= 8) // si on atteint x/60 fps on remet a 0
        {
            IsDashing = false;
            CountDash = 0;
        }
        
        if (CountDash == 1) // a la premiere frame ou il est activé 
        {
            Raylib.PlaySound(swoosh);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.LeftShift) && !IsDashing)
        {
            IsDashing = true;
        };

        float deltaTime = Raylib.GetFrameTime(); 

        // Mise à jour du timer du laser
        laserTimer -= deltaTime;
        if (laserTimer < 0) laserTimer = 0;

        // Mise à jour du recul de l'arme
        if (recoilAngle < 0) recoilAngle += deltaTime * 30.0f;
        if (recoilAngle > 0) recoilAngle = 0;

        // --- A. GESTION DES MOUVEMENTS HORIZONTAUX (X & Z) ---
        Vector3 oldPosition = camera.Position;

        Raylib.UpdateCamera(ref camera, CameraMode.FirstPerson);
        Vector3 desiredMovement = (camera.Position - oldPosition)*DashSpeed;
        
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
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            if (isGrounded || NbJump > 0)
            {
                NbJump -=1;
                velocityY = jumpStrength;
            }
            
        }
            

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
                    NbJump = 2;
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

            // Dessiner les ennemis
            foreach (Enemy enemy in enemies)
            {
                Raylib.DrawBoundingBox(enemy.GetBoundingBox(), Color.Blue);
                Raylib.DrawModel(enemyModel, enemy.position, 1.0f, Color.White);
            }
            // Dessiner le laser rouge pendant 1 seconde après le tir
            if (laserTimer > 0)
            {
                byte alpha = (byte)(laserTimer * 255);
                Color laserColor = new Color((byte)255, (byte)0, (byte)0, alpha);
                Raylib.DrawLine3D(laserStart, laserEnd, laserColor);
                Raylib.DrawSphere(laserEnd, 0.2f, laserColor);
            }
        Raylib.EndMode3D();

        Camera3D weaponCamera = new Camera3D();
        weaponCamera.Position = new Vector3(0,0,0);
        weaponCamera.Target = new Vector3(0, 0, 1);
        weaponCamera.Up = new Vector3(0, 1, 0);
        weaponCamera.FovY = 45.0f;
        weaponCamera.Projection = CameraProjection.Perspective;



        
        bool hasWeapon = true;   // à changer quand il y aura d'autres armes
        bool hasAmmo = currentWeapon.ammo > 0;    // vérifie les munitions
        bool isAiming = Raylib.IsMouseButtonDown(MouseButton.Right) && hasWeapon;
        bool showweapon = !isAiming;
        Model actualWeapon = currentWeapon.modelname;
        Sound actualSound = currentWeapon.soundname;
        Vector2 positionViseurSniper = new Vector2(0,0);

        if (isAiming)
        {
            Raylib.DrawTextureEx(sniperaim, positionViseurSniper, 0, 1, Color.White);
            camera.FovY = 20.0f;
        }
        else
        {
            camera.FovY = 60.0f;
        }

        if (showweapon)
        {
            Raylib.BeginMode3D(weaponCamera);
                Vector3 weaponPos = new Vector3(0.5f, -0.4f, 1.2f);
                Raylib.DrawModelEx(actualWeapon, weaponPos, Vector3.UnitX, recoilAngle, Vector3.One, Color.White);
            Raylib.EndMode3D();
            Raylib.DrawCircle(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2, 3, Color.Green);
            Raylib.DrawFPS(10, 10);
            string texteMunitions = $"Munitions: {currentWeapon.ammo}/{currentWeapon.maxammo}";
            int posX = Raylib.GetScreenWidth() - 400;
            int posY = Raylib.GetScreenHeight() - 450;
            Raylib.DrawText(texteMunitions, posX, posY, 30, Color.Gray);
        }

        if (hasWeapon && hasAmmo && !currentWeapon.isReloading && Raylib.IsMouseButtonDown(MouseButton.Left) && ((float)Raylib.GetTime() - currentWeapon.lastShotTime >= currentWeapon.fireRate))
        {
            Raylib.PlaySound(actualSound);
            laserTimer = 1.0f;
            recoilAngle = -15.0f;
            currentWeapon.lastShotTime = (float)Raylib.GetTime();
            currentWeapon.ammo--;
            Vector3 direction = Vector3.Normalize(camera.Target - camera.Position);
            Vector3 right = Vector3.Normalize(Vector3.Cross(direction, camera.Up));
            laserStart = camera.Position + direction * 0.5f + right * 0.25f;
            laserEnd = laserStart + direction * currentWeapon.range;

            // Calculer les dégâts sur les ennemis touchés
            foreach (Enemy enemy in enemies.ToList())
            {
                float distanceToEnemy = Vector3.Distance(laserStart, enemy.position);
                if (distanceToEnemy <= currentWeapon.range)
                {
                    Vector3 toEnemy = Vector3.Normalize(enemy.position - laserStart);
                    float dot = Vector3.Dot(direction, toEnemy);
                    if (dot > 0.99f)
                    {
                        enemy.health -= currentWeapon.damage;
                        if (enemy.health <= 0)
                        {
                            enemies.Remove(enemy);
                        }
                    }
                }
            }
        }

        // Gestion du rechargement
        currentWeapon.Reload();

        // Debug: afficher le timer du laser pour vérifier l'activation
        if (laserTimer > 0)
        {
            Raylib.DrawText($"Laser actif: {laserTimer:F2}", 10, 40, 20, Color.Red);
        }


        Raylib.EndDrawing();
    }
}
