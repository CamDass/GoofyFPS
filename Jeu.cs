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

        Raylib.BeginMode3D(weaponCamera);

            Vector3 weaponPos = new Vector3(0.4f, -0.4f, 1.2f);
            Raylib.DrawModel(sniper, weaponPos, 1.0f, Color.White);
        Raylib.EndMode3D();
        

        // --- E. INTERFACE 2D (UI) ---
        Raylib.DrawCircle(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2, 3, Color.Green);
        Raylib.DrawFPS(10, 10);

        Raylib.EndDrawing();
    }
}