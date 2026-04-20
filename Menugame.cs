using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;


partial class Program
{
  
    public static void Menugame()
    {
        // Le menu reste ouvert jusqu'à ce qu'on l'explicite via les boutons ou autre logique
        // On ne vérifie pas LeftAlt ici pour éviter les conflits avec la logique d'ouverture

        // ===== Vecteurs =====
        Vector2 souris = Raylib.GetMousePosition();

        float rotation = 0.0f;

        // === BOUTON MENU PAUSE ===
        float echelleBoutons = 0.2f;
        float echelleBoutonActif = 0.205f;
        float echelleQuit = 0.190f;
        int posX_boutons = Raylib.GetScreenWidth() / 2 - 150; // Centre horizontal
        
        int posY_reprendre = 300; // Position verticale pour le bouton reprendre
        Vector2 positionReprendre = new Vector2(posX_boutons, posY_reprendre);

        int posY_option = 450; // Position verticale pour le bouton option
        Vector2 positionOption = new Vector2(posX_boutons, posY_option);

        int posY_quit = 600; // Position verticale pour le bouton quit
        Vector2 positionQuit = new Vector2(posX_boutons, posY_quit);

        Vector2 ajustement = new Vector2(0, 20);
        Vector2 ajustement_draw = new Vector2(5, 5);
        Vector2 ajustement_quit = new Vector2(10, 5);
        
        // Collisions 
        Rectangle boxReprendre = new Rectangle(positionReprendre + ajustement, 300, 120);
        Rectangle boxOption = new Rectangle(positionOption + ajustement, 300, 120);
        Rectangle boxQuit = new Rectangle(positionQuit + ajustement, 300, 120);

        // Le curseur est déjà activé dans Jeu.cs quand on ouvre le menu
        
        Raylib.BeginDrawing();

        

        // === BOUTON OPTION ===
        if (Raylib.CheckCollisionPointRec(souris, boxOption))
        {
            Raylib.DrawTextureEx(option_active, positionOption, rotation, echelleBoutonActif, Color.White);
            //Raylib.PlaySound(survole);

            if (Raylib.IsMouseButtonReleased(MouseButton.Left))
            {
                isMenuGameOpen = false; // Fermer le menu
                Raylib.DisableCursor();
                Raylib.PlaySound(select);
                return;
            }
        }
        else
        {
            Raylib.DrawTextureEx(option_button, positionOption, rotation, echelleBoutons, Color.White);
        }



        if (Raylib.CheckCollisionPointRec(souris, boxQuit))
        {
            //bouton quitter activé
            Raylib.DrawTextureEx(quit_active, positionQuit, rotation, echelleQuit, Color.White);
            

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



        
        Raylib.EndDrawing();
        

        







    }
}