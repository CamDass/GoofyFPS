using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;

partial class Program
{
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
            
        if (Raylib.IsKeyDown(KeyboardKey.Tab)) 
        {
            Raylib.EnableCursor();
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

        if (Raylib.IsKeyPressed(KeyboardKey.LeftShift) && !IsDashing)
        {
            IsDashing = true;
        }




        float deltaTime = Raylib.GetFrameTime(); 

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
        Raylib.EndMode3D();

        Camera3D weaponCamera = new Camera3D();
        weaponCamera.Position = new Vector3(0,0,0);
        weaponCamera.Target = new Vector3(0, 0, 1);
        weaponCamera.Up = new Vector3(0, 1, 0);
        weaponCamera.FovY = 45.0f;
        weaponCamera.Projection = CameraProjection.Perspective;




        bool hasWeapon = true;   // à changer quand il y aura d'autres armes
        bool hasAmmo = true;    // à changer quand il y aura le système de munitions
        bool isAiming = Raylib.IsMouseButtonDown(MouseButton.Right) && hasWeapon;
        bool showweapon = !isAiming;
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
                Raylib.DrawModel(sniper, weaponPos, 1.0f, Color.White);
            Raylib.EndMode3D();
            Raylib.DrawCircle(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2, 3, Color.Green);
            Raylib.DrawFPS(10, 10);
        }

        if (hasWeapon && hasAmmo && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Raylib.PlaySound(snipershot);
        }

        Raylib.EndDrawing();
    }
}
