using System;
using System.IO;
using System.Numerics;
using Raylib_cs;

// ========================================================
// LE SYSTÈME DE COSMÉTIQUES (Skins)
// ========================================================
// - 9 couleurs de corps, 5 chapeaux, 5 têtes (visages)
// - Tout est dessiné avec des primitives Raylib : aucun modèle 3D à charger !
// - Le choix du joueur est sauvegardé dans skin.cfg et voyage sur le réseau
//   via PlayerStatePacket : les autres joueurs voient donc notre skin en jeu.
//
// ANATOMIE DU PERSONNAGE :
// - Le corps est une capsule (rayon 0.5). La TÊTE est le dôme du haut
//   (la demi-sphère de rayon 0.5 centrée sur le sommet du segment).
// - Le visage est VERROUILLÉ sur la surface de ce dôme : il glisse dessus
//   en suivant le regard (yaw + pitch) mais ne peut jamais sortir du corps.
partial class Program
{
    // --- LES 9 COULEURS DU CORPS (palette sobre, pas flashy) ---
    public static readonly string[] nomsCouleursSkin = { "Orange", "Rouge", "Bleu", "Vert", "Gris clair", "Violet", "Rose", "Marron", "Noir" };
    public static readonly Color[] couleursSkin =
    {
        new Color(185, 95, 15, 255),    // orange foncé
        new Color(140, 30, 30, 255),    // rouge foncé
        new Color(35, 60, 140, 255),    // bleu foncé
        new Color(30, 105, 50, 255),    // vert foncé
        new Color(185, 185, 185, 255),  // gris clair
        new Color(90, 45, 130, 255),    // violet foncé
        new Color(170, 70, 120, 255),   // rose foncé
        new Color(85, 55, 35, 255),     // marron foncé
        new Color(35, 35, 35, 255)      // noir
    };

    // --- LES 5 CHAPEAUX (équipables / déséquipables) ---
    public static readonly string[] nomsChapeaux = { "Haut-de-forme", "Cone de chantier", "Couronne royale", "Masque de robot", "Casque de samourai" };

    // --- LES 5 TÊTES (remplacent la boule grise du regard) ---
    public static readonly string[] nomsFaces = { "Smiley", "Cyclope", "Robot", "Citrouille", "Clown" };

    // --- LE SKIN ACTUELLEMENT ÉQUIPÉ ---
    public static int skinCouleur = 0;  // index dans couleursSkin
    public static int skinHat = -1;     // -1 = aucun chapeau
    public static int skinFace = -1;    // -1 = la boule grise classique

    // --- ÉTAT DU MENU DE PERSONNALISATION ---
    static int ongletCustom = 0;        // 0 = Couleur, 1 = Chapeau, 2 = Tête
    static float previewYaw = 0.6f;     // Rotation du perso dans l'aperçu (clic-glisser)

    const float RadVersDeg = 180f / MathF.PI;

    // ==========================================
    // SAUVEGARDE / CHARGEMENT DU SKIN (skin.cfg)
    // ==========================================
    public static void ChargerSkin()
    {
        try
        {
            if (!File.Exists("skin.cfg")) return;
            string[] parts = File.ReadAllText("skin.cfg").Split(';');
            skinCouleur = Math.Clamp(int.Parse(parts[0]), 0, couleursSkin.Length - 1);
            skinHat = Math.Clamp(int.Parse(parts[1]), -1, nomsChapeaux.Length - 1);
            skinFace = Math.Clamp(int.Parse(parts[2]), -1, nomsFaces.Length - 1);
        }
        catch { /* fichier corrompu : on garde le skin par défaut */ }
    }

    public static void SauvegarderSkin()
    {
        try { File.WriteAllText("skin.cfg", $"{skinCouleur};{skinHat};{skinFace}"); }
        catch { }
    }

    // ==========================================
    // LE DESSIN DU PERSONNAGE COMPLET
    // (Partagé entre l'aperçu du menu ET les joueurs distants en jeu)
    // ==========================================
    // 'lean' = penchement wall-run (le roll caméra du joueur). Purement visuel :
    // la capsule PHYSIQUE reste parfaitement droite (les capteurs de murs ne sont
    // donc jamais perturbés), seul le modèle 3D s'incline, plafonné à ±5°.
    public static void DessinerPersonnageComplet(Vector3 centre, float yaw, float pitch, int couleurIdx, int hatIdx, int faceIdx, float lean = 0f)
    {
        if (couleurIdx < 0 || couleurIdx >= couleursSkin.Length) couleurIdx = 0;

        Rlgl.PushMatrix();
        Rlgl.Translatef(centre.X, centre.Y, centre.Z);
        Rlgl.Rotatef(yaw * RadVersDeg, 0, 1, 0);
        // Dans ce repère local : +Z = devant le joueur, +X = sa droite

        // Penchement wall-run : on mappe le roll caméra (max ±0.25 rad) vers ±5° max
        float leanRad = Math.Clamp(lean * 0.35f, -0.0873f, 0.0873f);
        if (leanRad != 0f) Rlgl.Rotatef(leanRad * RadVersDeg, 0, 0, 1);

        // Le corps
        Raylib.DrawCapsule(new Vector3(0, 0.5f, 0), new Vector3(0, -0.5f, 0), 0.5f, 8, 8, couleursSkin[couleurIdx]);
        Raylib.DrawCapsuleWires(new Vector3(0, 0.5f, 0), new Vector3(0, -0.5f, 0), 0.5f, 8, 8, Color.Black);

        // Le visage, verrouillé sur le dôme de la tête
        DessinerFaceSkinLocale(faceIdx, pitch);

        // Le chapeau, posé sur le crâne
        DessinerChapeauSkinLocal(hatIdx);

        Rlgl.PopMatrix();
    }

    // ==========================================
    // LES TÊTES — verrouillées sur le dôme du crâne
    // Repère : origine = CENTRE du dôme (sommet du segment de la capsule),
    // +Z = regard horizontal. On pivote autour du centre du dôme, donc le
    // visage GLISSE sur la surface de la sphère et ne sort jamais du corps.
    // ==========================================
    static void DessinerFaceSkinLocale(int faceIdx, float pitch)
    {
        Rlgl.PushMatrix();
        Rlgl.Translatef(0, 0.5f, 0); // le centre du dôme (la "vraie" tête)

        // Le visage suit le regard, borné pour rester sur la partie avant du dôme
        float pitchFace = Math.Clamp(pitch, -0.95f, 0.95f);
        Rlgl.Rotatef(-pitchFace * RadVersDeg, 1, 0, 0);

        // Tous les éléments sont posés autour de (0, +0.10, ~0.42) : ce point est
        // à ~0.43 du centre du dôme (rayon 0.5), donc les visages affleurent
        // la surface, quel que soit le pitch. VERROUILLÉ !
        switch (faceIdx)
        {
            case 0: // SMILEY
                Raylib.DrawSphere(new Vector3(0, 0.10f, 0.40f), 0.18f, new Color(225, 185, 25, 255));
                Raylib.DrawSphere(new Vector3(-0.07f, 0.16f, 0.54f), 0.035f, Color.Black);
                Raylib.DrawSphere(new Vector3(0.07f, 0.16f, 0.54f), 0.035f, Color.Black);
                Raylib.DrawCube(new Vector3(-0.06f, 0.06f, 0.55f), 0.04f, 0.03f, 0.03f, Color.Black);
                Raylib.DrawCube(new Vector3(0, 0.03f, 0.555f), 0.06f, 0.03f, 0.03f, Color.Black);
                Raylib.DrawCube(new Vector3(0.06f, 0.06f, 0.55f), 0.04f, 0.03f, 0.03f, Color.Black);
                break;

            case 1: // CYCLOPE
                Raylib.DrawSphere(new Vector3(0, 0.10f, 0.40f), 0.18f, new Color(225, 225, 215, 255));
                Raylib.DrawSphere(new Vector3(0, 0.12f, 0.53f), 0.075f, new Color(35, 95, 180, 255));
                Raylib.DrawSphere(new Vector3(0, 0.12f, 0.58f), 0.035f, Color.Black);
                break;

            case 2: // ROBOT
                Raylib.DrawCube(new Vector3(0, 0.10f, 0.40f), 0.30f, 0.30f, 0.26f, new Color(120, 125, 135, 255));
                Raylib.DrawCubeWires(new Vector3(0, 0.10f, 0.40f), 0.30f, 0.30f, 0.26f, Color.Black);
                Raylib.DrawCube(new Vector3(-0.08f, 0.14f, 0.54f), 0.06f, 0.05f, 0.02f, new Color(200, 40, 40, 255));
                Raylib.DrawCube(new Vector3(0.08f, 0.14f, 0.54f), 0.06f, 0.05f, 0.02f, new Color(200, 40, 40, 255));
                Raylib.DrawCube(new Vector3(0, 0.02f, 0.54f), 0.16f, 0.025f, 0.02f, new Color(200, 50, 50, 255));
                Raylib.DrawCylinder(new Vector3(0, 0.25f, 0.40f), 0.015f, 0.015f, 0.12f, 8, Color.DarkGray);
                Raylib.DrawSphere(new Vector3(0, 0.39f, 0.40f), 0.035f, new Color(200, 40, 40, 255));
                break;

            case 3: // CITROUILLE
                Raylib.DrawSphere(new Vector3(0, 0.10f, 0.40f), 0.19f, new Color(200, 100, 15, 255));
                Raylib.DrawCube(new Vector3(-0.07f, 0.15f, 0.56f), 0.055f, 0.055f, 0.03f, Color.Black);
                Raylib.DrawCube(new Vector3(0.07f, 0.15f, 0.56f), 0.055f, 0.055f, 0.03f, Color.Black);
                Raylib.DrawCube(new Vector3(0, 0.03f, 0.57f), 0.14f, 0.035f, 0.03f, Color.Black);
                Raylib.DrawCylinder(new Vector3(0, 0.27f, 0.40f), 0.025f, 0.04f, 0.09f, 8, new Color(50, 110, 40, 255));
                break;

            case 4: // CLOWN
                Raylib.DrawSphere(new Vector3(0, 0.10f, 0.40f), 0.18f, new Color(225, 225, 215, 255));
                Raylib.DrawSphere(new Vector3(0, 0.09f, 0.565f), 0.06f, new Color(190, 30, 30, 255));
                Raylib.DrawSphere(new Vector3(-0.07f, 0.17f, 0.53f), 0.035f, new Color(35, 95, 180, 255));
                Raylib.DrawSphere(new Vector3(0.07f, 0.17f, 0.53f), 0.035f, new Color(35, 95, 180, 255));
                Raylib.DrawCube(new Vector3(-0.07f, 0.02f, 0.535f), 0.04f, 0.03f, 0.03f, new Color(190, 30, 30, 255));
                Raylib.DrawCube(new Vector3(0, -0.01f, 0.53f), 0.08f, 0.03f, 0.03f, new Color(190, 30, 30, 255));
                Raylib.DrawCube(new Vector3(0.07f, 0.02f, 0.535f), 0.04f, 0.03f, 0.03f, new Color(190, 30, 30, 255));
                break;

            default: // PAS DE TÊTE ÉQUIPÉE : la boule grise classique, sur le dôme
                Raylib.DrawSphere(new Vector3(0, 0.10f, 0.43f), 0.15f, Color.DarkGray);
                break;
        }

        Rlgl.PopMatrix();
    }

    // ==========================================
    // LES CHAPEAUX — posés sur le crâne (ne suivent PAS le pitch, comme un vrai chapeau)
    // Repère : origine = centre du dôme (rayon 0.5), +Z = devant le joueur.
    // Rappel : rayon du dôme à la hauteur h = sqrt(0.25 - h²)
    //   h=0.20 -> 0.46 | h=0.30 -> 0.40 | h=0.40 -> 0.30 | h=0.45 -> 0.22
    // ==========================================
    static void DessinerChapeauSkinLocal(int hatIdx)
    {
        if (hatIdx < 0) return;

        Rlgl.PushMatrix();
        Rlgl.Translatef(0, 0.5f, 0); // le centre du dôme

        switch (hatIdx)
        {
            case 0: // HAUT-DE-FORME
                Color noirChapeau = new Color(25, 25, 25, 255);
                Raylib.DrawCylinder(new Vector3(0, 0.40f, 0), 0.42f, 0.42f, 0.05f, 18, noirChapeau);
                Raylib.DrawCylinder(new Vector3(0, 0.45f, 0), 0.26f, 0.26f, 0.45f, 18, noirChapeau);
                Raylib.DrawCylinder(new Vector3(0, 0.46f, 0), 0.27f, 0.27f, 0.10f, 18, new Color(150, 25, 25, 255));
                break;

            case 1: // CÔNE DE CHANTIER
                Color orangeCone = new Color(200, 75, 15, 255);
                Raylib.DrawCylinder(new Vector3(0, 0.40f, 0), 0.34f, 0.34f, 0.05f, 16, orangeCone);
                Raylib.DrawCylinder(new Vector3(0, 0.45f, 0), 0.19f, 0.30f, 0.26f, 16, orangeCone);
                Raylib.DrawCylinder(new Vector3(0, 0.71f, 0), 0.14f, 0.185f, 0.10f, 16, new Color(225, 225, 220, 255));
                Raylib.DrawCylinder(new Vector3(0, 0.81f, 0), 0.02f, 0.135f, 0.20f, 16, orangeCone);
                break;

            case 2: // COURONNE ROYALE (haute et ajustée sur le crâne)
                Color orRoyal = new Color(210, 165, 40, 255);
                Color orFonce = new Color(170, 130, 30, 255);
                // L'anneau épouse le dôme vers son sommet (rayon du dôme à h=0.34 : ~0.37)
                Raylib.DrawCylinder(new Vector3(0, 0.34f, 0), 0.38f, 0.40f, 0.14f, 12, orRoyal);
                // Les pointes
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * MathF.Tau / 6f;
                    Raylib.DrawCylinder(new Vector3(MathF.Sin(angle) * 0.36f, 0.48f, MathF.Cos(angle) * 0.36f), 0.0f, 0.05f, 0.13f, 6, orFonce);
                }
                // Le rubis, devant
                Raylib.DrawSphere(new Vector3(0, 0.41f, 0.40f), 0.045f, new Color(160, 25, 35, 255));
                break;

            case 3: // MASQUE DE ROBOT (plaqué sur l'avant de la tête)
                Color metal = new Color(145, 150, 160, 255);
                Color metalFonce = new Color(105, 110, 120, 255);
                Color glow = new Color(40, 210, 230, 255);
                // La plaque frontale principale
                Raylib.DrawCube(new Vector3(0, 0.06f, 0.42f), 0.40f, 0.34f, 0.10f, metal);
                Raylib.DrawCubeWires(new Vector3(0, 0.06f, 0.42f), 0.40f, 0.34f, 0.10f, new Color(60, 65, 75, 255));
                // La plaque du front
                Raylib.DrawCube(new Vector3(0, 0.27f, 0.33f), 0.32f, 0.10f, 0.12f, metalFonce);
                // Les joues (retours sur les côtés)
                Raylib.DrawCube(new Vector3(-0.21f, 0.04f, 0.34f), 0.06f, 0.26f, 0.16f, metalFonce);
                Raylib.DrawCube(new Vector3(0.21f, 0.04f, 0.34f), 0.06f, 0.26f, 0.16f, metalFonce);
                // La visière lumineuse (les "yeux")
                Raylib.DrawCube(new Vector3(0, 0.12f, 0.475f), 0.30f, 0.05f, 0.02f, glow);
                // La grille de bouche
                Raylib.DrawCube(new Vector3(-0.07f, -0.05f, 0.475f), 0.025f, 0.08f, 0.02f, new Color(45, 48, 55, 255));
                Raylib.DrawCube(new Vector3(0, -0.05f, 0.475f), 0.025f, 0.08f, 0.02f, new Color(45, 48, 55, 255));
                Raylib.DrawCube(new Vector3(0.07f, -0.05f, 0.475f), 0.025f, 0.08f, 0.02f, new Color(45, 48, 55, 255));
                // Les boulons
                Raylib.DrawSphere(new Vector3(-0.21f, 0.16f, 0.40f), 0.028f, new Color(70, 75, 85, 255));
                Raylib.DrawSphere(new Vector3(0.21f, 0.16f, 0.40f), 0.028f, new Color(70, 75, 85, 255));
                // L'antenne
                Raylib.DrawCylinder(new Vector3(0.13f, 0.40f, 0.10f), 0.013f, 0.013f, 0.16f, 8, metalFonce);
                Raylib.DrawSphere(new Vector3(0.13f, 0.57f, 0.10f), 0.03f, new Color(190, 40, 40, 255));
                break;

            case 4: // CASQUE DE SAMOURAÏ (kabuto)
                Color laque = new Color(95, 25, 28, 255);       // rouge laqué sombre
                Color laqueFonce = new Color(70, 18, 20, 255);
                Color orSamourai = new Color(205, 160, 45, 255);
                // Le bol du casque (recouvre le sommet du crâne)
                Raylib.DrawSphere(new Vector3(0, 0.20f, 0), 0.48f, laque);
                // Le rebord
                Raylib.DrawCylinder(new Vector3(0, 0.28f, 0), 0.50f, 0.52f, 0.05f, 14, laqueFonce);
                // Le protège-nuque (shikoro) : 3 anneaux évasés, étagés vers l'arrière
                Raylib.DrawCylinder(new Vector3(0, 0.16f, -0.04f), 0.52f, 0.56f, 0.06f, 14, new Color(85, 22, 25, 255));
                Raylib.DrawCylinder(new Vector3(0, 0.09f, -0.06f), 0.56f, 0.60f, 0.06f, 14, new Color(75, 20, 22, 255));
                Raylib.DrawCylinder(new Vector3(0, 0.02f, -0.08f), 0.60f, 0.64f, 0.06f, 14, laqueFonce);
                // Le cimier doré (maedate) : un V en deux lames, sur le front
                Rlgl.PushMatrix();
                Rlgl.Translatef(0, 0.52f, 0.42f);
                Rlgl.Rotatef(28f, 0, 0, 1);
                Raylib.DrawCube(new Vector3(0, 0.13f, 0), 0.045f, 0.26f, 0.02f, orSamourai);
                Rlgl.PopMatrix();
                Rlgl.PushMatrix();
                Rlgl.Translatef(0, 0.52f, 0.42f);
                Rlgl.Rotatef(-28f, 0, 0, 1);
                Raylib.DrawCube(new Vector3(0, 0.13f, 0), 0.045f, 0.26f, 0.02f, orSamourai);
                Rlgl.PopMatrix();
                // L'attache du cimier + l'ornement central
                Raylib.DrawCube(new Vector3(0, 0.40f, 0.45f), 0.16f, 0.07f, 0.03f, orSamourai);
                Raylib.DrawSphere(new Vector3(0, 0.50f, 0.44f), 0.04f, orSamourai);
                // Le pommeau au sommet
                Raylib.DrawSphere(new Vector3(0, 0.69f, 0), 0.05f, orSamourai);
                break;
        }

        Rlgl.PopMatrix();
    }

    // ==========================================
    // LE MENU DE PERSONNALISATION (état Customization)
    // ==========================================
    public static void MenuCustomization()
    {
        // ClearBackground nettoie AUSSI le tampon de profondeur : indispensable
        // pour que l'aperçu 3D ne se batte pas avec la frame précédente.
        Raylib.ClearBackground(new Color(240, 240, 240, 255));
        Raylib.DrawTextureEx(BlurBackground, new Vector2(0, 0), 0f, 1f, Color.White);

        Vector2 souris = Raylib.GetMousePosition();

        // --- ROTATION DU PERSO AU CLIC-GLISSER (sur la moitié gauche de l'écran) ---
        if (Raylib.IsMouseButtonDown(MouseButton.Left) && souris.X < LargeurFenetre / 2f && souris.Y > 130)
        {
            previewYaw += Raylib.GetMouseDelta().X * 0.012f;
        }

        // --- L'APERÇU 3D DU PERSONNAGE ---
        // (Le Target est décalé en X pour que le perso apparaisse sur la gauche de l'écran)
        Camera3D camPreview = new Camera3D
        {
            Position = new Vector3(0.9f, 1.15f, 3.4f),
            Target = new Vector3(0.9f, 0.75f, 0f),
            Up = new Vector3(0, 1, 0),
            FovY = 45f,
            Projection = CameraProjection.Perspective
        };

        Raylib.BeginMode3D(camPreview);
        Raylib.DrawCylinder(new Vector3(0, -0.55f, 0), 1.0f, 1.1f, 0.12f, 24, new Color(70, 70, 70, 255)); // le socle
        DessinerPersonnageComplet(new Vector3(0, 0.6f, 0), previewYaw, 0f, skinCouleur, skinHat, skinFace);
        Raylib.EndMode3D();

        // --- TITRE & AIDE ---
        Raylib.DrawText("PERSONNALISATION", LargeurFenetre / 2 - Raylib.MeasureText("PERSONNALISATION", 40) / 2, 60, 40, Color.Black);
        Raylib.DrawText("Clique et glisse sur le personnage pour le faire tourner", 120, HauteurFenetre - 80, 20, Color.DarkGray);

        // --- LES 3 ONGLETS ---
        int panelX = LargeurFenetre / 2 + 80;
        int tabY = 170;
        string[] onglets = { "COULEUR", "CHAPEAU", "TETE" };
        for (int i = 0; i < onglets.Length; i++)
        {
            int tabX = panelX + i * 170;
            if (DrawButton(tabX, tabY, 160, 50, onglets[i]))
            {
                PlaySoundWithPriority(select, SoundPriority.Low);
                ongletCustom = i;
            }
            if (ongletCustom == i) Raylib.DrawRectangle(tabX, tabY + 52, 160, 6, Color.Red);
        }

        int itemY = 260;

        if (ongletCustom == 0)
        {
            // --- LA GRILLE 3x3 DE COULEURS ---
            for (int i = 0; i < couleursSkin.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                Rectangle swatch = new Rectangle(panelX + col * 110, itemY + row * 110, 90, 90);
                Raylib.DrawRectangleRec(swatch, couleursSkin[i]);

                bool hover = Raylib.CheckCollisionPointRec(souris, swatch);
                bool equipee = (skinCouleur == i);
                Raylib.DrawRectangleLinesEx(swatch, equipee ? 6 : 2, equipee ? Color.Lime : (hover ? Color.Red : Color.Black));

                if (hover && Raylib.IsMouseButtonReleased(MouseButton.Left))
                {
                    PlaySoundWithPriority(select, SoundPriority.Low);
                    skinCouleur = i;
                }
            }
            Raylib.DrawText("Couleur : " + nomsCouleursSkin[skinCouleur], panelX, itemY + 3 * 110 + 10, 25, Color.Black);
        }
        else
        {
            // --- LA LISTE DES CHAPEAUX OU DES TÊTES ---
            string[] noms = (ongletCustom == 1) ? nomsChapeaux : nomsFaces;
            int indexEquipe = (ongletCustom == 1) ? skinHat : skinFace;

            for (int i = 0; i < noms.Length; i++)
            {
                int y = itemY + i * 75;
                bool estEquipe = (indexEquipe == i);

                if (DrawButton(panelX, y, 420, 60, (estEquipe ? "[X] " : "") + noms[i]))
                {
                    PlaySoundWithPriority(select, SoundPriority.Low);
                    int nouvelIndex = estEquipe ? -1 : i; // re-cliquer sur l'objet équipé = le déséquiper
                    if (ongletCustom == 1) skinHat = nouvelIndex;
                    else skinFace = nouvelIndex;
                }
                if (estEquipe) Raylib.DrawRectangleLinesEx(new Rectangle(panelX - 4, y - 4, 428, 68), 4, Color.Lime);
            }
            Raylib.DrawText("Clique sur l'objet equipe [X] pour l'enlever", panelX, itemY + noms.Length * 75 + 10, 18, Color.DarkGray);
        }

        // --- RETOUR (avec sauvegarde du skin) ---
        if (DrawButton(50, 50, 150, 60, "RETOUR"))
        {
            PlaySoundWithPriority(unselect, SoundPriority.Low);
            SauvegarderSkin();
            currentState = GameState.ModeSelection;
        }
    }
}
