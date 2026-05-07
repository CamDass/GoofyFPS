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
                Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
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

    public static void Menumaps()
    {
         Raylib.BeginDrawing();



            // pour "quitter"
            if (Raylib.IsKeyDown(KeyBinds.SelectMenu)) 
            {
                Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                Raylib.DisableCursor();

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

            float rotation = 0.0f;

            

             // === BOUTON MENU ===
            float echelleBoutons = 0.2f;
            float echelleBoutonActif = 0.205f;
            float echelleQuit = 0.190f;
            int posX_boutons = Raylib.GetScreenHeight()/2 + 250;

            int posY_quit = 750;
            Vector2 positionQuit = new Vector2(posX_boutons, posY_quit);

            Vector2 ajustement = new Vector2(0,20);
            Vector2 ajustement_draw = new Vector2(5,5);
            Vector2 ajustement_quit = new Vector2(10,5);
            //collisions 

            Rectangle boxQuit = new Rectangle(positionQuit+ajustement, 300,120);



            // ==== debut du draw ====

            // On nettoie l'écran à chaque frame avec une couleur de fond
            Raylib.ClearBackground(Color.DarkGray);
            Raylib.DrawTextureEx(background, BackgroundPos, rotation, 1f,Color.White);

            //======= vrai boutons =======


            //quit
            if (Raylib.CheckCollisionPointRec(souris, boxQuit))
            {
                //bouton quitter activé
                Raylib.DrawTextureEx(quit_active, positionQuit+ajustement_quit, rotation, echelleQuit, Color.White);
                //Raylib.PlaySound(survole);
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