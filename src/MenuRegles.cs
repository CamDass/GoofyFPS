using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

// ========================================================
// LE PANNEAU "RÈGLES DU MATCH"
// ========================================================
// UN SEUL écran, utilisé à trois endroits :
//   - Salon (bouton RÈGLES)        -> editable = l'hôte uniquement
//   - Menu Paramètres, onglet 3    -> lecture seule (on consulte en pleine partie)
//   - Choix de map en solo         -> editable (c'est ta partie)
//
// Le panneau ne connaît AUCUNE règle en dur : il parcourt MatchRules.Liste. Chaque
// ligne porte un bouton (i) qui déplie l'explication du réglage en bas de l'écran.
// En lecture seule, les boutons [-] / [+] disparaissent : le joueur qui rejoint VOIT
// tout, mais ne peut rien changer.
// ========================================================
partial class Program
{
    // Règle (ou préréglage) dont l'explication est affichée en bas. "" = aucune.
    static string infoEpinglee = "";
    static string infoSurvolee = "";

    // Maintien du clic sur un [-] / [+] : répétition automatique après 0.4 s.
    static string boutonMaintenu = "";
    static float boutonDelaiRepetition = 0f;

    // Le panneau est-il ouvert ? (salon + solo ; l'onglet des Paramètres a son propre chemin)
    public static bool isReglesOpen = false;

    static readonly Color regleFond      = new Color(24, 24, 28, 245);
    static readonly Color regleFondLigne = new Color(38, 38, 44, 255);
    static readonly Color regleBord      = new Color(110, 110, 120, 255);

    // ========================================================
    // UN BOUTON À RÉPÉTITION (clic maintenu = incréments en rafale)
    // ========================================================
    static bool BoutonRegle(Rectangle box, string texte, string id, int taillePolice = 22, bool repetition = true)
    {
        bool focus = MenuNav.Item(box, out bool valide);
        bool survol = MenuNav.UsingMouse && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), box);
        bool declenche = false;

        if (!MenuNav.UsingMouse)
        {
            declenche = valide; // clavier / manette : Entrée ou A
        }
        else
        {
            if (survol && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                declenche = true;
                if (repetition) { boutonMaintenu = id; boutonDelaiRepetition = 0.4f; }
            }
            if (repetition && boutonMaintenu == id)
            {
                if (!Raylib.IsMouseButtonDown(MouseButton.Left) || !survol) boutonMaintenu = "";
                else
                {
                    boutonDelaiRepetition -= Raylib.GetFrameTime();
                    if (boutonDelaiRepetition <= 0f) { boutonDelaiRepetition = 0.05f; declenche = true; }
                }
            }
        }

        Raylib.DrawRectangleRec(box, focus ? new Color(120, 40, 40, 255) : new Color(70, 70, 78, 255));
        Raylib.DrawRectangleLinesEx(box, 2, focus ? Color.White : Color.Black);
        int w = Raylib.MeasureText(texte, taillePolice);
        Raylib.DrawText(texte, (int)(box.X + (box.Width - w) / 2), (int)(box.Y + (box.Height - taillePolice) / 2), taillePolice, Color.White);

        if (declenche) Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
        return declenche;
    }

    // Le petit rond "(i)" : survolé il prévisualise, cliqué il épingle l'explication.
    static void BoutonInfo(Rectangle box, string cle, string texte)
    {
        bool focus = MenuNav.Item(box, out bool valide);
        if (focus) infoSurvolee = cle;
        if (valide) infoEpinglee = (infoEpinglee == cle) ? "" : cle;

        bool ouvert = infoEpinglee == cle;
        Color fond = ouvert ? Color.Gold : (focus ? new Color(120, 120, 200, 255) : new Color(60, 60, 70, 255));
        Raylib.DrawCircle((int)(box.X + box.Width / 2), (int)(box.Y + box.Height / 2), box.Width / 2, fond);
        Raylib.DrawCircleLines((int)(box.X + box.Width / 2), (int)(box.Y + box.Height / 2), box.Width / 2, Color.Black);
        int w = Raylib.MeasureText("i", 20);
        Raylib.DrawText("i", (int)(box.X + (box.Width - w) / 2), (int)(box.Y + 4), 20, ouvert ? Color.Black : Color.White);
    }

    // ========================================================
    // UNE LIGNE DE RÈGLE
    // ========================================================
    // Renvoie true si la valeur a changé (l'appelant rediffuse alors aux clients).
    static bool DessinerLigneRegle(MatchRules.Regle r, int x, int y, int largeur, int hauteur, bool editable)
    {
        bool change = false;
        bool horsSujet = r.SoloSeulement && Program.isOnline; // zombies en ligne : sans effet

        Rectangle ligne = new Rectangle(x, y, largeur, hauteur - 4);
        Raylib.DrawRectangleRec(ligne, regleFondLigne);
        if (!r.EstDefaut) Raylib.DrawRectangle(x, y, 4, hauteur - 4, Color.Gold); // repère "modifié"

        // --- Le nom (+ étiquette SOLO si la règle ne sert à rien en ligne) ---
        Color couleurNom = horsSujet ? new Color(120, 120, 125, 255) : Color.White;
        Raylib.DrawText(r.Nom, x + 14, y + 9, 21, couleurNom);
        if (horsSujet)
        {
            int wn = Raylib.MeasureText(r.Nom, 21);
            Raylib.DrawText("SOLO", x + 24 + wn, y + 12, 15, new Color(190, 140, 60, 255));
        }

        // --- Les commandes, calées à droite ---
        int bx = x + largeur - 8;

        Rectangle boxInfo = new Rectangle(bx - 30, y + 4, 30, 30);
        bx -= 38;
        BoutonInfo(boxInfo, r.Cle, r.Info);

        if (editable && r.Type != MatchRules.Genre.Masque)
        {
            Rectangle boxPlus = new Rectangle(bx - 34, y + 3, 34, 32);
            if (BoutonRegle(boxPlus, "+", r.Cle + "+")) { MatchRules.Increment(r, +1); change = true; }
            bx -= 40;

            int largeurValeur = 150;
            DessinerValeur(r, bx - largeurValeur, y, largeurValeur, hauteur, horsSujet);
            bx -= largeurValeur + 6;

            Rectangle boxMoins = new Rectangle(bx - 34, y + 3, 34, 32);
            if (BoutonRegle(boxMoins, "-", r.Cle + "-")) { MatchRules.Increment(r, -1); change = true; }
        }
        else
        {
            DessinerValeur(r, bx - 220, y, 220, hauteur, horsSujet);
        }

        return change;
    }

    static void DessinerValeur(MatchRules.Regle r, int x, int y, int largeur, int hauteur, bool grise)
    {
        string txt = r.Texte;
        // Les libellés longs ("1 (double saut)", "60 m (brouillard)") passent en petit
        // pour ne jamais déborder sur les boutons [-] / [+].
        int taille = txt.Length > 12 ? 17 : 21;
        int w = Raylib.MeasureText(txt, taille);
        Color c = grise ? new Color(130, 130, 135, 255) : (r.EstDefaut ? Color.LightGray : Color.Gold);
        Raylib.DrawText(txt, x + (largeur - w) / 2, y + (hauteur - 4 - taille) / 2, taille, c);
    }

    // ========================================================
    // LE PANNEAU COMPLET
    // ========================================================
    // editable : true = on peut régler (hôte dans le salon, ou partie solo).
    //            false = lecture seule (client, ou consultation en pleine partie).
    // Le panneau sert aussi d'ONGLET dans les Paramètres : on peut alors couper le fond,
    // le titre et le bouton FERMER, et décaler le contenu sous les onglets (yDepart).
    // Renvoie true quand l'utilisateur demande à fermer.
    public static bool DessinerPanneauRegles(bool editable, bool dessinerFond = true,
                                             bool avecTitre = true, bool avecBoutonFermer = true,
                                             int yDepart = 28)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        infoSurvolee = "";
        bool fermer = false;
        bool change = false;

        if (dessinerFond) Raylib.DrawRectangle(0, 0, sw, sh, new Color(12, 12, 16, 238));

        int panW = Math.Min(1520, sw - 80);
        int panX = (sw - panW) / 2;
        int y = yDepart;

        // ---------- TITRE ----------
        if (avecTitre)
        {
            string titre = "RÈGLES DU MATCH";
            Raylib.DrawText(titre, sw / 2 - Raylib.MeasureText(titre, 44) / 2, y, 44, Color.Gold);
            y += 52;
        }

        string sousTitre = editable
            ? "Tu es l'hôte : ces réglages s'appliquent à TOUS les joueurs."
            : (Program.isOnline ? "Réglé par l'hôte - lecture seule." : "Lecture seule.");
        Raylib.DrawText(sousTitre, sw / 2 - Raylib.MeasureText(sousTitre, 20) / 2, y, 20, Color.LightGray);
        y += 38;

        // ---------- PRÉRÉGLAGES (hôte seulement) ----------
        if (editable)
        {
            int nbP = MatchRules.Presets.Count;
            int bw = 190, ecart = 12;
            int total = nbP * bw + (nbP - 1) * ecart;
            int px = sw / 2 - total / 2;
            for (int i = 0; i < nbP; i++)
            {
                MatchRules.Preset p = MatchRules.Presets[i];
                Rectangle box = new Rectangle(px + i * (bw + ecart), y, bw, 42);
                bool focus = MenuNav.Item(box, out bool clic);
                if (focus) infoSurvolee = "preset:" + i;
                if (clic) { p.Poser(); change = true; }

                Raylib.DrawRectangleRec(box, focus ? new Color(150, 50, 50, 255) : new Color(55, 55, 62, 255));
                Raylib.DrawRectangleLinesEx(box, 2, focus ? Color.White : Color.Black);
                int w = Raylib.MeasureText(p.Nom, 20);
                Raylib.DrawText(p.Nom, (int)(box.X + (bw - w) / 2), (int)box.Y + 11, 20, Color.White);
            }
            y += 56;
        }

        // ---------- LES DEUX COLONNES ----------
        // Colonne gauche : PHYSIQUE + MONDE. Colonne droite : COMBAT + ZOMBIES + CHAOS.
        string[] catGauche = { "PHYSIQUE", "MONDE" };
        string[] catDroite = { "COMBAT", "ZOMBIES", "CHAOS" };

        int colW = (panW - 40) / 2;
        int hauteurLigne = 40;

        int yG = y, yD = y;
        foreach (string cat in catGauche) yG = DessinerCategorie(cat, panX, yG, colW, hauteurLigne, editable, ref change);
        foreach (string cat in catDroite) yD = DessinerCategorie(cat, panX + colW + 40, yD, colW, hauteurLigne, editable, ref change);

        y = Math.Max(yG, yD) + 6;

        // ---------- LA BANDE DES ARMES ----------
        MatchRules.Regle rArmes = MatchRules.Get("armes");
        if (rArmes != null)
        {
            Raylib.DrawRectangle(panX, y, panW, 62, regleFondLigne);
            if (!rArmes.EstDefaut) Raylib.DrawRectangle(panX, y, 4, 62, Color.Gold);
            Raylib.DrawText("ARMES AUTORISÉES", panX + 14, y + 8, 19, Color.LightGray);
            Raylib.DrawText(rArmes.Texte, panX + 14, y + 32, 21, rArmes.EstDefaut ? Color.LightGray : Color.Gold);

            BoutonInfo(new Rectangle(panX + panW - 40, y + 16, 30, 30), "armes", rArmes.Info);

            int chipW = 118, chipEcart = 8;
            int nbA = MatchRules.NomsArmes.Length;
            int totalChips = nbA * chipW + (nbA - 1) * chipEcart;
            int cx = panX + panW - 60 - totalChips;
            for (int i = 0; i < nbA; i++)
            {
                bool active = MatchRules.ArmeAutorisee(i);
                Rectangle box = new Rectangle(cx + i * (chipW + chipEcart), y + 14, chipW, 34);

                bool focus = false, clic = false;
                if (editable) focus = MenuNav.Item(box, out clic);
                if (clic) { MatchRules.BasculerArme(i); change = true; }

                Color fond = active ? new Color(45, 120, 55, 255) : new Color(58, 58, 64, 255);
                Raylib.DrawRectangleRec(box, fond);
                Raylib.DrawRectangleLinesEx(box, focus ? 3 : 2, focus ? Color.White : Color.Black);
                string nom = MatchRules.NomsArmes[i];
                int w = Raylib.MeasureText(nom, 18);
                Raylib.DrawText(nom, (int)(box.X + (chipW - w) / 2), (int)box.Y + 8, 18,
                                active ? Color.White : new Color(140, 140, 145, 255));
            }
            y += 70;
        }

        // ---------- LA BOÎTE D'EXPLICATION ----------
        string cleInfo = infoSurvolee != "" ? infoSurvolee : infoEpinglee;
        int boiteH = 118;
        Raylib.DrawRectangle(panX, y, panW, boiteH, regleFond);
        Raylib.DrawRectangleLinesEx(new Rectangle(panX, y, panW, boiteH), 2, regleBord);

        if (cleInfo == "")
        {
            string aide = "Clique sur un (i) pour savoir ce que fait un réglage.";
            Raylib.DrawText(aide, panX + 18, y + 16, 21, new Color(150, 150, 158, 255));
            string aide2 = editable
                ? "Les réglages modifiés sont marqués en doré ; ils partent automatiquement à tous les joueurs du salon."
                : "Ces réglages sont ceux choisis par l'hôte du salon.";
            Raylib.DrawText(aide2, panX + 18, y + 48, 19, new Color(120, 120, 128, 255));
        }
        else
        {
            string titreInfo, corps;
            if (cleInfo.StartsWith("preset:"))
            {
                MatchRules.Preset p = MatchRules.Presets[int.Parse(cleInfo.Substring(7))];
                titreInfo = "PRÉRÉGLAGE " + p.Nom;
                corps = p.Info;
            }
            else
            {
                MatchRules.Regle r = MatchRules.Get(cleInfo);
                titreInfo = r != null ? r.Nom.ToUpperInvariant() + "   -   " + r.Texte : "";
                corps = r != null ? r.Info : "";
            }
            Raylib.DrawText(titreInfo, panX + 18, y + 12, 22, Color.Gold);
            int ly = y + 42;
            foreach (string ligne in corps.Split('\n'))
            {
                Raylib.DrawText(ligne, panX + 18, ly, 19, new Color(215, 215, 220, 255));
                ly += 22;
            }
        }
        y += boiteH + 14;

        // ---------- FERMER ----------
        if (avecBoutonFermer)
        {
            Rectangle boxFermer = new Rectangle(sw / 2 - 130, y, 260, 48);
            if (BoutonRegle(boxFermer, "FERMER", "regles-fermer", 24, false)) fermer = true;
            if (MenuNav.Back) fermer = true;
        }

        // ---------- DIFFUSION ----------
        // Un changement s'applique tout de suite chez nous ET part chez les clients.
        if (change)
        {
            MatchRules.Appliquer();
            if (Program.isServer) Program.HoteDiffuserRegles();
        }

        return fermer;
    }

    // Dessine l'en-tête d'une catégorie puis toutes ses règles. Renvoie le Y suivant.
    static int DessinerCategorie(string categorie, int x, int y, int largeur, int hauteurLigne, bool editable, ref bool change)
    {
        Raylib.DrawText(categorie, x + 4, y + 4, 20, new Color(200, 90, 90, 255));
        Raylib.DrawLine(x + 4, y + 27, x + largeur, y + 27, new Color(90, 90, 96, 255));
        y += 34;

        foreach (MatchRules.Regle r in MatchRules.Liste)
        {
            if (r.Categorie != categorie) continue;
            if (r.Type == MatchRules.Genre.Masque) continue; // les armes ont leur propre bande
            if (DessinerLigneRegle(r, x, y, largeur, hauteurLigne, editable)) change = true;
            y += hauteurLigne;
        }
        return y + 10;
    }
}
