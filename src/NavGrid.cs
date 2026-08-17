using System.Numerics;
using System.Diagnostics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Trees;
using Raylib_cs;

// ========================================================================================
// NAVGRID : LE GPS DES ZOMBIES
// ----------------------------------------------------------------------------------------
// On ne parse pas le .glb à la main : la map est DÉJÀ dans BEPU sous forme de mesh statique
// (ExtraireTrianglesMap). On échantillonne donc le monde physique avec des rayons verticaux
// pour "cuire" (bake) une grille de cases marchables, multi-étages, avec :
//   - liaisons de marche 8-directions (marches <= 0.55m, pas de coupe de coin en diagonale)
//   - liaisons de SAUT au-dessus des trous / descentes de corniche (vérifiées au bake ET au runtime)
// Ensuite : A* sur la grille + lissage du chemin.
// Rebake à chaque changement de map (appelé par InitEnnemis).
// ========================================================================================

// Capteur laser réservé à la navigation : il ne voit QUE la géométrie STATIQUE
// (jamais les joueurs/zombies/projectiles). En mode "mapSeulement", il ignore
// aussi les barils et les murs posés — utile pour le bake du sol.
public struct NavRaySensor : IRayHitHandler
{
    public bool aTouche;
    public float distance;
    public Vector3 normale;
    public bool mapSeulement;

    public NavRaySensor(bool mapSeulement_)
    {
        aTouche = false;
        distance = float.MaxValue;
        normale = Vector3.Zero;
        mapSeulement = mapSeulement_;
    }

    public bool AllowTest(CollidableReference collidable)
    {
        if (collidable.Mobility != CollidableMobility.Static) return false;
        return !mapSeulement || collidable.StaticHandle == Program.mapStaticHandle;
    }

    public bool AllowTest(CollidableReference collidable, int childIndex) { return true; }

    public void OnRayHit(in RayData ray, ref float maximumT, float t, in Vector3 hitNormal, CollidableReference collidable, int childIndex)
    {
        if (t < maximumT)
        {
            maximumT = t;
            distance = t;
            normale = hitNormal;
            aTouche = true;
        }
    }
}

public static class NavGrid
{
    // --- Réglages de la grille ---
    public const float CELLULE = 1.0f;        // taille d'une case (m) — la capsule zombie fait 0.5 de rayon
    const float MARCHE_MAX = 0.55f;           // hauteur de marche franchissable à pied
    const float HAUTEUR_LIBRE = 1.9f;         // plafond minimum pour qu'un zombie (2m) tienne debout
    const float PENTE_MIN = 0.7f;             // normale.Y minimum (~45°) pour être marchable
    const int SAUT_CASES_MIN = 2;             // portée minimale d'un saut (en cases)
    const int SAUT_CASES_MAX = 6;             // portée maximale d'un saut (en cases)
    const float SAUT_MONTEE_MAX = 1.2f;       // on peut sauter jusqu'à 1.2m PLUS HAUT
    const float SAUT_DESCENTE_MAX = 8f;       // ... et se laisser tomber jusqu'à 8m PLUS BAS

    // --- Marge de sécurité (obstacle inflation) : évite le "scraping" contre les murs ---
    const float CLEARANCE_RAYON = 0.9f;       // sonde de proximité mur, à hauteur de torse
    const float CLEARANCE_HAUTEUR = 0.9f;     // hauteur du torse au-dessus des pieds (où on sonde)
    const float BLOCK_DIST = 0.5f;            // mur plus proche que ça (= rayon capsule) => case à fuir
    const float MULT_ORANGE = 10f;            // surcoût x10 des cases "orange" (frôlent un mur, 0.5-0.9m)
    // Case à moins de 0.5m d'un mur : la capsule s'y coincerait -> coût prohibitif (x1000).
    // Pourquoi un coût énorme plutôt qu'un vrai blocage ? La grille fait 1m : un passage
    // pourtant franchissable mais mal aligné sur le quadrillage se retrouverait TOTALEMENT
    // scellé (constaté : un spawn des Blocs se retrouvait emmuré dans une poche). Avec x1000,
    // l'A* ne passe là QUE s'il n'existe aucune autre route — c'est exactement l'exception
    // "couloir étroit" demandée, et ça marche quelle que soit l'épaisseur de la bande.
    const float MULT_BLOQUE = 1000f;
    const float CLEARANCE_ZOMBIE = 1.9f;      // hauteur d'un zombie debout (blocage dynamique par murs posés)

    // Un point du chemin renvoyé par A*. "sautVersSuivant" = l'arête qui part de CE
    // waypoint vers le suivant est un saut (le zombie doit bondir, pas marcher).
    public struct Waypoint
    {
        public Vector3 pos;           // point AU SOL
        public bool sautVersSuivant;
    }

    struct Cellule
    {
        public Vector3 pos; // point au sol (centre de la case)
        public int gx, gz;  // coordonnées grille
    }

    static Cellule[] cellules = Array.Empty<Cellule>();
    static int[][] voisins = Array.Empty<int[]>();   // arêtes de MARCHE
    static int[][] sauts = Array.Empty<int[]>();     // arêtes de SAUT (indices de cellules cibles)
    static int[] composante = Array.Empty<int>();    // îlot de connexité de chaque cellule
    static float[] coutMult = Array.Empty<float>();  // multiplicateur de coût A* (1 = dégagé, 10 = frôle un mur)
    static bool[] bloque = Array.Empty<bool>();       // case INMARCHABLE (trop près d'un mur, ou mur posé dessus)
    static Dictionary<(int, int), int[]> colonnes = new(); // (gx,gz) -> indices de cellules (étages)
    static readonly Random rng = new Random();

    // 16 directions horizontales pour sonder la proximité des murs (marge de sécurité).
    // 16 et pas 8 : un pilier fin peut se glisser ENTRE deux rayons trop espacés (le fameux
    // coincement sur les "doubles piliers fins") ; à 16 rayons l'écart tombe à ~0.2m au rayon
    // de blocage, assez serré pour attraper les piliers fins. (Rayons à HAUTEUR DE TORSE :
    // ils ignorent d'eux-mêmes les marches basses <0.55m, donc n'abîment pas escaliers/rampes.)
    static readonly Vector3[] DIRS_CLEARANCE = GenererDirsClearance(12);
    static Vector3[] GenererDirsClearance(int n)
    {
        var dirs = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * MathF.Tau / n;
            dirs[i] = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
        }
        return dirs;
    }

    public static bool EstPrete => cellules.Length > 0;
    public static int NbCellules => cellules.Length;

    public static void Vider()
    {
        cellules = Array.Empty<Cellule>();
        voisins = Array.Empty<int[]>();
        sauts = Array.Empty<int[]>();
        composante = Array.Empty<int>();
        coutMult = Array.Empty<float>();
        bloque = Array.Empty<bool>();
        colonnes.Clear();
    }

    // ==========================================
    // LE BAKE : map physique -> grille marchable
    // ==========================================
    public static void Bake(Simulation sim, BoundingBox boiteModele, float echelle, Vector3 origine)
    {
        Vider();
        Stopwatch chrono = Stopwatch.StartNew();

        Vector3 min = boiteModele.Min * echelle + origine;
        Vector3 max = boiteModele.Max * echelle + origine;

        int gxMin = (int)MathF.Floor(min.X / CELLULE) - 1;
        int gxMax = (int)MathF.Ceiling(max.X / CELLULE) + 1;
        int gzMin = (int)MathF.Floor(min.Z / CELLULE) - 1;
        int gzMax = (int)MathF.Ceiling(max.Z / CELLULE) + 1;

        // Garde-fou : une map démesurée (skybox géante, bbox cassée...) ne doit pas geler
        // le chargement. Si on doit tronquer, on CENTRE la fenêtre sur les points de spawn
        // (la zone de jeu réelle), pas sur le coin de la bounding box !
        // 400 cases = ±200m autour du centre de jeu : largement au-delà de l'aire de spawn
        // la plus étalée (sandbox ~226m), tout en évitant de cuire 172k cases de sol vide.
        const int MAX_CASES_PAR_AXE = 400;
        if (gxMax - gxMin > MAX_CASES_PAR_AXE || gzMax - gzMin > MAX_CASES_PAR_AXE)
        {
            Vector3 centre = CentroideSpawns((min + max) * 0.5f);
            int cgx = (int)MathF.Round(centre.X / CELLULE);
            int cgz = (int)MathF.Round(centre.Z / CELLULE);
            if (gxMax - gxMin > MAX_CASES_PAR_AXE)
            {
                Console.WriteLine("[NAV] Map trop large, grille recentrée sur les spawns (X)");
                gxMin = Math.Max(gxMin, cgx - MAX_CASES_PAR_AXE / 2);
                gxMax = Math.Min(gxMax, cgx + MAX_CASES_PAR_AXE / 2);
            }
            if (gzMax - gzMin > MAX_CASES_PAR_AXE)
            {
                Console.WriteLine("[NAV] Map trop large, grille recentrée sur les spawns (Z)");
                gzMin = Math.Max(gzMin, cgz - MAX_CASES_PAR_AXE / 2);
                gzMax = Math.Min(gzMax, cgz + MAX_CASES_PAR_AXE / 2);
            }
        }

        float yHaut = max.Y + 2f;
        float yBas = min.Y - 1f;

        var brouillon = new List<Cellule>();
        var colonnesTmp = new Dictionary<(int, int), List<int>>();

        // --- 1. ÉCHANTILLONNAGE : un rayon vers le bas par colonne, multi-étages ---
        for (int gx = gxMin; gx <= gxMax; gx++)
        {
            for (int gz = gzMin; gz <= gzMax; gz++)
            {
                float x = gx * CELLULE;
                float z = gz * CELLULE;
                float depart = yHaut;
                int garde = 0;

                while (depart > yBas && garde++ < 24)
                {
                    NavRaySensor sol = new NavRaySensor(true);
                    sim.RayCast(new Vector3(x, depart, z), new Vector3(0, -1, 0), depart - yBas, ref sol);
                    if (!sol.aTouche) break;

                    float ySol = depart - sol.distance;

                    // Marchable ? (pente OK — Abs car la normale d'un mesh peut être retournée)
                    if (MathF.Abs(sol.normale.Y) >= PENTE_MIN)
                    {
                        // Assez de place au-dessus ? (vérifié contre TOUS les statics :
                        // un baril ou un mur posé au moment du bake bloque la case)
                        NavRaySensor plafond = new NavRaySensor(false);
                        sim.RayCast(new Vector3(x, ySol + 0.1f, z), Vector3.UnitY, HAUTEUR_LIBRE, ref plafond);

                        if (!plafond.aTouche)
                        {
                            int idx = brouillon.Count;
                            brouillon.Add(new Cellule { pos = new Vector3(x, ySol, z), gx = gx, gz = gz });
                            if (!colonnesTmp.TryGetValue((gx, gz), out var liste))
                            {
                                liste = new List<int>();
                                colonnesTmp[(gx, gz)] = liste;
                            }
                            liste.Add(idx);
                        }
                    }

                    depart = ySol - 0.2f; // on continue sous cette surface (étages inférieurs)
                }
            }
        }

        cellules = brouillon.ToArray();
        foreach (var kv in colonnesTmp) colonnes[kv.Key] = kv.Value.ToArray();

        // --- 1bis. MARGE DE SÉCURITÉ (obstacle inflation) ---
        // On sonde 12 directions à hauteur de torse et on retient le MUR LE PLUS PROCHE :
        //   < 0.5m (rayon capsule) -> coût x1000 : la capsule s'y coincerait, l'A* n'y passe
        //                            QUE s'il n'existe aucune autre route (exception couloir) ;
        //   0.5–0.9m -> case "orange" à coût x10 : l'A* la contourne agressivement ;
        //   >= 0.9m  -> case dégagée (coût normal).
        coutMult = new float[cellules.Length];
        bloque = new bool[cellules.Length]; // réservé au blocage DUR : murs posés par les joueurs
        for (int i = 0; i < cellules.Length; i++)
        {
            Vector3 torse = cellules[i].pos + new Vector3(0, CLEARANCE_HAUTEUR, 0);
            float plusProche = CLEARANCE_RAYON + 1f;
            foreach (Vector3 d in DIRS_CLEARANCE)
            {
                NavRaySensor mur = new NavRaySensor(true); // murs STATIQUES de la map uniquement
                sim.RayCast(torse, d, CLEARANCE_RAYON, ref mur);
                if (mur.aTouche && mur.distance < plusProche) plusProche = mur.distance;
            }

            if (plusProche < BLOCK_DIST) coutMult[i] = MULT_BLOQUE;
            else if (plusProche < CLEARANCE_RAYON) coutMult[i] = MULT_ORANGE;
            else coutMult[i] = 1f;
        }

        // --- 2. LIAISONS DE MARCHE (8 directions, anti coupe-de-coin) ---
        (int dx, int dz)[] dirs8 = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };
        voisins = new int[cellules.Length][];
        var tmpVoisins = new List<int>(8);

        for (int i = 0; i < cellules.Length; i++)
        {
            tmpVoisins.Clear();
            ref var c = ref cellules[i];

            foreach (var (dx, dz) in dirs8)
            {
                int j = ChercherNiveau(c.gx + dx, c.gz + dz, c.pos.Y, MARCHE_MAX);
                if (j < 0) continue;

                // Diagonale : les deux cases orthogonales doivent exister au même niveau,
                // sinon le zombie "coupe le coin" à travers un mur ou au-dessus d'un trou.
                if (dx != 0 && dz != 0)
                {
                    if (ChercherNiveau(c.gx + dx, c.gz, c.pos.Y, MARCHE_MAX) < 0) continue;
                    if (ChercherNiveau(c.gx, c.gz + dz, c.pos.Y, MARCHE_MAX) < 0) continue;
                }

                // CRUCIAL : y a-t-il un MUR ENTRE les deux cases ? L'adjacence de grille ne
                // regarde que la hauteur ; deux cases voisines séparées par un mur fin (chacune
                // à ~0.5m du mur, donc non bloquées) seraient reliées et le zombie foncerait
                // DANS le mur. On ne sonde QUE le segment CENTRAL entre les deux cases (pas leurs
                // abords) : ça attrape un mur intercalé sans couper une arête juste parce qu'une
                // case longe un mur. (À 0.9m de haut, les marches <0.55m passent dessous : escaliers OK.)
                Vector3 ta = c.pos + new Vector3(0, 0.9f, 0);
                Vector3 tb = cellules[j].pos + new Vector3(0, 0.9f, 0);
                if (MurEntre(sim, ta, tb)) continue;

                tmpVoisins.Add(j);
            }
            voisins[i] = tmpVoisins.ToArray();
        }

        // --- 3. LIAISONS DE SAUT (cardinales, au-dessus des trous / corniches) ---
        (int dx, int dz)[] dirs4 = { (1, 0), (-1, 0), (0, 1), (0, -1) };
        sauts = new int[cellules.Length][];
        var tmpSauts = new List<int>(4);
        int nbLiens = 0;

        for (int i = 0; i < cellules.Length; i++)
        {
            tmpSauts.Clear();
            ref var c = ref cellules[i];

            foreach (var (dx, dz) in dirs4)
            {
                // Un saut n'a de sens que là où la marche s'arrête (bord de plateforme)
                if (ChercherNiveau(c.gx + dx, c.gz + dz, c.pos.Y, MARCHE_MAX) >= 0) continue;

                for (int d = SAUT_CASES_MIN; d <= SAUT_CASES_MAX; d++)
                {
                    int j = ChercherNiveauSaut(c.gx + dx * d, c.gz + dz * d, c.pos.Y);
                    if (j < 0) continue;

                    // Trajectoire dégagée ? Rayon tête -> tête (map uniquement).
                    Vector3 a = c.pos + new Vector3(0, 1.4f, 0);
                    Vector3 b = cellules[j].pos + new Vector3(0, 1.4f, 0);
                    if (RayonDegage(sim, a, b, true))
                    {
                        tmpSauts.Add(j);
                        nbLiens++;
                    }
                    break; // premier candidat trouvé dans cette direction : on s'arrête
                }
            }
            sauts[i] = tmpSauts.Count > 0 ? tmpSauts.ToArray() : Array.Empty<int>();
        }

        // --- 4. COMPOSANTES CONNEXES (union-find) ---
        // Permet de répondre INSTANTANÉMENT "est-ce joignable ?" avant de lancer un A* :
        // sans ça, chaque cible injoignable brûlerait tout le budget d'exploration à chaque repath.
        RecalculerComposantes();

        int nbEvitees = 0, nbOrange = 0;
        for (int i = 0; i < coutMult.Length; i++) { if (coutMult[i] >= MULT_BLOQUE) nbEvitees++; else if (coutMult[i] > 1.01f) nbOrange++; }

        chrono.Stop();
        Console.WriteLine($"[NAV] Grille cuite : {cellules.Length} cases ({nbEvitees} quasi-bloquées, {nbOrange} orange), {nbLiens} liens de saut, en {chrono.ElapsedMilliseconds} ms");
    }

    // Recalcule les îlots de connexité en IGNORANT les cases bloquées (statiques ET murs
    // posés dynamiquement). Rappelé à chaque fois que le blocage change (mur posé).
    static void RecalculerComposantes()
    {
        composante = new int[cellules.Length];
        for (int i = 0; i < composante.Length; i++) composante[i] = i;
        for (int i = 0; i < cellules.Length; i++)
        {
            if (bloque[i]) continue;
            foreach (int j in voisins[i]) if (!bloque[j]) Unir(i, j);
            foreach (int j in sauts[i]) if (!bloque[j]) Unir(i, j); // saut réversible pour la connexité
        }
        for (int i = 0; i < composante.Length; i++) composante[i] = Racine(i);
    }

    // ==========================================
    // BLOCAGE DYNAMIQUE : un mur posé par un joueur rend des cases inmarchables.
    // Renvoie les CENTRES des cases nouvellement bloquées (pour re-router les zombies).
    // Un mur placé tout en haut (plafond/passerelle) qui ne gêne pas un zombie au sol
    // est ignoré (aucune case bloquée -> liste vide).
    // ==========================================
    public static List<Vector3> BloquerBoite(Vector3 min, Vector3 max)
    {
        var touchees = new List<Vector3>();
        if (!EstPrete) return touchees;

        // Marge = rayon de la capsule : une case dont le centre est à <0.45m du mur gêne déjà.
        const float marge = 0.45f;
        int gxMin = (int)MathF.Floor((min.X - marge) / CELLULE);
        int gxMax = (int)MathF.Ceiling((max.X + marge) / CELLULE);
        int gzMin = (int)MathF.Floor((min.Z - marge) / CELLULE);
        int gzMax = (int)MathF.Ceiling((max.Z + marge) / CELLULE);

        for (int gx = gxMin; gx <= gxMax; gx++)
        {
            for (int gz = gzMin; gz <= gzMax; gz++)
            {
                if (!colonnes.TryGetValue((gx, gz), out var etages)) continue;
                foreach (int idx in etages)
                {
                    if (bloque[idx]) continue;
                    float yPieds = cellules[idx].pos.Y;
                    // Le mur gêne-t-il un zombie (haut 1.9m) debout sur cette case ?
                    // -> chevauchement de [yPieds, yPieds+1.9] avec la tranche verticale du mur.
                    if (max.Y > yPieds && min.Y < yPieds + CLEARANCE_ZOMBIE)
                    {
                        bloque[idx] = true;
                        coutMult[idx] = MULT_ORANGE;
                        touchees.Add(cellules[idx].pos);
                    }
                }
            }
        }

        if (touchees.Count > 0) RecalculerComposantes();
        return touchees;
    }

    // Union-Find avec compression de chemin
    static int Racine(int i)
    {
        while (composante[i] != i)
        {
            composante[i] = composante[composante[i]];
            i = composante[i];
        }
        return i;
    }
    static void Unir(int a, int b)
    {
        int ra = Racine(a), rb = Racine(b);
        if (ra != rb) composante[ra] = rb;
    }

    // Centre de la zone de jeu = moyenne des points de spawn (joueurs + ennemis).
    // Repli sur le centre de la bbox si aucun spawn n'est chargé.
    static Vector3 CentroideSpawns(Vector3 secours)
    {
        Vector3 somme = Vector3.Zero;
        int n = 0;
        foreach (Vector3 p in Program.enemySpawnPoints) { somme += p; n++; }
        foreach (Vector3 p in Program.listeSpawns) { somme += p; n++; }
        return n > 0 ? somme / n : secours;
    }

    // Dans la colonne (gx,gz), trouve l'étage dont la hauteur est la plus proche de y (tolérance tol).
    static int ChercherNiveau(int gx, int gz, float y, float tol)
    {
        if (!colonnes.TryGetValue((gx, gz), out var etages)) return -1;
        int meilleur = -1;
        float meilleurEcart = tol;
        foreach (int idx in etages)
        {
            float ecart = MathF.Abs(cellules[idx].pos.Y - y);
            if (ecart <= meilleurEcart) { meilleurEcart = ecart; meilleur = idx; }
        }
        return meilleur;
    }

    // Cible d'un saut : la cellule la plus HAUTE de la colonne qui reste atteignable
    // (pas plus de 1.2m au-dessus, pas plus de 8m en dessous).
    static int ChercherNiveauSaut(int gx, int gz, float y)
    {
        if (!colonnes.TryGetValue((gx, gz), out var etages)) return -1;
        int meilleur = -1;
        float meilleureHauteur = float.MinValue;
        foreach (int idx in etages)
        {
            float dy = cellules[idx].pos.Y - y;
            if (dy > SAUT_MONTEE_MAX || dy < -SAUT_DESCENTE_MAX) continue;
            if (cellules[idx].pos.Y > meilleureHauteur) { meilleureHauteur = cellules[idx].pos.Y; meilleur = idx; }
        }
        return meilleur;
    }

    // Y a-t-il un mur STATIQUE dans le segment CENTRAL entre a et b ? (on ignore les 30%
    // proches de chaque extrémité, pour ne pas couper une arête juste parce qu'une case
    // longe un mur : seul un mur réellement INTERCALÉ entre les deux cases compte.)
    static bool MurEntre(Simulation sim, Vector3 a, Vector3 b)
    {
        Vector3 delta = b - a;
        float d = delta.Length();
        if (d < 0.01f) return false;
        Vector3 dir = delta / d;
        NavRaySensor s = new NavRaySensor(true);
        sim.RayCast(a + dir * (d * 0.30f), dir, d * 0.40f, ref s);
        return s.aTouche;
    }

    static bool RayonDegage(Simulation sim, Vector3 a, Vector3 b, bool mapSeulement)
    {
        Vector3 delta = b - a;
        float dist = delta.Length();
        if (dist < 0.01f) return true;
        NavRaySensor s = new NavRaySensor(mapSeulement);
        sim.RayCast(a, delta / dist, dist - 0.05f, ref s);
        return !s.aTouche;
    }

    // ==========================================
    // RECHERCHE DE CELLULE (position monde -> grille)
    // ==========================================
    // "pos" = position des PIEDS. Recherche en spirale si la colonne exacte est vide.
    public static int CelluleProche(Vector3 pos)
    {
        if (!EstPrete) return -1;
        int gx = (int)MathF.Round(pos.X / CELLULE);
        int gz = (int)MathF.Round(pos.Z / CELLULE);

        for (int rayon = 0; rayon <= 5; rayon++)
        {
            int meilleur = -1;
            float meilleurScore = float.MaxValue;
            for (int dx = -rayon; dx <= rayon; dx++)
            {
                for (int dz = -rayon; dz <= rayon; dz++)
                {
                    // uniquement l'anneau extérieur (le centre a déjà été testé)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != rayon) continue;
                    if (!colonnes.TryGetValue((gx + dx, gz + dz), out var etages)) continue;
                    foreach (int idx in etages)
                    {
                        if (bloque[idx]) continue; // jamais snapper/cibler une case inmarchable
                        float dy = MathF.Abs(cellules[idx].pos.Y - pos.Y);
                        if (dy > 4f) continue; // pas le bon étage
                        float score = dx * dx + dz * dz + dy * dy * 0.5f;
                        if (score < meilleurScore) { meilleurScore = score; meilleur = idx; }
                    }
                }
            }
            if (meilleur >= 0) return meilleur;
        }
        return -1;
    }

    // ==========================================
    // A* : le vrai pathfinding
    // ==========================================
    // "complet" = true si le chemin atteint vraiment la destination. Si la cible est
    // injoignable (autre îlot, perchoir...), on renvoie un chemin PARTIEL vers le point
    // exploré le plus proche de la cible : le zombie s'approche au maximum au lieu
    // d'abandonner (et l'appelant peut espacer ses recalculs grâce à "complet").
    public static List<Waypoint> TrouverChemin(Vector3 depart, Vector3 arrivee, out bool complet)
    {
        complet = false;
        if (!EstPrete) return null;
        int a = CelluleProche(depart);
        int b = CelluleProche(arrivee);
        if (a < 0 || b < 0) return null;

        if (a == b)
        {
            complet = true;
            return new List<Waypoint> { new Waypoint { pos = cellules[b].pos, sautVersSuivant = false } };
        }

        // Le test de connexité rend la réponse "injoignable" quasi gratuite :
        //   - MÊME îlot => un chemin EXISTE : budget = taille de la grille pour GARANTIR de le
        //     trouver, aussi long que soit le détour (spec "Persistent Hunter" : jamais
        //     abandonner une plateforme joignable, même par un immense contournement).
        //   - îlots DIFFÉRENTS => injoignable : petit budget pour juste construire l'approche.
        bool memeIlot = composante[a] == composante[b];
        int budget = memeIlot ? cellules.Length + 16 : 8000;

        // Pathing en HAUTEUR : si la cible est nettement plus haut/bas (>2m), on gonfle le
        // poids vertical de l'heuristique pour que la recherche PRIORISE la montée (rampes,
        // escaliers) au lieu d'inonder le sol et d'exploser le budget avant de trouver l'accès.
        float dyTotal = MathF.Abs(cellules[b].pos.Y - cellules[a].pos.Y);
        float poidsY = dyTotal > 2f ? 3.0f : 1.2f;

        var ouverts = new PriorityQueue<int, float>();
        var gScore = new Dictionary<int, float>();
        var vientDe = new Dictionary<int, int>();
        var arriveParSaut = new Dictionary<int, bool>();
        var fermes = new HashSet<int>();

        gScore[a] = 0f;
        ouverts.Enqueue(a, Heuristique(a, b, poidsY));

        int explores = 0;
        int meilleurNoeud = a;                    // pour le chemin d'approche partiel
        float meilleureDistance = Heuristique(a, b, poidsY);

        while (ouverts.Count > 0)
        {
            int courant = ouverts.Dequeue();
            if (courant == b)
            {
                complet = true;
                return ReconstruireChemin(vientDe, arriveParSaut, a, b);
            }
            if (!fermes.Add(courant)) continue; // entrée périmée (déjà fermée)

            // On compte les cases RÉELLEMENT fermées (uniques), pas les défilements de la file :
            // avec le coût x10, une case est ré-enfilée souvent, donc compter les dequeues
            // épuiserait le budget bien avant d'avoir exploré l'îlot. budget = taille grille
            // -> on peut fermer tout l'îlot, donc on trouve TOUJOURS un chemin qui existe.
            if (++explores > budget) break;

            float distCible = Heuristique(courant, b, poidsY);
            if (distCible < meilleureDistance) { meilleureDistance = distCible; meilleurNoeud = courant; }

            float gCourant = gScore[courant];

            // Arêtes de marche. Coût = distance x multiplicateur de la case d'arrivée
            // (x10 pour une case "orange" qui frôle un mur -> l'A* la contourne).
            foreach (int voisin in voisins[courant])
            {
                if (bloque[voisin] || fermes.Contains(voisin)) continue;
                float cout = gCourant + Vector3.Distance(cellules[courant].pos, cellules[voisin].pos) * coutMult[voisin];
                if (!gScore.TryGetValue(voisin, out float ancien) || cout < ancien)
                {
                    gScore[voisin] = cout;
                    vientDe[voisin] = courant;
                    arriveParSaut[voisin] = false;
                    ouverts.Enqueue(voisin, cout + Heuristique(voisin, b, poidsY));
                }
            }

            // Arêtes de saut (plus chères : on préfère marcher quand c'est possible)
            foreach (int cible in sauts[courant])
            {
                if (bloque[cible] || fermes.Contains(cible)) continue;
                float cout = gCourant + (Vector3.Distance(cellules[courant].pos, cellules[cible].pos) * 1.5f + 2f) * coutMult[cible];
                if (!gScore.TryGetValue(cible, out float ancien) || cout < ancien)
                {
                    gScore[cible] = cout;
                    vientDe[cible] = courant;
                    arriveParSaut[cible] = true;
                    ouverts.Enqueue(cible, cout + Heuristique(cible, b, poidsY));
                }
            }
        }

        // Cible non atteinte : on renvoie le chemin d'APPROCHE (s'il fait avancer les choses).
        // Exemple : joueur perché sur un toit injoignable -> les zombies s'agglutinent au pied.
        if (meilleurNoeud != a)
        {
            return ReconstruireChemin(vientDe, arriveParSaut, a, meilleurNoeud);
        }
        return null;
    }

    // Heuristique légèrement "gonflée" (A* pondéré x1.3) : chemins quasi optimaux mais
    // exploration bien plus resserrée — indispensable sur les grosses maps (100k+ cases).
    // poidsY : poids du dénivelé (gonflé quand la cible est en hauteur, cf. TrouverChemin).
    static float Heuristique(int i, int j, float poidsY)
    {
        Vector3 d = cellules[j].pos - cellules[i].pos;
        return (MathF.Sqrt(d.X * d.X + d.Z * d.Z) + MathF.Abs(d.Y) * poidsY) * 1.3f;
    }

    static List<Waypoint> ReconstruireChemin(Dictionary<int, int> vientDe, Dictionary<int, bool> arriveParSaut, int depart, int arrivee)
    {
        var indices = new List<int>();
        int courant = arrivee;
        indices.Add(courant);
        while (courant != depart)
        {
            courant = vientDe[courant];
            indices.Add(courant);
        }
        indices.Reverse();

        var chemin = new List<Waypoint>(indices.Count);
        for (int k = 0; k < indices.Count; k++)
        {
            bool saut = k + 1 < indices.Count && arriveParSaut.TryGetValue(indices[k + 1], out bool s) && s;
            chemin.Add(new Waypoint { pos = cellules[indices[k]].pos, sautVersSuivant = saut });
        }
        return Lisser(chemin);
    }

    // ==========================================
    // LISSAGE : on saute les waypoints intermédiaires quand la ligne droite est marchable
    // (jamais à travers une arête de saut !)
    // ==========================================
    static List<Waypoint> Lisser(List<Waypoint> brut)
    {
        if (brut.Count <= 2) return brut;
        var resultat = new List<Waypoint> { brut[0] };
        int i = 0;

        while (i < brut.Count - 1)
        {
            if (brut[i].sautVersSuivant)
            {
                // On ne lisse JAMAIS par-dessus un saut : le waypoint d'atterrissage doit rester exact.
                resultat.Add(brut[i + 1]);
                i++;
                continue;
            }

            int meilleur = i + 1;
            int limite = Math.Min(i + 6, brut.Count - 1);
            for (int j = limite; j > i + 1; j--)
            {
                bool contientSaut = false;
                for (int k = i; k < j; k++)
                {
                    if (brut[k].sautVersSuivant) { contientSaut = true; break; }
                }
                if (contientSaut) continue;

                if (LigneMarchable(brut[i].pos, brut[j].pos)) { meilleur = j; break; }
            }
            resultat.Add(brut[meilleur]);
            i = meilleur;
        }
        return resultat;
    }

    // Une ligne droite est "marchable" si : hauteurs proches, rien aux genoux ni à la tête,
    // et du sol TOUT LE LONG (pas de trou caché entre les deux points).
    static bool LigneMarchable(Vector3 a, Vector3 b)
    {
        if (MathF.Abs(a.Y - b.Y) > 1.0f) return false;
        var sim = Program.simulation;

        if (!RayonDegage(sim, a + new Vector3(0, 0.4f, 0), b + new Vector3(0, 0.4f, 0), true)) return false;
        if (!RayonDegage(sim, a + new Vector3(0, 1.5f, 0), b + new Vector3(0, 1.5f, 0), true)) return false;

        Vector3 delta = b - a;
        float dist = delta.Length();
        int pas = (int)(dist / 0.9f);
        for (int k = 1; k <= pas; k++)
        {
            Vector3 p = a + delta * (k / (float)(pas + 1));
            NavRaySensor sol = new NavRaySensor(true);
            sim.RayCast(p + new Vector3(0, 0.5f, 0), new Vector3(0, -1, 0), 1.6f, ref sol);
            if (!sol.aTouche) return false;              // trou !
            if (EstBloqueEn(p)) return false;            // traverse une case bloquée (pilier, mur posé)
        }
        return true;
    }

    // Y a-t-il une case à ÉVITER à cette position (mur posé, ou case collée à un mur) ?
    // Sert au lissage/look-ahead : les rayons ne voient que la map, pas les murs posés ni
    // le concept "trop près d'un mur" — cette vérification niveau-grille comble le trou
    // (sinon on lisserait un raccourci droit à travers un mur posé ou en frottant un pilier).
    static bool EstBloqueEn(Vector3 pos)
    {
        int gx = (int)MathF.Round(pos.X / CELLULE);
        int gz = (int)MathF.Round(pos.Z / CELLULE);
        if (!colonnes.TryGetValue((gx, gz), out var etages)) return false;
        foreach (int idx in etages)
            if ((bloque[idx] || coutMult[idx] >= MULT_BLOQUE) && MathF.Abs(cellules[idx].pos.Y - pos.Y) < 1.0f) return true;
        return false;
    }

    // Version publique pour le "string pulling" des zombies : peut-on aller de a à b
    // en ligne droite marchable ? (a et b sont des positions AU SOL / aux pieds.)
    public static bool CheminLibre(Vector3 a, Vector3 b)
    {
        if (!EstPrete) return false;
        return LigneMarchable(a, b);
    }

    // ==========================================
    // ERRANCE : un point marchable au hasard autour d'une position
    // ==========================================
    public static bool PointErranceAleatoire(Vector3 autour, float rayon, out Vector3 point)
    {
        point = default;
        if (!EstPrete) return false;

        int origine = CelluleProche(autour);
        if (origine < 0) return false;

        int gx = cellules[origine].gx;
        int gz = cellules[origine].gz;
        float y = cellules[origine].pos.Y;
        int rayonCases = Math.Max(2, (int)(rayon / CELLULE));

        for (int essai = 0; essai < 12; essai++)
        {
            int cx = gx + rng.Next(-rayonCases, rayonCases + 1);
            int cz = gz + rng.Next(-rayonCases, rayonCases + 1);
            int idx = ChercherNiveau(cx, cz, y, 3f);
            // Non bloquée + même îlot de connexité : sinon le zombie choisirait une destination
            // inmarchable ou derrière un mur et passerait sa promenade à pousser contre.
            if (idx >= 0 && idx != origine && !bloque[idx] && composante[idx] == composante[origine])
            {
                point = cellules[idx].pos;
                return true;
            }
        }
        return false;
    }

    // ==========================================
    // SNAP : recale une position sur le sol marchable le plus proche.
    // Utilisé au spawn des zombies pour qu'une coordonnée approximative (ou fausse)
    // ne fasse jamais apparaître un zombie hors map ou dans un mur.
    // ==========================================
    public static bool SnapAuSol(Vector3 pos, out Vector3 posSol)
    {
        posSol = pos;
        int idx = CelluleProche(pos);
        if (idx < 0) return false;
        posSol = cellules[idx].pos;
        return true;
    }

    // ==========================================
    // DEBUG : renvoie le premier lien de saut dont le trou horizontal >= gapMin.
    // Sert au test runtime (--aitest) à placer un zombie et sa cible de part et d'autre
    // d'un vrai saut, de façon déterministe et indépendante de la géométrie de la map.
    // ==========================================
    // dropMin > 0 : ne renvoie qu'un saut qui DESCEND d'au moins dropMin (ledge). Un saut
    // vers le bas force l'IA à bondir (impossible de descendre une marche > MARCHE_MAX en
    // marchant) et sort du cas "corps à corps à plat", ce qui rend le test de saut fiable.
    public static bool DebugPremierSaut(float gapMin, float dropMin, out Vector3 depart, out Vector3 arrivee)
    {
        depart = arrivee = default;
        for (int i = 0; i < cellules.Length; i++)
        {
            if (bloque[i]) continue;
            foreach (int c in sauts[i])
            {
                if (bloque[c]) continue;
                Vector3 a = cellules[i].pos, b = cellules[c].pos;
                float dx = a.X - b.X, dz = a.Z - b.Z;
                if (MathF.Sqrt(dx * dx + dz * dz) < gapMin) continue;
                if (dropMin > 0f && a.Y - b.Y < dropMin) continue;
                depart = a; arrivee = b; return true;
            }
        }
        return false;
    }

    // ==========================================
    // DEBUG : détaille pourquoi une case est (ou non) reliée à ses 8 voisines.
    // "vide" = pas de case, "EVIT" = quasi-bloquée (x1000), "BLOQ" = mur posé,
    // "MUR" = un mur s'intercale entre les deux cases, "OK" = arête utilisable.
    // Très pratique quand un zombie semble coincé ou refuse un passage.
    // ==========================================
    public static string DebugVoisinage(Vector3 pos)
    {
        int i = CelluleProche(pos);
        if (i < 0) return "aucune case";
        ref var c = ref cellules[i];
        (int dx, int dz)[] d8 = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };
        var sb = new System.Text.StringBuilder($"case {c.pos} : ");
        foreach (var (dx, dz) in d8)
        {
            int j = ChercherNiveau(c.gx + dx, c.gz + dz, c.pos.Y, MARCHE_MAX);
            string etat;
            if (j < 0) etat = "vide";
            else if (bloque[j]) etat = "BLOQ";
            else if (coutMult[j] >= MULT_BLOQUE) etat = "EVIT";
            else if (MurEntre(Program.simulation, c.pos + new Vector3(0, 0.9f, 0), cellules[j].pos + new Vector3(0, 0.9f, 0))) etat = "MUR";
            else etat = "OK";
            sb.Append($"({dx},{dz})={etat} ");
        }
        return sb.ToString();
    }

    // ==========================================
    // DEBUG : trouve une case nettement plus HAUTE que "depuis" (>= minMontee), soit
    // dans le MÊME îlot (joignable par rampe/détour), soit dans un autre (perchoir isolé).
    // Sert au test runtime du pathing en hauteur (--aitest).
    // ==========================================
    public static bool DebugCibleEnHauteur(Vector3 depuis, float minMontee, bool memeIlot, out Vector3 cible)
    {
        cible = default;
        int a = CelluleProche(depuis);
        if (a < 0) return false;
        float yA = cellules[a].pos.Y;
        for (int i = 0; i < cellules.Length; i++)
        {
            if (bloque[i]) continue;
            if (cellules[i].pos.Y < yA + minMontee) continue;
            bool same = composante[i] == composante[a];
            if (same != memeIlot) continue;
            cible = cellules[i].pos;
            return true;
        }
        return false;
    }

    // ==========================================
    // DEBUG : décrire la cellule la plus proche d'un point (pour le navtest)
    // ==========================================
    public static string InfoCellule(Vector3 pos)
    {
        int idx = CelluleProche(pos);
        if (idx < 0) return "AUCUNE CELLULE dans un rayon de 5 cases";
        int tailleIlot = 0;
        for (int i = 0; i < composante.Length; i++) if (composante[i] == composante[idx]) tailleIlot++;
        return $"cellule {cellules[idx].pos} | {voisins[idx].Length} voisins, {sauts[idx].Length} sauts | îlot de {tailleIlot} cases";
    }

    // ==========================================
    // DEBUG : visualiser la grille autour d'un point (activé par debugInfo)
    // ==========================================
    public static void DessinerDebug(Vector3 centre, float rayon)
    {
        if (!EstPrete) return;
        float rayonCarre = rayon * rayon;

        for (int i = 0; i < cellules.Length; i++)
        {
            Vector3 p = cellules[i].pos;
            float dx = p.X - centre.X, dz = p.Z - centre.Z;
            if (dx * dx + dz * dz > rayonCarre) continue;

            // Rouge = case inmarchable (mur posé dessus) ou quasi-bloquée (<0.5m d'un mur,
            // coût x1000) ; orange = case à coût x10 (frôle un mur) ; vert = dégagée.
            Color couleur = (bloque[i] || coutMult[i] >= MULT_BLOQUE) ? Color.Red
                          : (coutMult[i] > 1.01f ? Color.Orange : Color.Green);
            Raylib.DrawCubeV(p + new Vector3(0, 0.05f, 0), new Vector3(0.15f, 0.02f, 0.15f), couleur);

            if (bloque[i]) continue; // pas de liens de saut affichés depuis une case bloquée

            foreach (int cible in sauts[i])
            {
                Raylib.DrawLine3D(p + new Vector3(0, 0.4f, 0), cellules[cible].pos + new Vector3(0, 0.4f, 0), Color.Yellow);
            }
        }
    }
}
