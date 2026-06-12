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
partial class Program
{
    // --- LES 9 COULEURS DU CORPS ---
    public static readonly string[] nomsCouleursSkin = { "Orange", "Rouge", "Bleu", "Vert", "Jaune", "Violet", "Rose", "Turquoise", "Noir" };
    public static readonly Color[] couleursSkin =
    {
        Color.Orange,
        new Color(220, 40, 40, 255),
        new Color(50, 110, 255, 255),
        new Color(40, 180, 70, 255),
        new Color(250, 220, 30, 255),
        new Color(150, 60, 220, 255),
        new Color(255, 110, 200, 255),
        new Color(0, 200, 210, 255),
        new Color(35, 35, 35, 255)
    };

    // --- LES 5 CHAPEAUX (équipables / déséquipables) ---
    public static readonly string[] nomsChapeaux = { "Haut-de-forme", "Cone de chantier", "Couronne royale", "Canard en plastique", "Casquette a helice" };

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
    public static void DessinerPersonnageComplet(Vector3 centre, float yaw, float pitch, int couleurIdx, int hatIdx, int faceIdx)
    {
        if (couleurIdx < 0 || couleurIdx >= couleursSkin.Length) couleurIdx = 0;

        Vector3 pointHaut = new Vector3(centre.X, centre.Y + 0.5f, centre.Z);
        Vector3 pointBas = new Vector3(centre.X, centre.Y - 0.5f, centre.Z);
        Raylib.DrawCapsule(pointHaut, pointBas, 0.5f, 8, 8, couleursSkin[couleurIdx]);
        Raylib.DrawCapsuleWires(pointHaut, pointBas, 0.5f, 8, 8, Color.Black);

        // Le visage, là où le joueur regarde (même ancrage que l'ancienne boule grise)
        Vector3 teteCentre = pointHaut + new Vector3(0, 0.2f, 0);
        DessinerFaceSkin(faceIdx, teteCentre, yaw, pitch);

        // Le chapeau : on lui donne le CENTRE du crâne (la sphère de rayon 0.5 en haut
        // de la capsule), chaque chapeau se place tout seul par rapport à ce repère.
        DessinerChapeauSkin(hatIdx, pointHaut, yaw);
    }

    // ==========================================
    // LES TÊTES — dessinées dans le repère local du regard (+Z = devant)
    // ==========================================
    public static void DessinerFaceSkin(int faceIdx, Vector3 teteCentre, float yaw, float pitch)
    {
        Rlgl.PushMatrix();
        Rlgl.Translatef(teteCentre.X, teteCentre.Y, teteCentre.Z);
        Rlgl.Rotatef(yaw * RadVersDeg, 0, 1, 0);
        Rlgl.Rotatef(-pitch * RadVersDeg, 1, 0, 0);

        switch (faceIdx)
        {
            case 0: // SMILEY
                Raylib.DrawSphere(new Vector3(0, 0, 0.42f), 0.18f, new Color(255, 215, 30, 255));
                Raylib.DrawSphere(new Vector3(-0.07f, 0.06f, 0.56f), 0.035f, Color.Black);
                Raylib.DrawSphere(new Vector3(0.07f, 0.06f, 0.56f), 0.035f, Color.Black);
                Raylib.DrawCube(new Vector3(-0.06f, -0.04f, 0.57f), 0.04f, 0.03f, 0.03f, Color.Black);
                Raylib.DrawCube(new Vector3(0, -0.07f, 0.575f), 0.06f, 0.03f, 0.03f, Color.Black);
                Raylib.DrawCube(new Vector3(0.06f, -0.04f, 0.57f), 0.04f, 0.03f, 0.03f, Color.Black);
                break;

            case 1: // CYCLOPE
                Raylib.DrawSphere(new Vector3(0, 0, 0.42f), 0.18f, Color.White);
                Raylib.DrawSphere(new Vector3(0, 0.02f, 0.55f), 0.075f, new Color(40, 120, 255, 255));
                Raylib.DrawSphere(new Vector3(0, 0.02f, 0.60f), 0.035f, Color.Black);
                break;

            case 2: // ROBOT
                Raylib.DrawCube(new Vector3(0, 0, 0.42f), 0.30f, 0.30f, 0.26f, new Color(120, 125, 135, 255));
                Raylib.DrawCubeWires(new Vector3(0, 0, 0.42f), 0.30f, 0.30f, 0.26f, Color.Black);
                Raylib.DrawCube(new Vector3(-0.08f, 0.04f, 0.56f), 0.06f, 0.05f, 0.02f, Color.Red);
                Raylib.DrawCube(new Vector3(0.08f, 0.04f, 0.56f), 0.06f, 0.05f, 0.02f, Color.Red);
                Raylib.DrawCube(new Vector3(0, -0.08f, 0.56f), 0.16f, 0.025f, 0.02f, new Color(255, 60, 60, 255));
                Raylib.DrawCylinder(new Vector3(0, 0.15f, 0.42f), 0.015f, 0.015f, 0.12f, 8, Color.DarkGray);
                Raylib.DrawSphere(new Vector3(0, 0.29f, 0.42f), 0.035f, Color.Red);
                break;

            case 3: // CITROUILLE
                Raylib.DrawSphere(new Vector3(0, 0, 0.42f), 0.19f, new Color(235, 120, 20, 255));
                Raylib.DrawCube(new Vector3(-0.07f, 0.05f, 0.58f), 0.055f, 0.055f, 0.03f, Color.Black);
                Raylib.DrawCube(new Vector3(0.07f, 0.05f, 0.58f), 0.055f, 0.055f, 0.03f, Color.Black);
                Raylib.DrawCube(new Vector3(0, -0.07f, 0.59f), 0.14f, 0.035f, 0.03f, Color.Black);
                Raylib.DrawCylinder(new Vector3(0, 0.17f, 0.42f), 0.025f, 0.04f, 0.09f, 8, new Color(60, 140, 50, 255));
                break;

            case 4: // CLOWN
                Raylib.DrawSphere(new Vector3(0, 0, 0.42f), 0.18f, Color.White);
                Raylib.DrawSphere(new Vector3(0, -0.01f, 0.585f), 0.06f, Color.Red);
                Raylib.DrawSphere(new Vector3(-0.07f, 0.07f, 0.55f), 0.035f, new Color(40, 120, 255, 255));
                Raylib.DrawSphere(new Vector3(0.07f, 0.07f, 0.55f), 0.035f, new Color(40, 120, 255, 255));
                Raylib.DrawCube(new Vector3(-0.07f, -0.08f, 0.555f), 0.04f, 0.03f, 0.03f, Color.Red);
                Raylib.DrawCube(new Vector3(0, -0.11f, 0.55f), 0.08f, 0.03f, 0.03f, Color.Red);
                Raylib.DrawCube(new Vector3(0.07f, -0.08f, 0.555f), 0.04f, 0.03f, 0.03f, Color.Red);
                break;

            default: // PAS DE TÊTE ÉQUIPÉE : la boule grise classique
                Raylib.DrawSphere(new Vector3(0, 0, 0.45f), 0.15f, Color.DarkGray);
                break;
        }

        Rlgl.PopMatrix();
    }

    // ==========================================
    // LES CHAPEAUX — dessinés dans le repère local du crâne
    // (origine = centre de la sphère du haut de la capsule, rayon 0.5, +Z = devant)
    // ==========================================
    public static void DessinerChapeauSkin(int hatIdx, Vector3 centreCrane, float yaw)
    {
        if (hatIdx < 0) return;

        Rlgl.PushMatrix();
        Rlgl.Translatef(centreCrane.X, centreCrane.Y, centreCrane.Z);
        Rlgl.Rotatef(yaw * RadVersDeg, 0, 1, 0);

        switch (hatIdx)
        {
            case 0: // HAUT-DE-FORME
                Color noirChapeau = new Color(25, 25, 25, 255);
                Raylib.DrawCylinder(new Vector3(0, 0.40f, 0), 0.42f, 0.42f, 0.05f, 18, noirChapeau);
                Raylib.DrawCylinder(new Vector3(0, 0.45f, 0), 0.26f, 0.26f, 0.45f, 18, noirChapeau);
                Raylib.DrawCylinder(new Vector3(0, 0.46f, 0), 0.27f, 0.27f, 0.10f, 18, Color.Red);
                break;

            case 1: // CÔNE DE CHANTIER
                Color orangeCone = new Color(245, 90, 20, 255);
                Raylib.DrawCylinder(new Vector3(0, 0.40f, 0), 0.34f, 0.34f, 0.05f, 16, orangeCone);
                Raylib.DrawCylinder(new Vector3(0, 0.45f, 0), 0.19f, 0.30f, 0.26f, 16, orangeCone);
                Raylib.DrawCylinder(new Vector3(0, 0.71f, 0), 0.14f, 0.185f, 0.10f, 16, Color.White);
                Raylib.DrawCylinder(new Vector3(0, 0.81f, 0), 0.02f, 0.135f, 0.20f, 16, orangeCone);
                break;

            case 2: // COURONNE ROYALE (elle entoure le crâne !)
                Color orRoyal = new Color(255, 200, 40, 255);
                Raylib.DrawCylinder(new Vector3(0, 0.18f, 0), 0.48f, 0.50f, 0.20f, 12, orRoyal);
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * MathF.Tau / 5f;
                    Raylib.DrawCylinder(new Vector3(MathF.Sin(angle) * 0.46f, 0.38f, MathF.Cos(angle) * 0.46f), 0.0f, 0.07f, 0.16f, 8, orRoyal);
                }
                Raylib.DrawSphere(new Vector3(0, 0.30f, 0.50f), 0.055f, Color.Red); // Le rubis, devant
                break;

            case 3: // CANARD EN PLASTIQUE
                Color jauneCanard = new Color(255, 225, 40, 255);
                Raylib.DrawSphere(new Vector3(0, 0.62f, -0.02f), 0.20f, jauneCanard);          // le corps
                Raylib.DrawSphere(new Vector3(0, 0.80f, 0.12f), 0.12f, jauneCanard);           // la tête
                Raylib.DrawCube(new Vector3(0, 0.78f, 0.27f), 0.10f, 0.045f, 0.10f, new Color(245, 130, 20, 255)); // le bec
                Raylib.DrawSphere(new Vector3(0, 0.68f, -0.19f), 0.07f, jauneCanard);          // la queue
                Raylib.DrawSphere(new Vector3(-0.05f, 0.84f, 0.195f), 0.02f, Color.Black);
                Raylib.DrawSphere(new Vector3(0.05f, 0.84f, 0.195f), 0.02f, Color.Black);
                break;

            case 4: // CASQUETTE À HÉLICE
                Raylib.DrawSphere(new Vector3(0, 0.32f, 0), 0.30f, new Color(220, 50, 50, 255));
                Raylib.DrawCylinder(new Vector3(0, 0.60f, 0), 0.02f, 0.02f, 0.14f, 8, Color.DarkGray);
                // L'hélice tourne toute seule avec le temps !
                Rlgl.Rotatef((float)(Raylib.GetTime() * 420.0 % 360.0), 0, 1, 0);
                Raylib.DrawCube(new Vector3(0, 0.76f, 0), 0.60f, 0.02f, 0.07f, new Color(250, 220, 30, 255));
                Raylib.DrawCube(new Vector3(0, 0.76f, 0), 0.07f, 0.02f, 0.60f, new Color(40, 120, 255, 255));
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
