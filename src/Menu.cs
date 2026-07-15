using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;

// Moteur Physique
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;

//reseau 
using LiteNetLib;
using LiteNetLib.Utils;

partial class Program
{
    // ===== BOUCLE MENU =====


    static float echelle = 0.2f;
    static bool agrandissement = true;
    // 0 = Page Principale (Son/FOV), 1 = Page Raccourcis (Keybinds)
    public static int ongletOptionActif = 0;

    // Index de sélection clavier/manette : menu principal (0=Jouer,1=Options,2=Quitter)
    // et sélection de carte (0..nbMaps-1).
    static int menuPrincipalIndex = 0;
    static int choixCarteIndex = 0;

    // Variables pour le rebinding des touches
    private static bool isRebindingKey = false;
    private static string rebindingAction = "";
    private static int rebindingIndex = -1;

    public static void Menu()
    {
        SetActiveMusic(ActiveMusic.Menu);
        UpdateActiveMusicStream();

        // --- RENDU (Draw) ---
            //Raylib.BeginDrawing();

            // pour "quitter"
            if (Raylib.IsKeyDown(KeyBinds.SelectMenu)) 
            {
                Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                Program.currentState = Program.GameState.ChoiceMap;
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

            // === NAVIGATION CLAVIER / MANETTE ===
            // Haut/bas (ou avancer/reculer configurés) déplacent la sélection ; la souris
            // qui survole un bouton met aussi la sélection dessus (les deux restent synchro).
            if (KeyBinds.IsMenuDownPressed())
            {
                menuPrincipalIndex = (menuPrincipalIndex + 1) % 3;
                Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
            }
            if (KeyBinds.IsMenuUpPressed())
            {
                menuPrincipalIndex = (menuPrincipalIndex + 2) % 3;
                Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
            }
            if (Raylib.CheckCollisionPointRec(souris, boxPlay))   menuPrincipalIndex = 0;
            if (Raylib.CheckCollisionPointRec(souris, boxOption)) menuPrincipalIndex = 1;
            if (Raylib.CheckCollisionPointRec(souris, boxQuit))   menuPrincipalIndex = 2;

            // Entrée / A valide le bouton sélectionné.
            bool valider = KeyBinds.IsMenuConfirmPressed();



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
            if (menuPrincipalIndex == 0)
            {
                Raylib.DrawTextureEx(play_active, positionPlay-ajustement_draw, rotation, echelleBoutonActif, Color.White);

                if ((Raylib.CheckCollisionPointRec(souris, boxPlay) && Raylib.IsMouseButtonReleased(MouseButton.Left)) || valider)
                {
                    Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                    Program.currentState = Program.GameState.ChoiceMap;
                }
            }
            else
            {
                Raylib.DrawTextureEx(play_button, positionPlay, rotation, echelleBoutons, Color.White);
            }



            //option
            if (menuPrincipalIndex == 1)
            {
                Raylib.DrawTextureEx(option_active, positionOption-ajustement_draw, rotation, echelleBoutonActif, Color.White);

                if ((Raylib.CheckCollisionPointRec(souris, boxOption) && Raylib.IsMouseButtonReleased(MouseButton.Left)) || valider)
                {
                    Program.currentState = Program.GameState.Options;
                }
            }
            else
            {
                Raylib.DrawTextureEx(option_button, positionOption, rotation, echelleBoutons, Color.White);
            }



            //quit
            if (menuPrincipalIndex == 2)
            {
                //bouton quitter activé
                Raylib.DrawTextureEx(quit_active, positionQuit+ajustement_quit, rotation, echelleQuit, Color.White);

                if ((Raylib.CheckCollisionPointRec(souris, boxQuit) && Raylib.IsMouseButtonReleased(MouseButton.Left)) || valider)
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


            //Raylib.EndDrawing();
    }













    public static void ChoiceMap()
    {
        SetActiveMusic(ActiveMusic.Menu);
        UpdateActiveMusicStream();

        // 1. NETTOYAGE DES LISTES DU JEU PRÉCÉDENT
        // 1. NETTOYAGE DES LISTES DU JEU PRÉCÉDENT
        
        // A. On supprime proprement chaque mur
        foreach (MurPose mur in listeMur) simulation.Statics.Remove(mur.handlePhysique);
        listeMur.Clear();

        // B. On supprime proprement chaque baril
        foreach (BarrelSpot spot in barrelSpots)
        {
            if (spot.estSolide)
            {
                simulation.Statics.Remove(spot.handlePhysique);
                spot.estSolide = false;
            }
        }

        activeExplosions.Clear();
        activeDamageTexts.Clear();

        // C. On supprime proprement les ennemis
        foreach (Enemy enemy in enemiesList)
        {
            if (enemy.isAlive) simulation.Bodies.Remove(enemy.bodyId);
        }
        enemiesList.Clear();
        killCount = 0;
        deathCount = 0;

        // ==========================================
        // 2. LE CORRECTIF DU CRASH : PURGE DE LA MÉMOIRE
        // ==========================================
        if (mapDejaChargee)
        {
            // CORRECTION CRUCIALE : On retire la map de la simulation AVANT de la détruire !
            // Cela détruit automatiquement les "liens fantômes" avec le joueur.
            simulation.Statics.Remove(mapStaticHandle);
            
            // Libère la VRAM et la RAM
            Raylib.UnloadModel(mapModel);
            bepuMapMesh.Dispose(pool);
            mapDejaChargee = false;
        }


        listeMur.Clear(); // Très important sinon les murs fantômes restent !
        activeExplosions.Clear();
        activeDamageTexts.Clear();

        foreach (Enemy enemy in enemiesList)
        {
            if (enemy.isAlive) simulation.Bodies.Remove(enemy.bodyId);
        }
        enemiesList.Clear();

        // ==========================================
        // 2. LE CORRECTIF DU CRASH : PURGE DE LA MÉMOIRE
        // ==========================================
        if (mapDejaChargee)
        {
            // Libère la carte graphique (VRAM de Raylib)
            Raylib.UnloadModel(mapModel); 
            
            // Libère la RAM physique (Le BufferPool de BEPU)
            bepuMapMesh.Dispose(pool); 
            mapDejaChargee = false;
        }


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
        Texture2D[] texturesMap = { ImageMapTest, ImageMapVille, Imageville2, ImageMapBlocs };
        string[] nomsMap = { "Le Cube", "La Ville", "Chantier", "Blocs" };
        string[] destinationsMap = { "boucle", "boucle", "boucle", "boucle" }; // Ce qu'on va mettre dans 'endroit'
        string[] ModelMapPath = {"assets/models/maps/test.glb","assets/models/maps/map.glb","assets/models/maps/sandbox.glb","assets/models/maps/blockmap.glb"};
        //attention si on ajoute une map ne pas oublier de mettre
        //les spawnpoint a jour dans jeu.cs

        // Paramètres de taille et d'espacement
        int largeurMap = 350;
        int hauteurMap = 220;
        int espacement = 80;
        int nbMaps = 4;

        // Calcul mathématique pour centrer le tout parfaitement
        int largeurTotale = (largeurMap * nbMaps) + (espacement * (nbMaps - 1));
        int startX = (Raylib.GetScreenWidth() - largeurTotale) / 2;
        int startY = (Raylib.GetScreenHeight() - hauteurMap) / 2;

        Vector2 souris = Raylib.GetMousePosition();

        // === NAVIGATION : chaque carte est un item focalisable (flèches / stick / souris) ===
        // La carte focalisée EST la sélection ; on la lance avec Entrée / A / clic.

        // ========================================================
        // 5. AFFICHAGE DES CARTES
        // ========================================================
        for (int n = 0; n < nbMaps; n++)
        {
            int mapX = startX + n * (largeurMap + espacement);
            Rectangle mapBox = new Rectangle(mapX, startY, largeurMap, hauteurMap);

            bool focusCarte = MenuNav.Item(mapBox, out bool lancerCarte);
            if (focusCarte) choixCarteIndex = n;

            // A. Image
            Rectangle sourceRec = new Rectangle(0, 0, texturesMap[n].Width, texturesMap[n].Height);
            Raylib.DrawTexturePro(texturesMap[n], sourceRec, mapBox, Vector2.Zero, 0f, Color.White);

            // B. Sélection : contour rouge + assombrissement sur la carte focalisée
            if (focusCarte)
            {
                Raylib.DrawRectangleLinesEx(mapBox, 6, Color.Red);
                Raylib.DrawRectangleRec(mapBox, new Color(0, 0, 0, 50));
            }
            else
            {
                Raylib.DrawRectangleLinesEx(mapBox, 3, Color.Black);
            }

            // C. Texte (fond blanc pour la lisibilité)
            int tailleTexteMap = 30;
            int largeurTexteMap = Raylib.MeasureText(nomsMap[n], tailleTexteMap);
            int texteX = mapX + (largeurMap - largeurTexteMap) / 2;
            int texteY = startY + (hauteurMap - tailleTexteMap) / 2;
            Raylib.DrawRectangle(texteX - 10, texteY - 5, largeurTexteMap + 20, tailleTexteMap + 10, new Color(255, 255, 255, 180));
            Raylib.DrawText(nomsMap[n], texteX, texteY, tailleTexteMap, Color.Black);

            // D. Lancement de CETTE carte
            if (lancerCarte)
            {
                Raylib.PlaySound(select);
                Raylib.DisableCursor(); // Si on passe en mode FPS

                // On décharge d'abord l'ancienne map (modèle + collisions BEPU).
                DechargerMapActuelle();
                mapModel = Raylib.LoadModel(ModelMapPath[n]);
                ExtraireTrianglesMap();
                unsafe
                {
                    for (int j = 0; j < mapModel.MaterialCount; j++) mapModel.Materials[j].Shader = lightShader;
                }
                ApplyMapTextures();
                LoadMapSpawns(n);
                InitBarrels();
                InitEnnemis(); // respecte Program.zombiesActifs

                int indexAleatoire = Raylib.GetRandomValue(0, listeSpawns.Count - 1);
                BodyReference joueurVivant = simulation.Bodies.GetBodyReference(PlayerId);
                joueurVivant.Pose.Position = listeSpawns[indexAleatoire];
                joueurVivant.Velocity.Linear = Vector3.Zero;
                joueurVivant.Velocity.Angular = Vector3.Zero;

                Program.isOnline = false;
                Program.currentState = Program.GameState.Playing;
            }
        }

        // ========================================================
        // 5bis. BOUTON TUTO (cliquable, sans effet) + INTERRUPTEUR ZOMBIES
        // ========================================================
        int centreX = Raylib.GetScreenWidth() / 2;
        int optY = startY + hauteurMap + 45;

        // Bouton TUTO
        Rectangle boxTuto = new Rectangle(centreX - 95, optY, 190, 55);
        bool focusTuto = MenuNav.Item(boxTuto, out bool clicTuto);
        Raylib.DrawRectangleRec(boxTuto, focusTuto ? Color.LightGray : Color.Gray);
        Raylib.DrawRectangleLinesEx(boxTuto, 3, focusTuto ? Color.White : Color.Black);
        int wTuto = Raylib.MeasureText("TUTO", 30);
        Raylib.DrawText("TUTO", centreX - wTuto / 2, (int)boxTuto.Y + 13, 30, focusTuto ? Color.Black : Color.White);
        if (clicTuto) Program.PlaySoundWithPriority(select, Program.SoundPriority.Low); // TODO : brancher le tutoriel

        // Interrupteur ZOMBIES : active/désactive le spawn des zombies au lancement de la map
        int labelZY = (int)boxTuto.Y + 55 + 22;
        int wZ = Raylib.MeasureText("ZOMBIES", 25);
        Raylib.DrawText("ZOMBIES", centreX - wZ / 2, labelZY, 25, Color.Black);

        Rectangle boxSwitch = new Rectangle(centreX - 45, labelZY + 32, 90, 40);
        bool focusSwitch = MenuNav.Item(boxSwitch, out bool clicSwitch);
        if (clicSwitch)
        {
            Program.zombiesActifs = !Program.zombiesActifs;
            Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
        }
        // Interrupteur CARRÉ : piste rectangulaire (vert = activé, gris = désactivé) +
        // bouton carré coulissant (gauche = OFF, droite = ON) + contour blanc si focalisé.
        Color pisteColor = Program.zombiesActifs ? new Color(70, 180, 70, 255) : new Color(90, 90, 90, 255);
        Raylib.DrawRectangleRec(boxSwitch, pisteColor);
        Raylib.DrawRectangleLinesEx(boxSwitch, focusSwitch ? 3 : 2, focusSwitch ? Color.White : Color.Black);
        int knobSize = (int)boxSwitch.Height - 8;
        int knobY = (int)boxSwitch.Y + 4;
        int knobX = Program.zombiesActifs ? (int)(boxSwitch.X + boxSwitch.Width - knobSize - 4) : (int)(boxSwitch.X + 4);
        Raylib.DrawRectangle(knobX, knobY, knobSize, knobSize, Color.White);

        // ========================================================
        // 6. BOUTON RETOUR
        // ========================================================
        Rectangle boxRetour = new Rectangle(50, 50, 150, 60);
        // RETOUR : à la souris OU via le bouton Retour (Échap / B). Hors du cycle des flèches
        // pour ne pas parasiter la sélection des cartes.
        bool retourHovered = MenuNav.UsingMouse && Raylib.CheckCollisionPointRec(souris, boxRetour);
        Raylib.DrawRectangleRec(boxRetour, retourHovered ? Color.LightGray : Color.DarkGray);
        Raylib.DrawRectangleLinesEx(boxRetour, 3, Color.Black);
        Raylib.DrawText("RETOUR", 50 + 20, 50 + 20, 25, retourHovered ? Color.Black : Color.White);

        if ((retourHovered && Raylib.IsMouseButtonReleased(MouseButton.Left)) || MenuNav.Back)
        {
            Raylib.PlaySound(unselect);
            Program.currentState = Program.GameState.ModeSelection;
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


    }

    // Variables pour le rebinding des touches sont déclarées plus haut à la ligne 22

    public static void AfficherMenuOptions(ref string etatJeu)
    {
        // Keep music playing in options menu
        SetActiveMusic(ActiveMusic.Menu);
        UpdateActiveMusicStream();

        int ecranLargeur = Raylib.GetScreenWidth();
        int ecranHauteur = Raylib.GetScreenHeight();
        Vector2 souris = Raylib.GetMousePosition();

        // CORRECTION DU CRASH DE RENDU : On utilise DrawRectangle au lieu de ClearBackground, 
        // et on supprime les BeginDrawing/EndDrawing car ils sont déjà gérés par le parent !
        if (!isPaused)
        {
            Raylib.DrawRectangle(0, 0, ecranLargeur, ecranHauteur, new Color(30, 30, 30, 255));
        }
        else
        {
            Raylib.DrawRectangle(0, 0, ecranLargeur, ecranHauteur, new Color(30, 30, 30, 240));
        }

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

        bool focusOnglet1 = MenuNav.Item(btnOnglet1, out bool clicOnglet1);
        bool focusOnglet2 = MenuNav.Item(btnOnglet2, out bool clicOnglet2);

        // Onglet 1 — Paramètres Généraux (contour blanc = focalisé au clavier/manette)
        Color couleurOnglet1 = (ongletOptionActif == 0) ? Color.Red : Color.DarkGray;
        Raylib.DrawRectangleRec(btnOnglet1, couleurOnglet1);
        Raylib.DrawRectangleLinesEx(btnOnglet1, focusOnglet1 ? 3 : 2, focusOnglet1 ? Color.White : Color.Black);
        int w1 = Raylib.MeasureText("GÉNÉRAL", 18);
        Raylib.DrawText("GÉNÉRAL", (int)btnOnglet1.X + ((int)btnOnglet1.Width - w1) / 2, (int)btnOnglet1.Y + 11, 18, Color.White);

        // Onglet 2 — Modifier les Touches
        Color couleurOnglet2 = (ongletOptionActif == 1) ? Color.Red : Color.DarkGray;
        Raylib.DrawRectangleRec(btnOnglet2, couleurOnglet2);
        Raylib.DrawRectangleLinesEx(btnOnglet2, focusOnglet2 ? 3 : 2, focusOnglet2 ? Color.White : Color.Black);
        int w2 = Raylib.MeasureText("RACCOURCIS", 18);
        Raylib.DrawText("RACCOURCIS", (int)btnOnglet2.X + ((int)btnOnglet2.Width - w2) / 2, (int)btnOnglet2.Y + 11, 18, Color.White);

        if (clicOnglet1) ongletOptionActif = 0;
        if (clicOnglet2) ongletOptionActif = 1;

        

        // ==========================================
        // 3. LE CONTENU
        // ==========================================
        int debutX = ecranLargeur / 2 - 300;
        int debutY = 220;
        int espacementLigne = 70;

        if (ongletOptionActif == 0) // --- PAGE GÉNÉRALE ---
        {
            // === VOLUME GÉNÉRAL ===
            Raylib.DrawText("Volume Général", debutX, debutY, 22, Color.LightGray);
            DrawVolumeSlider(debutX, debutY + 30, 300, ref Settings.MasterVolume, "Master");

            // === VOLUME SFX ===
            Raylib.DrawText("Volume SFX", debutX, debutY + espacementLigne, 22, Color.LightGray);
            DrawVolumeSlider(debutX, debutY + espacementLigne + 30, 300, ref Settings.SFXVolume, "SFX");

            // === VOLUME MUSIQUE ===
            Raylib.DrawText("Volume Musique", debutX, debutY + espacementLigne * 2, 22, Color.LightGray);
            DrawVolumeSlider(debutX, debutY + espacementLigne * 2 + 30, 300, ref Settings.MusicVolume, "Music");

            // === FOV === (souris : cliquer/glisser ; clavier/manette : Gauche/Droite)
            Raylib.DrawText("Champ de Vision (FOV)", debutX, debutY + espacementLigne * 3, 22, Color.LightGray);
            Rectangle fovSlider = new Rectangle(debutX, debutY + espacementLigne * 3 + 30, 300, 25);
            bool fovFocus = MenuNav.Item(fovSlider, out _);
            Raylib.DrawRectangle(debutX, debutY + espacementLigne * 3 + 30, 300, 25, new Color(50, 50, 50, 255));
            int fovFilled = (int)((Settings.BaseFOV - 40f) / (120f - 40f) * 300f);
            Raylib.DrawRectangle(debutX, debutY + espacementLigne * 3 + 30, fovFilled, 25, Color.Red);
            if (fovFocus) Raylib.DrawRectangleLinesEx(fovSlider, 2, Color.White);
            Raylib.DrawText($"{(int)Settings.BaseFOV}°", debutX + 310, debutY + espacementLigne * 3 + 30, 20, Color.White);

            if (MenuNav.UsingMouse && Raylib.CheckCollisionPointRec(souris, fovSlider) && Raylib.IsMouseButtonDown(MouseButton.Left))
                Settings.BaseFOV = Math.Clamp(40f + ((souris.X - fovSlider.X) / 300f) * (120f - 40f), 40f, 120f);
            if (fovFocus && !MenuNav.UsingMouse)
            {
                if (MenuNav.Right) Settings.BaseFOV = Math.Clamp(Settings.BaseFOV + 2f, 40f, 120f);
                if (MenuNav.Left)  Settings.BaseFOV = Math.Clamp(Settings.BaseFOV - 2f, 40f, 120f);
            }

            // === SENSIBILITÉ SOURIS ===
            Raylib.DrawText("Sensibilité Souris", debutX, debutY + espacementLigne * 4, 22, Color.LightGray);
            Rectangle mouseSlider = new Rectangle(debutX, debutY + espacementLigne * 4 + 30, 300, 25);
            bool mouseFocus = MenuNav.Item(mouseSlider, out _);
            Raylib.DrawRectangle(debutX, debutY + espacementLigne * 4 + 30, 300, 25, new Color(50, 50, 50, 255));
            float mouseSensPercent = (Settings.MouseSensitivity - 0.001f) / (0.01f - 0.001f);
            int mouseSensFilled = (int)(Math.Clamp(mouseSensPercent, 0, 1) * 300f);
            Raylib.DrawRectangle(debutX, debutY + espacementLigne * 4 + 30, mouseSensFilled, 25, Color.Red);
            if (mouseFocus) Raylib.DrawRectangleLinesEx(mouseSlider, 2, Color.White);
            Raylib.DrawText($"{(int)(mouseSensPercent * 100)}%", debutX + 310, debutY + espacementLigne * 4 + 30, 20, Color.White);

            if (MenuNav.UsingMouse && Raylib.CheckCollisionPointRec(souris, mouseSlider) && Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                mouseSensPercent = Math.Clamp((souris.X - mouseSlider.X) / 300f, 0, 1);
                Settings.MouseSensitivity = 0.001f + mouseSensPercent * (0.01f - 0.001f);
            }
            if (mouseFocus && !MenuNav.UsingMouse)
            {
                if (MenuNav.Right) Settings.MouseSensitivity = Math.Clamp(Settings.MouseSensitivity + 0.0005f, 0.001f, 0.01f);
                if (MenuNav.Left)  Settings.MouseSensitivity = Math.Clamp(Settings.MouseSensitivity - 0.0005f, 0.001f, 0.01f);
            }
        }
        else // --- PAGE RACCOURCIS ---
        {
            DrawKeybindsMenu(debutX, debutY, ecranLargeur, ecranHauteur, souris);
        }

        // ==========================================
        // 4. BOUTON RETOUR
        // ==========================================
        Rectangle btnRetour = new Rectangle(ecranLargeur / 2 - 100, ecranHauteur - 100, 200, 50);
        bool retourHover = MenuNav.Item(btnRetour, out bool retourClic);

        Raylib.DrawRectangleRec(btnRetour, retourHover ? Color.Red : Color.DarkGray);
        Raylib.DrawText("RETOUR", (int)btnRetour.X + 55, (int)btnRetour.Y + 15, 20, Color.White);

        // Retour aussi via Échap / B — sauf pendant un réassignement de touche.
        // EN JEU (isPaused), l'overlay Options est refermé par le gestionnaire ÉCHAP au
        // relâchement (BouclePrincipale) : on n'écoute donc MenuNav.Back qu'HORS jeu, pour
        // ne pas compter Échap deux fois.
        if ((retourClic || (MenuNav.Back && !isPaused)) && !isRebindingKey)
        {
            Program.PlaySoundWithPriority(unselect, Program.SoundPriority.Low);
            if (isPaused) isOptionsMenuOpen = false;
            else Program.currentState = Program.GameState.MainMenu;
            isRebindingKey = false;
        }

        // On ne clôture le dessin QUE si on est dans le menu d'accueil
        if (!isPaused) 
        {
            //Raylib.EndDrawing();
        }
    }

    private static void DrawVolumeSlider(int x, int y, int width, ref float volume, string label)
    {
        Vector2 souris = Raylib.GetMousePosition();
        Rectangle sliderRect = new Rectangle(x, y, width, 25);

        // Focus clavier/manette (le survol souris est géré à l'intérieur de MenuNav.Item)
        bool focused = MenuNav.Item(sliderRect, out _);

        // Fond + portion remplie
        Raylib.DrawRectangle(x, y, width, 25, new Color(50, 50, 50, 255));
        int filledWidth = (int)(volume * width);
        Raylib.DrawRectangle(x, y, filledWidth, 25, Color.Red);
        if (focused) Raylib.DrawRectangleLinesEx(sliderRect, 2, Color.White); // contour = focalisé

        // Texte du pourcentage
        Raylib.DrawText($"{(int)(volume * 100)}%", x + width + 10, y, 20, Color.White);

        bool changed = false;

        // Souris : cliquer/glisser
        if (MenuNav.UsingMouse && Raylib.CheckCollisionPointRec(souris, sliderRect) && Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            volume = Math.Clamp((souris.X - x) / width, 0, 1);
            changed = true;
        }

        // Clavier / manette : Gauche/Droite par pas de 5 %
        if (focused && !MenuNav.UsingMouse)
        {
            if (MenuNav.Right) { volume = Math.Clamp(volume + 0.05f, 0, 1); changed = true; }
            if (MenuNav.Left)  { volume = Math.Clamp(volume - 0.05f, 0, 1); changed = true; }
        }

        if (changed) ApplyVolumeChanges(label);
    }

    private static void ApplyVolumeChanges(string label)
    {
        // Peu importe quel slider a bougé (Master, SFX, ou Music), 
        // on met à jour toute la carte son !
        Program.UpdateAllVolumes();
        // SFX and Master volumes will be applied when sounds are played via PlaySoundWithPriority
    }

    private static void DrawKeybindsMenu(int startX, int startY, int screenWidth, int screenHeight, Vector2 mourisPos)
    {
        // Liste des actions personnalisables
        var keybindsMap = new List<(string name, KeyboardKey[] keys)>
        {
            ("Avancer", new[] { Settings.KEY_MoveForward, Settings.KEY_MoveForwardAlt }),
            ("Reculer", new[] { Settings.KEY_MoveBackward, Settings.KEY_MoveBackwardAlt }),
            ("Gauche", new[] { Settings.KEY_MoveLeft, Settings.KEY_MoveLeftAlt }),
            ("Droite", new[] { Settings.KEY_MoveRight, Settings.KEY_MoveRightAlt }),
            ("Sauter", new[] { Settings.KEY_Jump }),
            ("S'accroupir", new[] { Settings.KEY_Crouch }),
            ("Sprint", new[] { Settings.KEY_Sprint }),
            ("Dash", new[] { Settings.KEY_Dash }),
            ("Recharger", new[] { Settings.KEY_Reload }),
            ("Menu Jeu", new[] { Settings.KEY_ToggleGameMenu }),
            ("Construire Mur", new[] { Settings.KEY_BuildWall })
        };

        int y = startY;
        int lineHeight = 50;

        for (int i = 0; i < keybindsMap.Count; i++)
        {
            // Fond alternant
            if (i % 2 == 0) 
                Raylib.DrawRectangle(startX - 20, y - 5, 620, lineHeight - 5, new Color(40, 40, 40, 255));

            Raylib.DrawText(keybindsMap[i].name, startX, y + 10, 22, Color.LightGray);

            // Afficher les touches
            string keyDisplay = "";
            foreach (var key in keybindsMap[i].keys)
            {
                keyDisplay += (keyDisplay.Length > 0 ? " / " : "") + Settings.GetKeyName(key);
            }

            Rectangle keyButton = new Rectangle(startX + 350, y, 200, lineHeight - 10);
            bool isHover = MenuNav.Item(keyButton, out bool clicKey);

            Raylib.DrawRectangleRec(keyButton, isHover ? new Color(80, 40, 40, 255) : Color.DarkGray);
            Raylib.DrawText(keyDisplay, (int)keyButton.X + 10, (int)keyButton.Y + 12, 18, Color.White);

            if (clicKey && !isRebindingKey)
            {
                isRebindingKey = true;
                rebindingAction = keybindsMap[i].name;
                rebindingIndex = i;
            }

            y += lineHeight;
        }

        // Afficher le message de rebinding
        if (isRebindingKey)
        {
            // Overlay semi-transparent
            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(0, 0, 0, 180));
            
            // Texte d'instructions
            string message = $"Appuyez sur une touche pour '{rebindingAction}'";
            int msgWidth = Raylib.MeasureText(message, 30);
            Raylib.DrawText(message, (screenWidth - msgWidth) / 2, screenHeight / 2 - 50, 30, Color.White);
            
            // Attendre une touche
            int pressedKeyCode = Raylib.GetKeyPressed();
            if (pressedKeyCode != 0)
            {
                KeyboardKey pressedKey = (KeyboardKey)pressedKeyCode;
                // Appliquer le changement de touche
                ApplyKeybindChange(rebindingIndex, pressedKey);
                isRebindingKey = false;
                rebindingIndex = -1;
            }
        }
    }

    private static void ApplyKeybindChange(int index, KeyboardKey newKey)
    {
        // Prevent changing to invalid keys
        if (newKey == KeyboardKey.Null) return;
        
        // Check for conflicts with other keybinds
        var allKeys = new List<KeyboardKey>
        {
            Settings.KEY_MoveForward, Settings.KEY_MoveForwardAlt,
            Settings.KEY_MoveBackward, Settings.KEY_MoveBackwardAlt,
            Settings.KEY_MoveLeft, Settings.KEY_MoveLeftAlt,
            Settings.KEY_MoveRight, Settings.KEY_MoveRightAlt,
            Settings.KEY_Jump, Settings.KEY_Crouch, Settings.KEY_Sprint,
            Settings.KEY_Dash, Settings.KEY_Reload, Settings.KEY_ToggleGameMenu,
            Settings.KEY_BuildWall
        };
        
        // Remove the current key being changed (to allow reassigning the same key)
        switch (index)
        {
            case 0: allKeys.Remove(Settings.KEY_MoveForward); break;
            case 1: allKeys.Remove(Settings.KEY_MoveBackward); break;
            case 2: allKeys.Remove(Settings.KEY_MoveLeft); break;
            case 3: allKeys.Remove(Settings.KEY_MoveRight); break;
            case 4: allKeys.Remove(Settings.KEY_Jump); break;
            case 5: allKeys.Remove(Settings.KEY_Crouch); break;
            case 6: allKeys.Remove(Settings.KEY_Sprint); break;
            case 7: allKeys.Remove(Settings.KEY_Dash); break;
            case 8: allKeys.Remove(Settings.KEY_Reload); break;
            case 9: allKeys.Remove(Settings.KEY_ToggleGameMenu); break;
            case 10: allKeys.Remove(Settings.KEY_BuildWall); break;
        }
        
        // If the new key is already used, don't assign
        if (allKeys.Contains(newKey)) return;
        
        // Now assign the new key
        switch (index)
        {
            case 0: Settings.KEY_MoveForward = newKey; break;
            case 1: Settings.KEY_MoveBackward = newKey; break;
            case 2: Settings.KEY_MoveLeft = newKey; break;
            case 3: Settings.KEY_MoveRight = newKey; break;
            case 4: Settings.KEY_Jump = newKey; break;
            case 5: Settings.KEY_Crouch = newKey; break;
            case 6: Settings.KEY_Sprint = newKey; break;
            case 7: Settings.KEY_Dash = newKey; break;
            case 8: Settings.KEY_Reload = newKey; break;
            case 9: Settings.KEY_ToggleGameMenu = newKey; break;
            case 10: Settings.KEY_BuildWall = newKey; break;
        }
    }



    // ==========================================
    // MENU PAUSE HORS LIGNE (Jeu figé, centré)
    // ==========================================
    public static void MenugameOffline()
    {
        Vector2 souris = Raylib.GetMousePosition();

        // 1. Fond semi-transparent pour assombrir le jeu derrière (Effet Pause)
        Raylib.DrawRectangle(0, 0, LargeurFenetre, HauteurFenetre, new Color(0, 0, 0, 150));

        // 2. Paramètres des boutons (Calqués sur ta maquette)
        int largeurBouton = 300;
        int hauteurBouton = 80;
        int ecart = 20;

        // On calcule les positions pour que ce soit parfaitement centré
        int posX = (LargeurFenetre - largeurBouton) / 2;
        int posY_resume = (HauteurFenetre / 2) - hauteurBouton - ecart;
        int posY_options = (HauteurFenetre / 2);
        int posY_menu = (HauteurFenetre / 2) + hauteurBouton + ecart;

        // 3. Création des zones de collision
        Rectangle boxResume = new Rectangle(posX, posY_resume, largeurBouton, hauteurBouton);
        Rectangle boxOptions = new Rectangle(posX, posY_options, largeurBouton, hauteurBouton);
        Rectangle boxMenu = new Rectangle(posX, posY_menu, largeurBouton, hauteurBouton);

        // --- DESSIN ET LOGIQUE DES BOUTONS ---

        // A. BOUTON RESUME
        bool hoverResume = MenuNav.Item(boxResume, out bool clicResume);
        Raylib.DrawRectangleRec(boxResume, new Color(60, 60, 60, 255)); // Fond gris
        Raylib.DrawRectangleLinesEx(boxResume, 4, hoverResume ? Color.Red : Color.Black); // Bordure rouge si survolé/focalisé

        int widthResume = Raylib.MeasureText("RESUME", 40);
        Raylib.DrawText("RESUME", posX + (largeurBouton - widthResume) / 2, posY_resume + 20, 40, Color.White);

        // ÉCHAP / B ferme aussi le menu pause (retour au jeu)
        if (clicResume)
        {
            Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
            isPaused = false;
            Raylib.DisableCursor(); // On redonne le contrôle de la caméra
        }

        // B. BOUTON OPTIONS
        bool hoverOptions = MenuNav.Item(boxOptions, out bool clicOptions);
        Raylib.DrawRectangleRec(boxOptions, new Color(60, 60, 60, 255));
        Raylib.DrawRectangleLinesEx(boxOptions, 4, hoverOptions ? Color.Red : Color.Black);

        int widthOptions = Raylib.MeasureText("OPTIONS", 40);
        Raylib.DrawText("OPTIONS", posX + (largeurBouton - widthOptions) / 2, posY_options + 20, 40, Color.White);

        if (clicOptions)
        {
            Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
            isOptionsMenuOpen = true; // On ouvre l'overlay au lieu de changer d'endroit !
        }

        // C. BOUTON MENU
        bool hoverMenu = MenuNav.Item(boxMenu, out bool clicMenu);
        Raylib.DrawRectangleRec(boxMenu, new Color(60, 60, 60, 255));
        Raylib.DrawRectangleLinesEx(boxMenu, 4, hoverMenu ? Color.Red : Color.Black);

        int widthMenu = Raylib.MeasureText("MENU", 40);
        Raylib.DrawText("MENU", posX + (largeurBouton - widthMenu) / 2, posY_menu + 20, 40, Color.White);

        if (clicMenu)
        {
            Program.PlaySoundWithPriority(unselect, Program.SoundPriority.Low);
            
            // Grand nettoyage avant de retourner au menu principal
            isPaused = false;
            Raylib.EnableCursor();

            foreach (Enemy enemy in enemiesList) { if (enemy.isAlive) simulation.Bodies.Remove(enemy.bodyId); }
            enemiesList.Clear();
            activeExplosions.Clear();
            activeDamageTexts.Clear();
            
            Program.currentState = Program.GameState.MainMenu;
        }
    }

    // ==========================================
    // MENU PAUSE EN LIGNE (Jeu actif, décalé à gauche)
    // ==========================================
    public static void MenugameOnline()
    {
        Vector2 souris = Raylib.GetMousePosition();

        // 1. Fond TRÈS léger (Pour bien voir le jeu tourner en fond sans être aveuglé)
        Raylib.DrawRectangle(0, 0, LargeurFenetre, HauteurFenetre, new Color(0, 0, 0, 80));

        // 2. Paramètres des boutons (Décalés à gauche)
        int largeurBouton = 300;
        int hauteurBouton = 80;
        int ecart = 20;

        // Position X à 25% de l'écran (sur la gauche) au lieu du centre
        int posX_boutons = (LargeurFenetre / 4) - (largeurBouton / 2); 
        int posY_resume = (HauteurFenetre / 2) - hauteurBouton - ecart;
        int posY_options = (HauteurFenetre / 2);
        int posY_menu = (HauteurFenetre / 2) + hauteurBouton + ecart;

        Rectangle boxResume = new Rectangle(posX_boutons, posY_resume, largeurBouton, hauteurBouton);
        Rectangle boxOptions = new Rectangle(posX_boutons, posY_options, largeurBouton, hauteurBouton);
        Rectangle boxMenu = new Rectangle(posX_boutons, posY_menu, largeurBouton, hauteurBouton);

        // --- BOUTON RESUME ---
        bool hoverResume = MenuNav.Item(boxResume, out bool clicResume);
        Raylib.DrawRectangleRec(boxResume, new Color(60, 60, 60, 255));
        Raylib.DrawRectangleLinesEx(boxResume, 4, hoverResume ? Color.Red : Color.Black);
        int widthResume = Raylib.MeasureText("RESUME", 40);
        Raylib.DrawText("RESUME", posX_boutons + (largeurBouton - widthResume) / 2, posY_resume + 20, 40, Color.White);

        if (clicResume)
        {
            Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
            isPaused = false;
            Raylib.DisableCursor();
        }

        // --- BOUTON OPTIONS ---
        bool hoverOptions = MenuNav.Item(boxOptions, out bool clicOptions);
        Raylib.DrawRectangleRec(boxOptions, new Color(60, 60, 60, 255));
        Raylib.DrawRectangleLinesEx(boxOptions, 4, hoverOptions ? Color.Red : Color.Black);
        int widthOptions = Raylib.MeasureText("OPTIONS", 40);
        Raylib.DrawText("OPTIONS", posX_boutons + (largeurBouton - widthOptions) / 2, posY_options + 20, 40, Color.White);

        if (clicOptions)
        {
            Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
            isOptionsMenuOpen = true; // On ouvre l'overlay au lieu de changer d'endroit !
        }

        // --- BOUTON(S) DE SORTIE ---
        // EN LIGNE : on ne casse plus toute la session ! Deux choix distincts :
        //   - RETOUR AU SALON : l'hôte termine le match pour tous ; le client repart au
        //     salon en RESTANT connecté (il pourra rejoindre la partie via le hot-join).
        //   - QUITTER LA SESSION : déconnexion propre -> menu réseau.
        // HORS LIGNE (solo) : un seul bouton MENU -> menu principal, comme avant.
        if (Program.isOnline)
        {
            // Bouton 1 : RETOUR AU SALON (à la place du MENU)
            bool hoverSalon = MenuNav.Item(boxMenu, out bool clicSalon);
            Raylib.DrawRectangleRec(boxMenu, new Color(60, 60, 60, 255));
            Raylib.DrawRectangleLinesEx(boxMenu, 4, hoverSalon ? Color.Red : Color.Black);
            int wSalon = Raylib.MeasureText("SALON", 40);
            Raylib.DrawText("SALON", posX_boutons + (largeurBouton - wSalon) / 2, posY_menu + 20, 40, Color.White);
            if (clicSalon)
            {
                Program.PlaySoundWithPriority(unselect, Program.SoundPriority.Low);
                isPaused = false;
                if (Program.isServer) Program.HoteTerminerMatch();  // fin du match pour tout le monde
                else Program.ClientQuitterVersSalon();               // le client repart au salon (reste connecté)
            }

            // Bouton 2 : QUITTER LA SESSION (juste en dessous)
            Rectangle boxQuit = new Rectangle(posX_boutons, posY_menu + hauteurBouton + ecart, largeurBouton, hauteurBouton);
            bool hoverQuit = MenuNav.Item(boxQuit, out bool clicQuit);
            Raylib.DrawRectangleRec(boxQuit, new Color(60, 40, 40, 255));
            Raylib.DrawRectangleLinesEx(boxQuit, 4, hoverQuit ? Color.Red : Color.Black);
            int wQuit = Raylib.MeasureText("QUITTER", 34);
            Raylib.DrawText("QUITTER", posX_boutons + (largeurBouton - wQuit) / 2, (int)boxQuit.Y + 22, 34, Color.White);
            if (clicQuit)
            {
                Program.PlaySoundWithPriority(unselect, Program.SoundPriority.Low);
                isPaused = false;
                Raylib.EnableCursor();
                foreach (Enemy enemy in enemiesList) { if (enemy.isAlive) simulation.Bodies.Remove(enemy.bodyId); }
                enemiesList.Clear();
                activeExplosions.Clear();
                activeDamageTexts.Clear();
                Program.CouperReseau();
                Program.currentState = Program.GameState.NetworkHub;
            }
        }
        else
        {
            // --- BOUTON MENU (solo) ---
            bool hoverMenu = MenuNav.Item(boxMenu, out bool clicMenu);
            Raylib.DrawRectangleRec(boxMenu, new Color(60, 60, 60, 255));
            Raylib.DrawRectangleLinesEx(boxMenu, 4, hoverMenu ? Color.Red : Color.Black);
            int widthMenu = Raylib.MeasureText("MENU", 40);
            Raylib.DrawText("MENU", posX_boutons + (largeurBouton - widthMenu) / 2, posY_menu + 20, 40, Color.White);

            if (clicMenu)
            {
                Program.PlaySoundWithPriority(unselect, Program.SoundPriority.Low);
                isPaused = false;
                Raylib.EnableCursor();

                // Grand nettoyage avant de retourner au menu principal
                foreach (Enemy enemy in enemiesList) { if (enemy.isAlive) simulation.Bodies.Remove(enemy.bodyId); }
                enemiesList.Clear();
                activeExplosions.Clear();
                activeDamageTexts.Clear();

                Program.CouperReseau();

                Program.currentState = Program.GameState.MainMenu;
            }
        }

        // ==========================================
        // 3. LE TABLEAU DES SCORES (À droite) - LES VRAIS JOUEURS !
        // ==========================================
        int scoreWidth = 600;
        int scoreHeight = 400;

        // Position X à 50% de l'écran (sur la droite)
        int scoreX = (LargeurFenetre / 2);
        int scoreY = posY_resume; // Aligné sur le haut du bouton RESUME pour faire propre

        // Fond du tableau (Gris très foncé transparent)
        Raylib.DrawRectangle(scoreX, scoreY, scoreWidth, scoreHeight, new Color(30, 30, 30, 200));

        // En-tête (Gris moyen opaque)
        Raylib.DrawRectangle(scoreX, scoreY, scoreWidth, 50, new Color(80, 80, 80, 255));

        // --- Titres des colonnes ---
        int textY = scoreY + 15;
        Raylib.DrawText("name", scoreX + 20, textY, 25, Color.White);
        Raylib.DrawText("kill", scoreX + 300, textY, 25, Color.White);
        Raylib.DrawText("death", scoreX + 420, textY, 25, Color.White);
        Raylib.DrawText("ping", scoreX + 520, textY, 25, Color.White);

        // --- LIGNE 1 : NOUS ---
        int rowY = scoreY + 70;
        string monNom = string.IsNullOrEmpty(Program.myLobbyName) ? "Moi" : Program.myLobbyName;
        int monPing = (!Program.isServer && Program.netManager != null && Program.netManager.FirstPeer != null) ? Program.netManager.FirstPeer.Ping : 0;
        Raylib.DrawText(monNom, scoreX + 20, rowY, 30, Color.White);
        Raylib.DrawText(Program.killCount.ToString(), scoreX + 300, rowY, 30, Color.White);
        Raylib.DrawText(Program.deathCount.ToString(), scoreX + 420, rowY, 30, Color.White);
        Raylib.DrawText(monPing.ToString(), scoreX + 520, rowY, 30, monPing < 80 ? Color.Green : Color.Red);
        rowY += 50;

        // --- LES AUTRES JOUEURS (Synchronisés par le réseau) ---
        foreach (Program.RemotePlayer rp in Program.remotePlayers.Values)
        {
            string pingTexte = "-";
            Color pingCouleur = Color.LightGray;
            if (Program.isServer && Program.peersParId.TryGetValue(rp.Id, out NetPeer sonPeer))
            {
                pingTexte = sonPeer.Ping.ToString();
                pingCouleur = sonPeer.Ping < 80 ? Color.Green : Color.Red;
            }

            Raylib.DrawText(rp.Name, scoreX + 20, rowY, 30, Color.LightGray);
            Raylib.DrawText(rp.Kills.ToString(), scoreX + 300, rowY, 30, Color.LightGray);
            Raylib.DrawText(rp.Deaths.ToString(), scoreX + 400, rowY, 30, Color.LightGray);
            Raylib.DrawText(pingTexte, scoreX + 490, rowY, 30, pingCouleur);

            // Phase 3 : bouton d'EXCLUSION (hôte uniquement). La suppression effective
            // se fait au prochain PollEvents (PeerDisconnectedEvent) -> pas de modif
            // de collection pendant l'itération.
            if (Program.isServer && DrawButton(scoreX + scoreWidth - 42, rowY, 34, 34, "X"))
            {
                Program.PlaySoundWithPriority(unselect, Program.SoundPriority.Low);
                Program.KickJoueur(rp.Id, NetConfig.RejetKick);
            }
            rowY += 50;

            if (rowY > scoreY + scoreHeight - 40) break; // On déborde pas du tableau
        }
    }



    // Type de filtre de saisie pour ChampTexteNav.
    public enum FiltreTexte { Libre, IP, CodeAlnum, MotDePasse }

    // Champ de texte navigable : SOURIS + CLAVIER + MANETTE.
    // - Focalisable à la flèche/stick ; Entrée / A pour ENTRER dedans.
    // - En édition : la navigation du menu est gelée (MenuNav.EditingText) pour taper
    //   librement ; Entrée / Échap (ou un clic ailleurs) pour SORTIR.
    public static void ChampTexteNav(Rectangle box, int id, ref string valeur, string placeholder, int maxLen, FiltreTexte filtre, int fontSize = 26)
    {
        int myItemId = MenuNav.NextId;
        bool focused = MenuNav.Item(box, out bool active);
        bool etaitActif = (activeTextBox == id);

        // Entrer dans le champ : validation (Entrée/A) ou clic souris quand il est focalisé.
        bool justEntered = false;
        if (active && !etaitActif) { activeTextBox = id; justEntered = true; Program.PlaySoundWithPriority(select, Program.SoundPriority.Low); }
        bool editing = (activeTextBox == id);

        // Le focus visuel suit le champ réellement édité (après un TAB), MAIS uniquement au
        // clavier/manette. En mode souris on ne force rien : le curseur reste prioritaire,
        // reste visible et peut cliquer où il veut (un clic hors du champ le referme).
        if (editing && !focused && !MenuNav.UsingMouse) { MenuNav.SetFocus(myItemId); focused = true; }

        // Fond + bordure : Or = en édition, Blanc = focalisé (nav), Noir sinon.
        Raylib.DrawRectangleRec(box, new Color(40, 40, 40, 255));
        Color bord = editing ? Color.Gold : (focused ? Color.White : Color.Black);
        Raylib.DrawRectangleLinesEx(box, 3, bord);

        // Texte saisi (ou placeholder grisé).
        string affichage = valeur.Length > 0 ? (filtre == FiltreTexte.MotDePasse ? new string('*', valeur.Length) : valeur) : placeholder;
        Color couleurTexte = valeur.Length > 0 ? Color.White : Color.Gray;
        int ty = (int)box.Y + (int)box.Height / 2 - fontSize / 2;
        Raylib.DrawText(affichage, (int)box.X + 15, ty, fontSize, couleurTexte);

        if (editing)
        {
            MenuNav.EditingText = true; // gèle la navigation tant qu'on écrit

            int k = Raylib.GetCharPressed();
            while (k > 0)
            {
                char c = (char)k;
                bool ok = filtre switch
                {
                    FiltreTexte.IP        => (c >= '0' && c <= '9') || c == '.',
                    FiltreTexte.CodeAlnum => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'),
                    _                     => (k >= 32 && k <= 125)
                };
                if (ok && valeur.Length < maxLen)
                    valeur += (filtre == FiltreTexte.CodeAlnum) ? char.ToUpperInvariant(c) : c;
                k = Raylib.GetCharPressed();
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && valeur.Length > 0)
                valeur = valeur.Substring(0, valeur.Length - 1);

            // Curseur clignotant
            if (((int)(Raylib.GetTime() * 2)) % 2 == 0)
            {
                int cx = (int)box.X + 15 + (valeur.Length > 0 ? Raylib.MeasureText(affichage, fontSize) : 0) + 3;
                Raylib.DrawRectangle(cx, ty, 3, fontSize + 4, Color.White);
            }

            // Sortir du champ : Entrée / Échap (mais pas la frame d'entrée), ou clic hors du champ.
            bool clicDehors = MenuNav.UsingMouse && Raylib.IsMouseButtonPressed(MouseButton.Left) && !Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), box);
            if (!justEntered && (MenuNav.Confirm || MenuNav.Back || clicDehors))
                activeTextBox = 0;
        }
    }

    // Fonction utilitaire pour dessiner un bouton cliquable.
    // Navigable à la SOURIS, au CLAVIER (flèches + Entrée) et à la MANETTE : le bouton
    // focalisé s'affiche exactement comme s'il était survolé par la souris.
    public static bool DrawButton(int x, int y, int width, int height, string text)
    {
        Rectangle buttonRec = new Rectangle(x, y, width, height);
        bool isHovered = MenuNav.Item(buttonRec, out bool isClicked);

        // Couleur : Gris clair si actif (survolé/focalisé), Gris foncé sinon
        Color buttonColor = isHovered ? Color.LightGray : Color.DarkGray;
        Color textColor = isHovered ? Color.Black : Color.RayWhite;

        Raylib.DrawRectangleRec(buttonRec, buttonColor);
        Raylib.DrawRectangleLinesEx(buttonRec, 2, Color.Black); // Bordure

        // Centrer le texte
        int textWidth = Raylib.MeasureText(text, 20);
        Raylib.DrawText(text, x + (width / 2) - (textWidth / 2), y + (height / 2) - 10, 20, textColor);

        return isClicked; // true à la frame où on valide (clic relâché OU Entrée/A si focalisé)
    }


    public static void Draw()
    {

        // NOUVEAU : On force la musique du menu ici pour TOUS les écrans du menu !
        SetActiveMusic(ActiveMusic.Menu);
        UpdateActiveMusicStream();

        Vector2 souris = Raylib.GetMousePosition();

        // Navigation clavier/manette + curseur auto pour TOUS les écrans de menu.
        // (screenId distinct par écran/onglet -> le focus repart du 1er bouton en changeant d'écran)
        int navScreenId = (int)Program.currentState * 1000;
        if (Program.currentState == Program.GameState.Options) navScreenId += ongletOptionActif;
        // (Skin : on NE remet PAS le focus en changeant d'onglet -> MenuCustomization gère le focus
        //  lui-même pour rester dans la catégorie validée.)
        MenuNav.Begin(navScreenId, Program.currentState == Program.GameState.Options);

        Vector2 BackgroundPos = new Vector2(0, 0);
        float rotation = 0.0f;

        // --- GESTION DES CLICS (Tes particules) ---
        if (Raylib.IsMouseButtonPressed(MouseButton.Left)) 
        {
            int randValue = Raylib.GetRandomValue(1, 3);
            Texture2D textureChoisie = clic1;
            if (randValue == 2) textureChoisie = clic2;
            if (randValue == 3) textureChoisie = clic3;

            Vector2 tailleImage = new Vector2(textureChoisie.Width * 0.2f, textureChoisie.Height * 0.2f);
            Vector2 positionCentree = Raylib.GetMousePosition() - (tailleImage / 2f);
            ListeEffets.Add(new EffetClic(positionCentree, textureChoisie));
        }

        Raylib.BeginDrawing();

        // ==========================================
        // LE ROUTEUR VISUEL
        // ==========================================
        switch (Program.currentState)
        {
            case Program.GameState.MainMenu:
                // --- 1. TON MENU PRINCIPAL ---
                Raylib.ClearBackground(Color.DarkGray);
                Raylib.DrawTextureEx(background, BackgroundPos, rotation, 1f, Color.White);

                // Animation du logo
                int posX_logo = Raylib.GetScreenHeight() - 140;
                int posY_logo = 250;
                if (agrandissement) {
                    echelle += 0.0002f;
                    if (echelle >= 0.21f) agrandissement = false;
                } else {
                    echelle -= 0.0002f;
                    if (echelle <= 0.2f) agrandissement = true;
                }
                Vector2 posLogo = new Vector2(posX_logo - (Logo.Width * echelle / 2), posY_logo - (Logo.Height * echelle / 2));
                Raylib.DrawTextureEx(Logo, posLogo, rotation, echelle, Color.White);

                // Positions de tes boutons originaux
                float echelleBoutons = 0.2f;
                float echelleBoutonActif = 0.205f;
                int posX_boutons = Raylib.GetScreenHeight() / 2 + 250;
                Vector2 posPlay = new Vector2(posX_boutons, 450);
                Vector2 posOption = new Vector2(posX_boutons, 600);
                Vector2 posQuit = new Vector2(posX_boutons, 750);
                Vector2 ajustement = new Vector2(0, 20);
                
                Rectangle boxPlay = new Rectangle(posPlay + ajustement, 300, 120);
                Rectangle boxOption = new Rectangle(posOption + ajustement, 300, 120);
                Rectangle boxQuit = new Rectangle(posQuit + ajustement, 300, 120);

                // Bouton PLAY
                if (MenuNav.Item(boxPlay, out bool clicPlay)) {
                    Raylib.DrawTextureEx(play_active, posPlay - new Vector2(5, 5), rotation, echelleBoutonActif, Color.White);
                    if (clicPlay) {
                        Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                        Program.currentState = Program.GameState.ModeSelection; // On avance !
                    }
                } else Raylib.DrawTextureEx(play_button, posPlay, rotation, echelleBoutons, Color.White);

                // Bouton OPTION
                if (MenuNav.Item(boxOption, out bool clicOption)) {
                    Raylib.DrawTextureEx(option_active, posOption - new Vector2(5, 5), rotation, echelleBoutonActif, Color.White);
                    if (clicOption) {
                        Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                        Program.currentState = Program.GameState.Options;
                    }
                } else Raylib.DrawTextureEx(option_button, posOption, rotation, echelleBoutons, Color.White);

                // Bouton QUIT
                if (MenuNav.Item(boxQuit, out bool clicQuit)) {
                    Raylib.DrawTextureEx(quit_active, posQuit + new Vector2(10, 5), rotation, 0.190f, Color.White);
                    if (clicQuit) Environment.Exit(0);
                } else Raylib.DrawTextureEx(quit_button, posQuit, rotation, echelleBoutons, Color.White);
                break;

            case Program.GameState.Options:
                // --- 2. TES OPTIONS (On garde ta fonction qui marche très bien) ---
                string dummy = "";
                AfficherMenuOptions(ref dummy);
                // (Note : Si tu cliques "RETOUR" dans tes options, la logique a besoin d'une petite MAJ, voir plus bas)
                break;

            case Program.GameState.ModeSelection:
                // --- 3. CHOIX SOLO / MULTI (Sur ton magnifique fond flou) ---
                Raylib.DrawTextureEx(BlurBackground, BackgroundPos, 0f, 1f, Color.White);
                
                int centerX = Raylib.GetScreenWidth() / 2;
                int centerY = Raylib.GetScreenHeight() / 2;
                Raylib.DrawText("SÉLECTIONNEZ UN MODE", centerX - Raylib.MeasureText("SÉLECTIONNEZ UN MODE", 40)/2, 200, 40, Color.Black);

                if (DrawButton(centerX - 150, centerY - 80, 300, 60, "SOLO")) {
                    Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                    Program.currentState = Program.GameState.ChoiceMap; // Renvoie vers ta sélection de map !
                }
                if (DrawButton(centerX - 150, centerY + 20, 300, 60, "MULTIJOUEUR (LAN)")) {
                    Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                    Program.currentState = Program.GameState.NetworkHub;
                }
                if (DrawButton(centerX - 150, centerY + 120, 300, 60, "SKIN")) {
                    Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                    Program.currentState = Program.GameState.Customization;
                }
                if (DrawButton(50, 50, 150, 60, "RETOUR") || (MenuNav.Back && !MenuNav.EditingText)) {
                    Program.PlaySoundWithPriority(unselect, Program.SoundPriority.Low);
                    Program.currentState = Program.GameState.MainMenu;
                }
                break;

            case Program.GameState.ChoiceMap:
                // --- 4. TA SÉLECTION DE MAP ---
                ChoiceMap();
                break;

            case Program.GameState.Customization:
                // --- LE VESTIAIRE : personnalisation du personnage ---
                Program.MenuCustomization();
                break;

            case Program.GameState.NetworkHub:
                // --- 5. LE HUB RÉSEAU ---
                Raylib.DrawTextureEx(BlurBackground, BackgroundPos, 0f, 1f, Color.White);
                Raylib.DrawText("RÉSEAU", Raylib.GetScreenWidth()/2 - 90, 150, 40, Color.Black);

                // La dernière erreur réseau (rejet version/plein, timeout, perçage NAT...) — S7
                if (!string.IsNullOrEmpty(Program.derniereErreurReseau))
                {
                    string msgErr = Program.derniereErreurReseau;
                    int largErr = Raylib.MeasureText(msgErr, 24);
                    Raylib.DrawText(msgErr, Raylib.GetScreenWidth()/2 - largErr/2 + 1, 211, 24, Color.Black);
                    Raylib.DrawText(msgErr, Raylib.GetScreenWidth()/2 - largErr/2, 210, 24, Color.Red);
                }

                // Mise en page aérée : une seule colonne centrée, éléments larges et espacés.
                int hubCX = Raylib.GetScreenWidth() / 2;
                int colW = 390;               // 520 * 0.75 : boutons du hub réseau réduits
                int colX = hubCX - colW / 2;
                int hy = 250;
                int bh = 50;                  // 66 * 0.75

                // 1) HÉBERGER
                if (DrawButton(colX, hy, colW, bh, "HÉBERGER")) {
                    Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                    Program.currentState = Program.GameState.CreateMatch;
                }
                hy += bh + 55;

                // 2) REJOINDRE PAR CODE
                Raylib.DrawText("REJOINDRE PAR CODE", colX, hy, 24, Color.Black);
                hy += 38;
                ChampTexteNav(new Rectangle(colX, hy, colW, 44), 9, ref Program.codeSaisi, "CODE", 5, FiltreTexte.CodeAlnum, 26);
                hy += 44 + 18;
                ChampTexteNav(new Rectangle(colX, hy, colW, 40), 10, ref Program.joinPassword, "Mot de passe (si besoin)", 20, FiltreTexte.MotDePasse, 22);
                hy += 40 + 18;
                // TAB : passer au champ suivant (CODE <-> Mot de passe)
                if (Raylib.IsKeyPressed(KeyboardKey.Tab))
                {
                    if (Program.activeTextBox == 9) Program.activeTextBox = 10;
                    else if (Program.activeTextBox == 10) Program.activeTextBox = 9;
                }
                if (DrawButton(colX, hy, colW, bh, "REJOINDRE")) {
                    if (Program.codeSaisi.Length == 5)
                    {
                        Program.PlaySoundWithPriority(select, Program.SoundPriority.High);
                        Program.AllumerMoteurReseau(false);
                        Program.ClientRejoindreParCode(Program.codeSaisi);
                    }
                }
                // État du perçage NAT (feedback en direct)
                string etatTxt = Program.etatJoin switch
                {
                    Program.EtatJoin.Resolution => "Résolution du code...",
                    Program.EtatJoin.Percage => "Perçage du NAT...",
                    Program.EtatJoin.Connexion => "Connexion à l'hôte...",
                    _ => ""
                };
                if (etatTxt != "") Raylib.DrawText(etatTxt, colX, hy + bh + 8, 18, Color.Gold);
                hy += bh + 58;

                // 3) RÉSEAU LOCAL / IP directe
                Raylib.DrawText("OU EN RÉSEAU LOCAL", colX, hy, 24, Color.Black);
                hy += 38;
                if (DrawButton(colX, hy, colW, bh, "REJOINDRE (LAN / IP)")) {
                    Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                    Program.AllumerMoteurReseau(false);
                    Program.currentState = Program.GameState.ServerBrowser;
                }

                if (DrawButton(50, 50, 150, 60, "RETOUR") || (MenuNav.Back && !MenuNav.EditingText)) {
                    Program.PlaySoundWithPriority(unselect, Program.SoundPriority.Low);
                    Program.currentState = Program.GameState.ModeSelection;
                }
                break;
            

            case Program.GameState.ServerBrowser:
                Raylib.DrawTextureEx(BlurBackground, BackgroundPos, 0f, 1f, Color.White);
                
                // On utilise les mêmes centres que pour CreateMatch
                int centerC = Raylib.GetScreenWidth() / 2;
                int centerCY = Raylib.GetScreenHeight() / 2;

                Raylib.DrawText("REJOINDRE UN MATCH", centerC - Raylib.MeasureText("REJOINDRE UN MATCH", 40)/2, centerCY - 200, 40, Color.White);

                // --- LES BOÎTES DE TEXTE (Centrées, navigables) ---
                // Flèches/stick pour atteindre un champ, Entrée/A pour y entrer (la nav se fige),
                // Entrée/Échap pour en sortir.
                ChampTexteNav(new Rectangle(centerC - 300, centerCY - 100, 600, 50), 1, ref Program.playerNameInput, "Entrez votre Pseudo", 15, FiltreTexte.Libre);
                ChampTexteNav(new Rectangle(centerC - 300, centerCY - 10, 600, 50), 2, ref Program.directIPInput, "Entrez l'IP du serveur", 15, FiltreTexte.IP);
                // TAB : passer au champ suivant (Pseudo <-> IP)
                if (Raylib.IsKeyPressed(KeyboardKey.Tab))
                {
                    if (Program.activeTextBox == 1) Program.activeTextBox = 2;
                    else if (Program.activeTextBox == 2) Program.activeTextBox = 1;
                }

                // --- BOUTON VALIDER ---
                if (DrawButton(centerC - 120, centerCY + 80, 240, 50, "REJOINDRE"))
                {
                    Program.PlaySoundWithPriority(select, Program.SoundPriority.High);
                    try
                    {
                        IPAddress ipCible = IPAddress.Parse(Program.directIPInput);
                        IPEndPoint endPointCible = new IPEndPoint(ipCible, 7777);

                        Console.WriteLine($"[RÉSEAU] Tentative de connexion directe Unicast vers {endPointCible}");
                        Program.ConnecterAHote(endPointCible);
                        lock (Program._lobbyLock) { Program.currentLobbyPlayers.Clear(); }
                        Program.currentState = Program.GameState.Lobby;
                    }
                    catch
                    {
                        // Si l'IP est mal tapée on vide pour éviter un crash
                        Program.directIPInput = "";
                    }
                }

                // ==========================================
                // NOUVEAU : LA LISTE DES SALONS DÉTECTÉS PAR LE RADAR
                // (Le broadcast tourne déjà dans la boucle principale, ici on AFFICHE enfin le résultat !)
                // ==========================================
                int listeY = centerCY + 160;
                string titreListe = serveursDisponibles.Count > 0 ? "SALONS DÉTECTÉS SUR LE RÉSEAU :" : "Recherche de salons sur le réseau...";
                Raylib.DrawText(titreListe, centerC - Raylib.MeasureText(titreListe, 22) / 2, listeY, 22, Color.White);
                listeY += 35;

                var salons = serveursDisponibles.Values.ToList();
                for (int s = 0; s < salons.Count && s < 4; s++)
                {
                    var salon = salons[s];
                    string ligneSalon = $"{salon.NomDuSalon}  ({salon.JoueursActuels}/{salon.JoueursMax})  -  {salon.EndPoint.Address}";
                    if (DrawButton(centerC - 300, listeY + s * 60, 600, 50, ligneSalon))
                    {
                        Program.PlaySoundWithPriority(select, Program.SoundPriority.High);
                        Console.WriteLine($"[RÉSEAU] Connexion au salon '{salon.NomDuSalon}' sur {salon.EndPoint}");
                        Program.ConnecterAHote(new IPEndPoint(salon.EndPoint.Address, NetConfig.GamePort));
                        lock (Program._lobbyLock) { Program.currentLobbyPlayers.Clear(); }
                        Program.currentState = Program.GameState.Lobby;
                    }
                }

                // --- BOUTON RETOUR ---
                if (DrawButton(50, 50, 150, 60, "RETOUR") || (MenuNav.Back && !MenuNav.EditingText)) {
                    Program.PlaySoundWithPriority(unselect, Program.SoundPriority.Low);
                    Program.CouperReseau();
                    Program.currentState = Program.GameState.NetworkHub;
                }
                break;


            
            case Program.GameState.Lobby:
                Raylib.DrawTextureEx(BlurBackground, BackgroundPos, 0f, 1f, Color.White);
                
                // 1. AFFICHAGE DU NOM DE LA PARTIE (Dynamique)
                string titreSalon = $"{Program.hostMatchName.ToUpper()}";
                Raylib.DrawText(titreSalon, Raylib.GetScreenWidth()/2 - Raylib.MeasureText(titreSalon, 40)/2, 60, 40, Color.Black);

                // 1bis. LE CODE DE SALON (Phase 2) — gros, pour le dicter à ses amis
                string ligneCode;
                Color couleurCode;
                switch (Program.etatSalon)
                {
                    case Program.EtatSalon.Actif:   ligneCode = $"CODE : {Program.roomCode}"; couleurCode = Color.Gold; break;
                    case Program.EtatSalon.Creation: ligneCode = "Enregistrement du salon..."; couleurCode = Color.LightGray; break;
                    case Program.EtatSalon.Echec:   ligneCode = "Serveur maître injoignable (LAN / IP directe seulement)"; couleurCode = Color.Orange; break;
                    default:                         ligneCode = ""; couleurCode = Color.White; break;
                }
                if (ligneCode != "")
                    Raylib.DrawText(ligneCode, Raylib.GetScreenWidth()/2 - Raylib.MeasureText(ligneCode, 30)/2, 105, 30, couleurCode);

                // ==========================================
                // BLOC GAUCHE : LE CHOIX DE LA MAP (D'après ta maquette)
                // ==========================================
                Rectangle zoneMap = new Rectangle(80, 150, Raylib.GetScreenWidth() / 2 + 100, 500);
                Raylib.DrawRectangleRec(zoneMap, new Color(50, 50, 50, 220));
                Raylib.DrawRectangleLinesEx(zoneMap, 3, Color.Black);
                Raylib.DrawText("CHOIX MAP", (int)zoneMap.X + (int)zoneMap.Width/2 - 60, (int)zoneMap.Y + 20, 24, Color.White);

                // Rendu des vignettes de tes maps préchargées
                Texture2D[] vignettes = { ImageMapTest, ImageMapVille, Imageville2, ImageMapBlocs };
                int vignetteW = 160;
                int vignetteH = 110;
                int startVignetteX = (int)zoneMap.X + 40;
                int startVignetteY = (int)zoneMap.Y + 80;

                for (int i = 0; i < vignettes.Length; i++)
                {
                    Rectangle boxVignette = new Rectangle(startVignetteX + (i * (vignetteW + 30)), startVignetteY, vignetteW, vignetteH);

                    // Seul l'hôte choisit la map -> seules ses vignettes sont focalisables au clavier/manette
                    bool focusVig = false, clicVig = false;
                    if (Program.isServer) focusVig = MenuNav.Item(boxVignette, out clicVig);

                    // Rendu de la texture forcée aux dimensions
                    Rectangle src = new Rectangle(0, 0, vignettes[i].Width, vignettes[i].Height);
                    Raylib.DrawTexturePro(vignettes[i], src, boxVignette, Vector2.Zero, 0f, Color.White);

                    // Cadre : Rouge = map choisie, Blanc = focalisée (clavier/manette), Noir sinon
                    if (Program.mapChoisieIndex == i)
                        Raylib.DrawRectangleLinesEx(boxVignette, 4, Color.Red);
                    else if (focusVig)
                        Raylib.DrawRectangleLinesEx(boxVignette, 3, Color.White);
                    else
                        Raylib.DrawRectangleLinesEx(boxVignette, 2, Color.Black);

                    if (clicVig)
                    {
                        Raylib.PlaySound(select);
                        Program.mapChoisieIndex = i;
                        Program.MettreAJourEtDiffuserLobby(); // On synchronise les clients
                    }
                }

                // ==========================================
                // BLOC DROIT : LA LISTE DES JOUEURS (Vrais pseudos + Pastilles)
                // ==========================================
                Rectangle zoneJoueurs = new Rectangle(Raylib.GetScreenWidth() / 2 + 220, 150, 400, 500);
                Raylib.DrawRectangleRec(zoneJoueurs, new Color(40, 40, 40, 240));
                Raylib.DrawRectangleLinesEx(zoneJoueurs, 3, Color.Black);

                int playerY = (int)zoneJoueurs.Y + 20;
                
                // Rendu de la vraie liste synchronisée par le réseau !
                lock (_lobbyLock)
                {
                    for (int i = 0; i < Program.currentLobbyPlayers.Count; i++)
                    {
                        var joueur = Program.currentLobbyPlayers[i];
                        string texteJoueur = joueur.Name + (joueur.IsHost ? " (Host)" : "");
                        
                        // Ligne de séparation sous le texte
                        Raylib.DrawText(texteJoueur, (int)zoneJoueurs.X + 20, playerY, 22, Color.White);
                        Raylib.DrawLine((int)zoneJoueurs.X + 20, playerY + 30, (int)zoneJoueurs.X + 380, playerY + 30, Color.DarkGray);

                        // Pastille de Statut (Vert = Prêt, Rouge = Pas prêt)
                        Color couleurPastille = joueur.IsReady ? Color.Green : Color.Red;
                        Raylib.DrawCircle((int)zoneJoueurs.X + 360, playerY + 12, 8, couleurPastille);

                        playerY += 50;
                    }
                }

                // ==========================================
                // BOUTON DE SÉCURITÉ : AFFICHER / CACHER L'IP
                // (CORRECTION : sorti de la boucle des joueurs ! Avant, il était dessiné
                // une fois PAR joueur au même endroit : un clic le basculait plusieurs fois)
                // ==========================================
                if (Program.isServer)
                {
                    int ipBtnX = (int)zoneJoueurs.X + ((int)zoneJoueurs.Width / 2) - 100; // Centré sous le bloc droit
                    int ipBtnY = (int)zoneJoueurs.Y + (int)zoneJoueurs.Height + 20;

                    // Le texte du bouton s'adapte dynamiquement
                    string texteBoutonIP = Program.afficherIPDuServeur ? "MASQUER L'IP" : "IP SERVER";

                    if (DrawButton(ipBtnX, ipBtnY, 200, 40, texteBoutonIP))
                    {
                        Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                        // Inversion de l'état : si c'était vrai ça devient faux, et inversement
                        Program.afficherIPDuServeur = !Program.afficherIPDuServeur;
                    }

                    // Si le joueur a cliqué pour révéler, on affiche le texte en blanc brillant en dessous
                    if (Program.afficherIPDuServeur)
                    {
                        int ipTxtW = Raylib.MeasureText(Program.adresseIPLocaleServeur, 26);
                        Raylib.DrawText(Program.adresseIPLocaleServeur, ((int)zoneJoueurs.X + (int)zoneJoueurs.Width / 2) - ipTxtW / 2, ipBtnY + 55, 26, Color.White);
                    }
                }


                // ==========================================
                // LES BOUTONS D'ACTION (En bas)
                // ==========================================
                
                // Bouton OPTIONS (Commun)
                if (DrawButton((int)zoneMap.X, (int)zoneMap.Y + (int)zoneMap.Height + 30, 250, 60, "OPTIONS"))
                {
                    Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                    isOptionsMenuOpen = true;
                }

                // Bouton central d'action (LANCER pour l'host, PRÊT pour le client)
                if (Program.isServer)
                {
                    // --- PANNEAU "RÈGLES DU MATCH" (réglé par l'hôte) ---
                    int panW = 560, panH = 90;
                    int panX = Raylib.GetScreenWidth() / 2 - panW / 2;
                    int panY = (int)zoneMap.Y + (int)zoneMap.Height + 25;
                    Raylib.DrawRectangle(panX, panY, panW, panH, new Color(30, 30, 30, 230));
                    Raylib.DrawRectangleLinesEx(new Rectangle(panX, panY, panW, panH), 2, new Color(120, 120, 120, 255));
                    Raylib.DrawText("RÈGLES DU MATCH", panX + panW / 2 - Raylib.MeasureText("RÈGLES DU MATCH", 20) / 2, panY + 8, 20, Color.Gold);

                    int rowY = panY + 42;
                    // Limite de kills : [ - ]  valeur  [ + ]
                    Raylib.DrawText("KILLS", panX + 30, rowY + 6, 22, Color.White);
                    if (DrawButton(panX + 120, rowY, 34, 34, "-")) Program.matchScoreLimitConfig = Math.Max(0, Program.matchScoreLimitConfig - 5);
                    string txtScore = Program.matchScoreLimitConfig == 0 ? "OFF" : Program.matchScoreLimitConfig.ToString();
                    Raylib.DrawText(txtScore, panX + 172 - Raylib.MeasureText(txtScore, 24) / 2 + 10, rowY + 6, 24, Color.Gold);
                    if (DrawButton(panX + 200, rowY, 34, 34, "+")) Program.matchScoreLimitConfig = Math.Min(100, Program.matchScoreLimitConfig + 5);

                    // Limite de temps (minutes) : [ - ]  valeur  [ + ]
                    Raylib.DrawText("TEMPS", panX + 300, rowY + 6, 22, Color.White);
                    if (DrawButton(panX + 390, rowY, 34, 34, "-")) Program.matchTimeLimitConfig = Math.Max(0, Program.matchTimeLimitConfig - 60);
                    string txtTemps = Program.matchTimeLimitConfig == 0 ? "OFF" : (Program.matchTimeLimitConfig / 60) + "m";
                    Raylib.DrawText(txtTemps, panX + 442 - Raylib.MeasureText(txtTemps, 24) / 2 + 10, rowY + 6, 24, Color.Gold);
                    if (DrawButton(panX + 470, rowY, 34, 34, "+")) Program.matchTimeLimitConfig = Math.Min(1800, Program.matchTimeLimitConfig + 60);

                    int cfgY = panY + panH + 10;

                    // L'hôte vérifie si tout le monde est prêt (sauf lui)
                    bool toutLeMondePret = Program.currentLobbyPlayers.Where(p => !p.IsHost).All(p => p.IsReady);

                    // Le bouton change de couleur visuellement si le lancement est possible
                    if (DrawButton(Raylib.GetScreenWidth() / 2 - 100, cfgY, 300, 60, toutLeMondePret ? "LANCER LA PARTIE" : "ATTENTE DES JOUEURS..."))
                    {
                        if (toutLeMondePret)
                        {
                            Program.PlaySoundWithPriority(select, Program.SoundPriority.Critical);
                            // Diffuse StartGame (avec les règles) + démarre chez l'hôte
                            Program.HoteDemarrerMatch();
                        }
                    }
                }
                else
                {
                    // Client : si un match tourne déjà chez l'hôte, on propose de le REJOINDRE (hot-join)
                    if (Program.partieEnCoursCoteHote)
                    {
                        if (DrawButton(Raylib.GetScreenWidth() / 2 - 100, (int)zoneMap.Y + (int)zoneMap.Height + 100, 300, 55, "REJOINDRE LA PARTIE"))
                        {
                            Program.PlaySoundWithPriority(select, Program.SoundPriority.High);
                            Program.ClientDemanderRejoindre();
                        }
                    }
                    // Code Client : Trouver notre propre état Prêt
                    // CORRECTION : on se cherche par notre ID unique (l'ancien code cherchait
                    // un pseudo commençant par "Joueur_" qui n'existe plus -> bouton cassé)
                    Program.LobbyPlayer moi;
                    lock (Program._lobbyLock) { moi = Program.currentLobbyPlayers.Find(p => p.Id == Program.myPlayerId); }
                    bool monEtatPret = moi != null ? moi.IsReady : false;
                    
                    string texteBoutonPret = monEtatPret ? "PRÊT (ANNULER)" : "METTRE PRÊT";
                    
                    if (DrawButton(Raylib.GetScreenWidth() / 2 - 100, (int)zoneMap.Y + (int)zoneMap.Height + 30, 300, 60, texteBoutonPret))
                    {
                        Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                        
                        Packets.ToggleReadyPacket readyPack = new Packets.ToggleReadyPacket();
                        readyPack.IsReady = !monEtatPret; // Inversion du statut

                        NetDataWriter writer = new NetDataWriter();
                        Program.netProcessor.Write(writer, readyPack);
                        
                        // Le client envoie son nouvel état au serveur
                        if (Program.netManager.FirstPeer != null)
                        {
                            Program.netManager.FirstPeer.Send(writer, DeliveryMethod.ReliableOrdered);
                        }
                    }
                }

                // Bouton QUITTER (Fermeture propre du socket) — bouton, ou Échap / B
                if (DrawButton(50, 50, 150, 60, "RETOUR") || MenuNav.Back)
                {
                    Program.PlaySoundWithPriority(unselect, Program.SoundPriority.Low);
                    Program.CouperReseau(); // Coupe la carte réseau proprement
                    Program.currentState = Program.GameState.NetworkHub;
                }

                // ==========================================
                // LOGIQUE DE TEST (Touche M / Semicolon)
                // ==========================================
                if (Program.isServer && Raylib.IsKeyPressed(KeyboardKey.Semicolon))
                {
                    Packets.JoinPacket fauxPasseport = new Packets.JoinPacket();
                    fauxPasseport.PlayerName = "Bot_KarlMike_" + Raylib.GetRandomValue(10, 99);
                    Program.OnJoinPacketReceived(fauxPasseport, null);
                }
                break;


            case Program.GameState.Loading:
                // Hot-join : on charge la map et on attend la baseline de l'hôte
                Raylib.ClearBackground(Color.Black);
                string txtCharge = "Connexion à la partie en cours...";
                Raylib.DrawText(txtCharge, Raylib.GetScreenWidth()/2 - Raylib.MeasureText(txtCharge, 30)/2, Raylib.GetScreenHeight()/2 - 15, 30, Color.White);
                string txtSous = "Synchronisation du monde (murs, scores, position)";
                Raylib.DrawText(txtSous, Raylib.GetScreenWidth()/2 - Raylib.MeasureText(txtSous, 20)/2, Raylib.GetScreenHeight()/2 + 30, 20, Color.Gray);
                break;

            case Program.GameState.PostMatch:
                // Écran de fin de match (scoreboard + gagnant + compte à rebours)
                Raylib.DrawTextureEx(BlurBackground, BackgroundPos, 0f, 1f, Color.White);
                Program.DessinerPostMatch();
                break;


            case Program.GameState.CreateMatch:
                Raylib.DrawTextureEx(BlurBackground, BackgroundPos, 0f, 1f, Color.White);
                int centerC2 = Raylib.GetScreenWidth() / 2;
                int centerCY2 = Raylib.GetScreenHeight() / 2;

                Raylib.DrawText("Créer un Match", centerC2 - Raylib.MeasureText("Créer un Match", 40)/2, centerCY2 - 200, 40, Color.White);

                // --- LES BOÎTES DE TEXTE (navigables) ---
                // Flèches/stick pour atteindre un champ, Entrée/A pour y entrer (la nav se fige),
                // Entrée/Échap pour en sortir.
                ChampTexteNav(new Rectangle(centerC2 - 300, centerCY2 - 120, 600, 50), 1, ref Program.playerNameInput, "Entrez votre Pseudo", 15, FiltreTexte.Libre);
                ChampTexteNav(new Rectangle(centerC2 - 300, centerCY2 - 50, 600, 50), 2, ref Program.matchNameInput, "Entrez le nom du match", 20, FiltreTexte.Libre);
                ChampTexteNav(new Rectangle(centerC2 - 300, centerCY2 + 20, 600, 50), 3, ref Program.hostRoomPassword, "Mot de passe (optionnel)", 20, FiltreTexte.MotDePasse);
                // TAB : champ suivant (Pseudo -> Match -> Mot de passe -> Pseudo)
                if (Raylib.IsKeyPressed(KeyboardKey.Tab) && Program.activeTextBox >= 1 && Program.activeTextBox <= 3)
                    Program.activeTextBox = (Program.activeTextBox % 3) + 1;

                // Bouton Créer
                if (DrawButton(centerC2 - 100, centerCY2 + 80, 200, 50, "Créer"))
                {
                    Program.PlaySoundWithPriority(select, Program.SoundPriority.High);
                    Program.hostMatchName = string.IsNullOrEmpty(Program.matchNameInput) ? "Partie sans nom" : Program.matchNameInput;

                    string pseudoFinal = string.IsNullOrEmpty(Program.playerNameInput) ? "Hôte_" + Raylib.GetRandomValue(10, 99) : Program.playerNameInput;
                    // Sécurité : la virgule et le point-virgule servent de séparateurs réseau
                    pseudoFinal = pseudoFinal.Replace(",", "").Replace(";", "");
                    Program.myLobbyName = pseudoFinal;

                    Program.AllumerMoteurReseau(true);
                    lock (Program._lobbyLock)
                    {
                        Program.currentLobbyPlayers.Clear();
                        // L'hôte porte TOUJOURS l'ID 0 (myPlayerId est déjà mis à 0 par AllumerMoteurReseau)
                        Program.currentLobbyPlayers.Add(new Program.LobbyPlayer { Name = pseudoFinal, IsHost = true, IsReady = true, Id = 0, Peer = null });
                    }

                    // Phase 2 : on enregistre le salon sur le serveur maître (code de salon).
                    // Si le maître est injoignable, la partie reste jouable en LAN + IP directe.
                    Program.HoteCreerSalon();

                    Program.currentState = Program.GameState.Lobby;
                }

                if (DrawButton(50, 50, 150, 60, "RETOUR") || (MenuNav.Back && !MenuNav.EditingText)) {
                    Program.PlaySoundWithPriority(unselect, Program.SoundPriority.Low);
                    Program.currentState = Program.GameState.NetworkHub;
                }
                break;

                
        }

        // --- AFFICHAGE DES EFFETS DE CLIC (Toujours par dessus) ---
        for (int i = ListeEffets.Count - 1; i >= 0; i--)
        {
            ListeEffets[i].Opacite -= 5;
            if (ListeEffets[i].Opacite <= 0) ListeEffets.RemoveAt(i);
            else 
            {
                Color couleurFondu = new Color(255, 255, 255, ListeEffets[i].Opacite);
                Raylib.DrawTextureEx(ListeEffets[i].Texture, ListeEffets[i].Position, 0.0f, 0.2f, couleurFondu);
            }
        }

        MenuNav.End(); // clôture la navigation menu de cette frame
        Raylib.EndDrawing();
        
    }

}