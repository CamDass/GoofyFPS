using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;

// Moteur Physique
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;

partial class Program
{
    // ===== BOUCLE MENU =====


    static float echelle = 0.2f; 
    static bool agrandissement = true;
    // 0 = Page Principale (Son/FOV), 1 = Page Raccourcis (Keybinds)
    public static int ongletOptionActif = 0;

    public static void Menu()
    {
        SetActiveMusic(ActiveMusic.Menu);
        UpdateActiveMusicStream();

        // --- RENDU (Draw) ---
            Raylib.BeginDrawing();

            // pour "quitter"
            if (Raylib.IsKeyDown(KeyboardKey.Space)) 
            {
                Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                endroit = "choice map";
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
            int posX_logo = Raylib.GetScreenHeight() - 140;
            int posY_logo = 250;

            Vector2 positionLogo = new Vector2(posX_logo, posY_logo);

            float rotation = 0.0f;

            

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
            if (agrandissement) 
            {
                echelle += 0.0002f;
                if (echelle >= 0.21f) agrandissement = false;
            } 
            else 
            {
                echelle -= 0.0002f;
                if (echelle <= 0.2f) agrandissement = true;
            }

            float largeuraffichee = Logo.Width * echelle;
            float hauteurAffichee = Logo.Height * echelle;

            Vector2 positionAjustee = new Vector2(posX_logo - (largeuraffichee / 2), posY_logo - (hauteurAffichee / 2));

            // 2. Affichage (une seule ligne suffit, en dehors des if/else)
            Raylib.DrawTextureEx(Logo, positionAjustee, rotation, echelle, Color.White);
        

            //======= vrai boutons =======
            
            //play
            if (Raylib.CheckCollisionPointRec(souris, boxPlay))
            {
                Raylib.DrawTextureEx(play_active, positionPlay-ajustement_draw, rotation, echelleBoutonActif, Color.White);
                //Raylib.PlaySound(survole);
                //Console.WriteLine("play");

                if (Raylib.IsMouseButtonReleased(MouseButton.Left))
                {
                    Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                    endroit = "choice map";
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
                //Raylib.PlaySound(survole);
                //Console.WriteLine("option");

                if (Raylib.IsMouseButtonReleased(MouseButton.Left))
                {
                    endroit = "option";
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













    public static void ChoiceMap()
    {
        SetActiveMusic(ActiveMusic.Menu);
        UpdateActiveMusicStream();

        simulation.Statics.Clear();

        foreach (Enemy enemy in enemiesList)
        {
            if (enemy.isAlive) simulation.Bodies.Remove(enemy.bodyId);
        }
        // On vide la liste mémoire
        enemiesList.Clear();

        Raylib.BeginDrawing();

        // ========================================================
        // 1. LE FOND AVEC EFFET "BLUR" (Verre dépoli)
        // ========================================================
        Vector2 BackgroundPos = new Vector2(0, 0);
        // On dessine le fond normal
        Raylib.DrawTextureEx(BlurBackground, BackgroundPos, 0.0f, 1f, Color.White);
        
        // L'astuce du flou : on dessine un rectangle blanc semi-transparent par-dessus (180/255 d'opacité)
        //Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(255, 255, 255, 180));

        // ========================================================
        // 2. GESTION DES CLICS (Système de particules du menu)
        // ========================================================
        if (Raylib.IsMouseButtonPressed(MouseButton.Left)) 
        {
            int randValue = Raylib.GetRandomValue(1, 3);
            Texture2D textureChoisie = clic1;
            if (randValue == 2) textureChoisie = clic2;
            if (randValue == 3) textureChoisie = clic3;
            float x_clic = textureChoisie.Width * 0.2f;
            float y_clic = textureChoisie.Height * 0.2f;
            Vector2 positionCentree = Raylib.GetMousePosition() - (new Vector2(x_clic, y_clic) / 2f);
            ListeEffets.Add(new EffetClic(positionCentree, textureChoisie));
        }

        // ========================================================
        // 3. TITRE
        // ========================================================
        string titre = "SÉLECTION DE LA CARTE";
        int tailleTitre = 50;
        int largeurTitre = Raylib.MeasureText(titre, tailleTitre);
        Raylib.DrawText(titre, (Raylib.GetScreenWidth() - largeurTitre) / 2, 200, tailleTitre, Color.Black);

        // ========================================================
        // 4. DONNÉES DES CARTES (En dur)
        // ========================================================
        // REMARQUE : Remplace "textureMap1", etc. par tes vraies variables de textures préchargées
        // Pour l'instant, je mets ton "Logo" comme placeholder pour que ça compile sans erreur.
        Texture2D[] texturesMap = { ImageMapTest, ImageMapVille, Imageville2 }; 
        string[] nomsMap = { "Tutoriel", "La Ville", "Arène de tir" };
        string[] destinationsMap = { "boucle", "boucle", "boucle" }; // Ce qu'on va mettre dans 'endroit'
        string[] ModelMapPath = {"test.glb","map.glb","map.glb"};

        // Paramètres de taille et d'espacement
        int largeurMap = 350;
        int hauteurMap = 220;
        int espacement = 80;
        int nbMaps = 3;

        // Calcul mathématique pour centrer le tout parfaitement
        int largeurTotale = (largeurMap * nbMaps) + (espacement * (nbMaps - 1));
        int startX = (Raylib.GetScreenWidth() - largeurTotale) / 2;
        int startY = (Raylib.GetScreenHeight() - hauteurMap) / 2;

        Vector2 souris = Raylib.GetMousePosition();

        // ========================================================
        // 5. AFFICHAGE DES CARTES
        // ========================================================
        for (int n = 0; n < nbMaps; n++)
        {
            // Position de cette carte
            int mapX = startX + n * (largeurMap + espacement);
            Rectangle mapBox = new Rectangle(mapX, startY, largeurMap, hauteurMap);
            
            bool isHovered = Raylib.CheckCollisionPointRec(souris, mapBox);

            // A. DESSIN DE L'IMAGE (DrawTexturePro permet de forcer la taille à 350x220)
            Rectangle sourceRec = new Rectangle(0, 0, texturesMap[n].Width, texturesMap[n].Height);
            Raylib.DrawTexturePro(texturesMap[n], sourceRec, mapBox, Vector2.Zero, 0f, Color.White);

            // B. EFFET DE SURVOL ET CLIC
            if (isHovered)
            {
                // Un contour rouge très épais pour montrer qu'on sélectionne
                Raylib.DrawRectangleLinesEx(mapBox, 6, Color.Red);
                
                // On assombrit un peu l'image pour l'effet "sélectionné"
                Raylib.DrawRectangleRec(mapBox, new Color(0, 0, 0, 50));


                if (Raylib.IsMouseButtonReleased(MouseButton.Left))
                {
                    Raylib.PlaySound(select);
                    Raylib.DisableCursor(); // Si on passe en mode FPS

                    mapModel = Raylib.LoadModel(ModelMapPath[n]);


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

                        var cartePhysiqueBEPU = new BepuPhysics.Collidables.Mesh(bepuTriangles, Vector3.One, pool);
                        TypedIndex ticketCarte = simulation.Shapes.Add(cartePhysiqueBEPU);
                        simulation.Statics.Add(new StaticDescription(mapPosition, ticketCarte));

                        Console.WriteLine($"[DEBUG] Map chargée avec {totalTriangles} triangles ! (Vérifie que ce chiffre n'est pas zéro)");
                    }






                    unsafe
                    {
                        for (int j = 0; j < mapModel.MaterialCount; j++)
                        {
                            mapModel.Materials[j].Shader = lightShader;
                        }
                    }

                    InitBarrels();
                    InitEnnemis();

                    endroit = destinationsMap[n]; // On lance la bonne map !

                }
            }
            else
            {
                // Un contour noir simple
                Raylib.DrawRectangleLinesEx(mapBox, 3, Color.Black);
            }

            // C. DESSIN DU TEXTE (Noir centré sur l'image)
            int tailleTexteMap = 30;
            int largeurTexteMap = Raylib.MeasureText(nomsMap[n], tailleTexteMap);
            int texteX = mapX + (largeurMap - largeurTexteMap) / 2;
            int texteY = startY + (hauteurMap - tailleTexteMap) / 2;

            // Astuce : On dessine un petit rectangle blanc semi-transparent juste derrière le texte
            // pour être sûr à 100% qu'il sera lisible en noir, peu importe l'image de fond !
            Raylib.DrawRectangle(texteX - 10, texteY - 5, largeurTexteMap + 20, tailleTexteMap + 10, new Color(255, 255, 255, 180));
            
            Raylib.DrawText(nomsMap[n], texteX, texteY, tailleTexteMap, Color.Black);
        }

        // ========================================================
        // 6. BOUTON RETOUR (Bonus pratique !)
        // ========================================================
        Rectangle boxRetour = new Rectangle(50, 50, 150, 60);
        bool retourHovered = Raylib.CheckCollisionPointRec(souris, boxRetour);
        Raylib.DrawRectangleRec(boxRetour, retourHovered ? Color.LightGray : Color.DarkGray);
        Raylib.DrawRectangleLinesEx(boxRetour, 3, Color.Black);
        Raylib.DrawText("RETOUR", 50 + 20, 50 + 20, 25, retourHovered ? Color.Black : Color.White);

        if (retourHovered && Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            Raylib.PlaySound(unselect);
            endroit = "menu"; // On retourne au menu principal
        }

        // ========================================================
        // 7. AFFICHAGE DES EFFETS DE CLIC (Identique au menu)
        // ========================================================
        for (int i = ListeEffets.Count - 1; i >= 0; i--)
        {
            ListeEffets[i].Opacite -= 5; 
            if (ListeEffets[i].Opacite <= 0)
            {
                ListeEffets.RemoveAt(i);
            }
            else 
            {
                Color couleurFondu = new Color(255, 255, 255, ListeEffets[i].Opacite);
                Raylib.DrawTextureEx(ListeEffets[i].Texture, ListeEffets[i].Position, 0.0f, 0.2f, couleurFondu);
            }
        }

        Raylib.EndDrawing();
    }

    public static void AfficherMenuOptions(ref string etatJeu)
    {
        int ecranLargeur = Raylib.GetScreenWidth();
        int ecranHauteur = Raylib.GetScreenHeight();
        Vector2 souris = Raylib.GetMousePosition();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(30, 30, 30, 255)); // Fond sombre classique

        // ==========================================
        // 1. LE TITRE
        // ==========================================
        int tailleTitre = 50;
        int largeurTitre = Raylib.MeasureText("PARAMÈTRES", tailleTitre);
        Raylib.DrawText("PARAMÈTRES", (ecranLargeur - largeurTitre) / 2, 40, tailleTitre, Color.White);

        // ==========================================
        // 2. LES ONGLETS (Navigation)
        // ==========================================
        Rectangle btnOnglet1 = new Rectangle(ecranLargeur / 2 - 220, 120, 200, 40);
        Rectangle btnOnglet2 = new Rectangle(ecranLargeur / 2 + 20, 120, 200, 40);

        // Changement d'onglet au clic
        if (Raylib.CheckCollisionPointRec(souris, btnOnglet1) && Raylib.IsMouseButtonPressed(MouseButton.Left)) ongletOptionActif = 0;
        if (Raylib.CheckCollisionPointRec(souris, btnOnglet2) && Raylib.IsMouseButtonPressed(MouseButton.Left)) ongletOptionActif = 1;

        // Couleurs des onglets selon la sélection
        Color couleurOnglet1 = (ongletOptionActif == 0) ? Color.Red : Color.DarkGray;
        Color couleurOnglet2 = (ongletOptionActif == 1) ? Color.Red : Color.DarkGray;

        Raylib.DrawRectangleRec(btnOnglet1, couleurOnglet1);
        Raylib.DrawText("GÉNÉRAL", (int)btnOnglet1.X + 45, (int)btnOnglet1.Y + 10, 20, Color.White);

        Raylib.DrawRectangleRec(btnOnglet2, couleurOnglet2);
        Raylib.DrawText("RACCOURCIS", (int)btnOnglet2.X + 35, (int)btnOnglet2.Y + 10, 20, Color.White);

        // Ligne de séparation sous les onglets
        Raylib.DrawLine(ecranLargeur / 2 - 300, 170, ecranLargeur / 2 + 300, 170, Color.Gray);

        // ==========================================
        // 3. LE CONTENU (Fake Options)
        // ==========================================
        int debutX = ecranLargeur / 2 - 250;
        int debutY = 220;

        if (ongletOptionActif == 0) // --- PAGE PRINCIPALE ---
        {
            // Fake Slider Volume
            Raylib.DrawText("Volume Général", debutX, debutY, 20, Color.LightGray);
            Raylib.DrawRectangle(debutX + 250, debutY, 200, 20, Color.DarkGray);
            Raylib.DrawRectangle(debutX + 250, debutY, 160, 20, Color.White); // 80% rempli
            Raylib.DrawText("80%", debutX + 460, debutY, 20, Color.White);

            // Fake Slider FOV
            Raylib.DrawText("Champ de Vision (FOV)", debutX, debutY + 60, 20, Color.LightGray);
            Raylib.DrawRectangle(debutX + 250, debutY + 60, 200, 20, Color.DarkGray);
            Raylib.DrawRectangle(debutX + 250, debutY + 60, 120, 20, Color.White); // 90 FOV rempli
            Raylib.DrawText("90", debutX + 460, debutY + 60, 20, Color.White);

            // Fake Bouton Qualité
            Raylib.DrawText("Qualité des Ombres", debutX, debutY + 120, 20, Color.LightGray);
            Raylib.DrawRectangle(debutX + 250, debutY + 115, 100, 30, Color.DarkGray);
            Raylib.DrawText("ULTRA", debutX + 265, debutY + 120, 20, Color.White);

            // Fake Bouton Plein Écran
            Raylib.DrawText("Mode d'affichage", debutX, debutY + 180, 20, Color.LightGray);
            Raylib.DrawRectangle(debutX + 250, debutY + 175, 150, 30, Color.DarkGray);
            Raylib.DrawText("Plein Écran", debutX + 265, debutY + 180, 20, Color.White);
        }
        else // --- PAGE RACCOURCIS (KEYBINDS) ---
        {
            // Liste de touches fake
            string[] actions = { "Avancer", "Reculer", "Aller à Gauche", "Aller à Droite", "Sauter", "S'accroupir", "Tirer", "Recharger" };
            string[] touches = { "W", "S", "A", "D", "ESPACE", "C", "CLIC GAUCHE", "R" };

            for (int i = 0; i < actions.Length; i++)
            {
                int yPos = debutY + (i * 45);
                
                // Dessine un fond légèrement plus clair 1 ligne sur 2
                if (i % 2 == 0) Raylib.DrawRectangle(debutX - 10, yPos - 5, 520, 35, new Color(40, 40, 40, 255));

                Raylib.DrawText(actions[i], debutX, yPos, 20, Color.LightGray);
                
                // Dessine la fausse touche
                Raylib.DrawRectangle(debutX + 300, yPos - 5, 150, 30, Color.DarkGray);
                Raylib.DrawText(touches[i], debutX + 315, yPos, 20, Color.White);
            }
        }

        // ==========================================
        // 4. BOUTON RETOUR
        // ==========================================
        Rectangle btnRetour = new Rectangle(ecranLargeur / 2 - 100, ecranHauteur - 100, 200, 50);
        bool retourHover = Raylib.CheckCollisionPointRec(souris, btnRetour);

        Raylib.DrawRectangleRec(btnRetour, retourHover ? Color.Red : Color.DarkGray);
        Raylib.DrawText("RETOUR", (int)btnRetour.X + 55, (int)btnRetour.Y + 15, 20, Color.White);

        // Si on clique sur Retour, on change l'état du jeu pour revenir à l'écran titre
        if (retourHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            etatJeu = "menu"; // Retour au menu principal
        }

        Raylib.EndDrawing();
    }

}