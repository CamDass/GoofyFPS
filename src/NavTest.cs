using System.Numerics;
using Raylib_cs;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;

// ========================================================================================
// AUTO-TEST DE NAVIGATION : `dotnet run -- --navtest`
// ----------------------------------------------------------------------------------------
// Charge chaque map dans une fenêtre CACHÉE (pas de jeu, pas de son), cuit la NavGrid
// dessus, puis vérifie que l'A* trouve un chemin entre les vrais points de spawn
// (ennemi <-> ennemi et ennemi -> joueur). Sert à valider le bake sans lancer une partie.
// ========================================================================================
partial class Program
{
    static void LancerNavTest()
    {
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
        Raylib.InitWindow(320, 200, "navtest");

        pool = new BufferPool();
        Vector3 gravity = new Vector3(0, -10f, 0);
        simulation = Simulation.Create(pool, new NarrowPhaseCallbacks(), new PoseIntegratorCallbacks(gravity), new SolveDescription(8, 1));

        string[] cheminsMap = { "assets/models/maps/test.glb", "assets/models/maps/map.glb", "assets/models/maps/sandbox.glb", "assets/models/maps/blockmap.glb" };
        string[] nomsMap = { "Tuto/Test", "La Ville", "Sandbox", "Blocs" };
        bool toutOk = true;

        for (int i = 0; i < cheminsMap.Length; i++)
        {
            if (!File.Exists(cheminsMap[i]))
            {
                Console.WriteLine($"[NAVTEST] map {i} ({nomsMap[i]}) : fichier introuvable, ignorée");
                continue;
            }

            mapModel = Raylib.LoadModel(cheminsMap[i]);
            ExtraireTrianglesMap();
            LoadMapSpawns(i);

            NavGrid.Bake(simulation, Raylib.GetModelBoundingBox(mapModel), mapScale, mapPosition);

            int ok = 0, echec = 0;

            if (i == 3)
                for (int a = 0; a < enemySpawnPoints.Count; a++)
                    Console.WriteLine($"[DBG] ennemi{a} {enemySpawnPoints[a]} : {NavGrid.InfoCellule(enemySpawnPoints[a])}");

            // Diagnostic : chaque spawn ennemi doit être posé sur la grille (les zombies
            // apparaissent LÀ : hors grille = zombie qui tombe dans le vide ou IA de secours).
            for (int a = 0; a < enemySpawnPoints.Count; a++)
            {
                string info = NavGrid.InfoCellule(enemySpawnPoints[a]);
                if (info.StartsWith("AUCUNE"))
                {
                    echec++;
                    Console.WriteLine($"[NAVTEST]   ECHEC spawn ennemi{a} {enemySpawnPoints[a]} : {info}");
                }
            }

            int perchoirs = 0;

            // Ennemi <-> ennemi : toutes les paires de points de spawn (au sol : chemin COMPLET exigé)
            for (int a = 0; a < enemySpawnPoints.Count; a++)
            {
                for (int b = a + 1; b < enemySpawnPoints.Count; b++)
                {
                    var chemin = NavGrid.TrouverChemin(enemySpawnPoints[a], enemySpawnPoints[b], out bool complet);
                    if (chemin != null && complet) ok++;
                    else { echec++; Console.WriteLine($"[NAVTEST]   ECHEC ennemi{a} {enemySpawnPoints[a]} -> ennemi{b} {enemySpawnPoints[b]} (chemin={(chemin == null ? "aucun" : "partiel")})"); }
                }
            }

            // Ennemi -> spawns joueur (le trajet le plus important du jeu !)
            // Cas accepté : spawn joueur PERCHÉ (toit, tour) injoignable à pied -> le chemin
            // d'approche partiel est le comportement voulu (les zombies s'agglutinent en dessous).
            foreach (Vector3 spawnJoueur in listeSpawns)
            {
                var chemin = NavGrid.TrouverChemin(enemySpawnPoints[0], spawnJoueur, out bool complet);
                bool perche = spawnJoueur.Y - enemySpawnPoints[0].Y > 3f;

                if (chemin != null && complet) ok++;
                else if (chemin != null && perche) { ok++; perchoirs++; }
                else { echec++; Console.WriteLine($"[NAVTEST]   ECHEC ennemi0 -> joueur {spawnJoueur} (chemin={(chemin == null ? "aucun" : "partiel")}, perché={perche})"); }
            }

            string verdict = echec == 0 ? "OK" : "PROBLEME";
            string notePerchoirs = perchoirs > 0 ? $" (dont {perchoirs} perchoirs en approche partielle)" : "";
            Console.WriteLine($"[NAVTEST] map {i} ({nomsMap[i]}) : {NavGrid.NbCellules} cases, chemins {ok}/{ok + echec}{notePerchoirs} -> {verdict}");
            if (echec > 0) toutOk = false;

            DechargerMapActuelle();
        }

        Raylib.CloseWindow();
        Console.WriteLine(toutOk ? "[NAVTEST] TOUT EST OK" : "[NAVTEST] DES CHEMINS SONT INTROUVABLES (voir ci-dessus)");
        Environment.Exit(toutOk ? 0 : 1);
    }

    // ========================================================================================
    // AUTO-TEST DU COMPORTEMENT : `dotnet run -- --aitest`
    // ----------------------------------------------------------------------------------------
    // Fait tourner la VRAIE physique à travers Enemy.Maj (fenêtre cachée) et mesure les points
    // sur lesquels portait le retour de playtest :
    //   - le zombie rejoint bien le joueur (le pathfinding aboutit en conditions réelles) ;
    //   - la vitesse reste finie et bornée (l'intégration douce n'explose pas) ;
    //   - PAS de "zombie gelé" : quand le joueur recule d'un cheveu, le zombie ne se fige pas
    //     hors de portée (bug #1) ;
    //   - mouvement fluide : la variation de vitesse par frame reste petite hors sauts (bug #2).
    // Headless : GetFrameTime() renvoie 0 -> le garde-fou de Maj fixe dt=1/60, donc déterministe.
    // ========================================================================================
    static void LancerAiTest()
    {
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
        Raylib.InitWindow(320, 200, "aitest");
        Raylib.InitAudioDevice();

        pool = new BufferPool();
        simulation = Simulation.Create(pool, new NarrowPhaseCallbacks(), new PoseIntegratorCallbacks(new Vector3(0, -10f, 0)), new SolveDescription(8, 1));

        // Map Blocs (petite, entièrement connexe d'après le navtest)
        int mapIdx = 3;
        string[] chemins = { "assets/models/maps/test.glb", "assets/models/maps/map.glb", "assets/models/maps/sandbox.glb", "assets/models/maps/blockmap.glb" };
        mapModel = Raylib.LoadModel(chemins[mapIdx]);
        ExtraireTrianglesMap();
        LoadMapSpawns(mapIdx);
        NavGrid.Bake(simulation, Raylib.GetModelBoundingBox(mapModel), mapScale, mapPosition);

        const float dt = 1f / 60f;
        const float PORTEE_ATTAQUE = 1.7f; // doit rester en phase avec Enemy.PORTEE_ATTAQUE

        // Headless : pas de rendu -> GetFrameTime() renverrait un delta réel minuscule et non
        // déterministe. On force le dt du cerveau pour qu'il colle au pas de physique fixe.
        Enemy.dtOverride = dt;

        // --- Le "joueur" : un corps dynamique qu'on téléporte à une position scriptée ---
        if (!NavGrid.SnapAuSol(enemySpawnPoints[5], out Vector3 cibleSol)) cibleSol = enemySpawnPoints[5];
        Vector3 playerPos = cibleSol + new Vector3(0, 1.0f, 0);
        Capsule formeJoueur = new Capsule(0.5f, 1f);
        TypedIndex idxJoueur = simulation.Shapes.Add(formeJoueur);
        BodyDescription descJoueur = BodyDescription.CreateDynamic(playerPos, formeJoueur.ComputeInertia(1f), idxJoueur, 0.01f);
        BodyHandle handleJoueur = simulation.Bodies.Add(descJoueur);

        // --- Le zombie, posé sur la grille au spawn 0 ---
        Vector3 zSpawn = NavGrid.SnapAuSol(enemySpawnPoints[0], out Vector3 zSol) ? zSol + new Vector3(0, 1.05f, 0) : enemySpawnPoints[0];
        Enemy zombie = new Enemy(zSpawn, 100, 4.0f);
        camera.Position = playerPos;

        // On veut tester le DÉPLACEMENT (rejoint/anti-gel/fluidité), pas la perception : à 18m
        // sur les Blocs le zombie peut spawn sans ligne de vue (donc en errance, comportement
        // normal). On déclenche l'aggro comme si le joueur lui avait tiré dessus -> il fonce.
        zombie.TakeDamage(0, playerPos);

        // --- Métriques ---
        float distDepart = DistHoriz(zSpawn, playerPos);
        float minDistPhaseA = float.MaxValue;
        float maxVitesse = 0f;
        bool nanDetecte = false;
        int framesGel = 0, framesTestGel = 0;
        var deltasVitesse = new List<float>();
        Vector3 vitessePrec = Vector3.Zero;
        bool premiereFrame = true;

        float duree = 13f;
        int totalFrames = (int)(duree / dt);

        for (int f = 0; f < totalFrames; f++)
        {
            float temps = f * dt;

            // Phase A (0-8s) : joueur immobile, cible lointaine -> le zombie doit le rejoindre.
            // Phase B (8-13s) : le joueur fait des micro-pas (±0.35m toutes les 0.35s) pour
            // reproduire EXACTEMENT le scénario du bug "zombie gelé" (recul/knockback/glissade).
            if (temps >= 8f)
            {
                float phase = (temps - 8f);
                float dec = ((int)(phase / 0.35f) % 2 == 0) ? 0.35f : -0.35f;
                playerPos = cibleSol + new Vector3(dec, 1.0f, dec * 0.5f);
            }

            // Téléporter le joueur à la position scriptée (immunisé gravité/knockback)
            BodyReference corpsJoueur = simulation.Bodies.GetBodyReference(handleJoueur);
            corpsJoueur.Pose.Position = playerPos;
            corpsJoueur.Velocity.Linear = Vector3.Zero;
            corpsJoueur.Awake = true;
            camera.Position = playerPos;

            // Garder le joueur en vie (on teste le déplacement, pas la mort) et à distance de vue.
            Program.localPlayer.Respawn();

            // LE CERVEAU puis un pas de physique fixe (comme la boucle du jeu)
            zombie.Maj(playerPos, ref corpsJoueur);
            simulation.Timestep(dt);

            // Re-téléporter le joueur (annule la chute pendant le pas)
            corpsJoueur = simulation.Bodies.GetBodyReference(handleJoueur);
            corpsJoueur.Pose.Position = playerPos;
            corpsJoueur.Velocity.Linear = Vector3.Zero;

            // --- Mesures ---
            Vector3 zPos = zombie.GetPosition();
            BodyReference zBody = simulation.Bodies.GetBodyReference(zombie.bodyId);
            Vector3 vel = zBody.Velocity.Linear;
            Vector3 velH = new Vector3(vel.X, 0, vel.Z);
            float vitesseH = velH.Length();

            if (!Finite(zPos) || !Finite(vel)) nanDetecte = true;
            if (vitesseH > maxVitesse) maxVitesse = vitesseH;

            float dist = DistHoriz(zPos, playerPos);
            if (temps < 8f) minDistPhaseA = MathF.Min(minDistPhaseA, dist);

            // Fluidité : variation de vitesse horizontale entre deux frames (hors 1re frame)
            if (!premiereFrame) deltasVitesse.Add((velH - vitessePrec).Length());
            vitessePrec = velH;
            premiereFrame = false;

            // Gel : en phase B, à portée moyenne (1.7-4m) avec vue dégagée, le zombie
            // NE DOIT PAS être quasi immobile (il doit recoller). On échantillonne ce cas.
            if (temps >= 8.5f && dist > PORTEE_ATTAQUE && dist < 4f)
            {
                framesTestGel++;
                if (vitesseH < 0.3f) framesGel++;
            }
        }

        // ========================================================================
        // SCÉNARIO 2 : LE SAUT (déterministe, indépendant de la géométrie)
        // On recharge la sandbox, on prend un VRAI lien de saut de la grille, et on place
        // le zombie + sa cible de part et d'autre du trou. On vérifie qu'il bondit et
        // atterrit proprement (le refactor de vitesse ne doit pas avoir cassé le saut).
        // ========================================================================
        simulation.Bodies.Remove(zombie.bodyId);
        simulation.Bodies.Remove(handleJoueur);
        DechargerMapActuelle();

        bool sautObserve = false, sautInconclusif = false, atterriProprement = true, sautReache = false, sautFini = true;
        int nbSauts = 0;
        float maxVy = 0f;

        mapModel = Raylib.LoadModel(chemins[2]); // sandbox : 3691 liens de saut
        ExtraireTrianglesMap();
        LoadMapSpawns(2);
        NavGrid.Bake(simulation, Raylib.GetModelBoundingBox(mapModel), mapScale, mapPosition);

        // On vise un saut qui DESCEND (ledge) : déterministe, et la seule façon d'y aller
        // est de bondir (pas de marche possible sur une chute > 0.55m).
        if (NavGrid.DebugPremierSaut(0f, 2f, out Vector3 sautA, out Vector3 sautB))
        {
            Vector3 cibleSaut = sautB + new Vector3(0, 1.0f, 0);
            BodyDescription dJ = BodyDescription.CreateDynamic(cibleSaut, formeJoueur.ComputeInertia(1f), simulation.Shapes.Add(formeJoueur), 0.01f);
            BodyHandle hJ = simulation.Bodies.Add(dJ);
            Enemy sauteur = new Enemy(sautA + new Vector3(0, 1.05f, 0), 100, 4.0f);
            camera.Position = cibleSaut;

            bool enLair = false;
            float chronoAir = 0f;
            int framesSaut = (int)(15f / dt);
            for (int f = 0; f < framesSaut; f++)
            {
                BodyReference cj = simulation.Bodies.GetBodyReference(hJ);
                cj.Pose.Position = cibleSaut; cj.Velocity.Linear = Vector3.Zero; cj.Awake = true;
                Program.localPlayer.Respawn();

                sauteur.Maj(cibleSaut, ref cj);
                simulation.Timestep(dt);
                cj = simulation.Bodies.GetBodyReference(hJ);
                cj.Pose.Position = cibleSaut; cj.Velocity.Linear = Vector3.Zero;

                BodyReference sb = simulation.Bodies.GetBodyReference(sauteur.bodyId);
                Vector3 sp = sb.Pose.Position;
                float vy = sb.Velocity.Linear.Y;
                maxVy = MathF.Max(maxVy, vy);
                if (!Finite(sp) || !Finite(sb.Velocity.Linear)) sautFini = false;
                if (sp.Y < -25f) atterriProprement = false; // tombé dans le vide

                // Détection d'un saut : décollage franc (vy > 1.8 ; un saut vers le bas ne
                // s'élance qu'à ~2.5 m/s, alors que la marche ne dépasse pas ~0.7). Puis atterrissage.
                if (!enLair && vy > 1.8f) { enLair = true; chronoAir = 0f; nbSauts++; sautObserve = true; }
                else if (enLair)
                {
                    chronoAir += dt;
                    if (MathF.Abs(vy) < 0.2f && sb.Velocity.Linear.Length() < 1f) enLair = false; // posé
                    if (chronoAir > 4f) { atterriProprement = false; enLair = false; } // coincé en l'air
                }

                if (DistHoriz(sp, cibleSaut) < 2.5f) sautReache = true;
            }
            simulation.Bodies.Remove(sauteur.bodyId);
            simulation.Bodies.Remove(hJ);
        }
        else sautInconclusif = true;

        // ========================================================================
        // SCÉNARIO 3 : PATHING EN HAUTEUR ("Persistent Hunter") — sur la sandbox
        // Une plateforme joignable (même îlot, >3m plus haut) DOIT donner un chemin complet
        // (l'A* prend le détour, quel qu'il soit). Un perchoir isolé (autre îlot) donne un
        // chemin d'approche PARTIEL (le zombie s'agglutine au pied).
        // ========================================================================
        Vector3 basSandbox = enemySpawnPoints[0];
        bool hauteurInconclusif = false, okHauteurJoignable = false, okHauteurPerchoir = false;

        if (NavGrid.DebugCibleEnHauteur(basSandbox, 3f, true, out Vector3 hautJoignable))
        {
            NavGrid.TrouverChemin(basSandbox, hautJoignable, out bool cHaut);
            okHauteurJoignable = cHaut; // plateforme joignable -> chemin COMPLET obligatoire
        }
        else hauteurInconclusif = true;

        if (NavGrid.DebugCibleEnHauteur(basSandbox, 3f, false, out Vector3 perchoir))
        {
            var pp = NavGrid.TrouverChemin(basSandbox, perchoir, out bool cPerch);
            okHauteurPerchoir = !cPerch && pp != null; // isolé -> approche partielle
        }
        else okHauteurPerchoir = true; // pas de perchoir isolé sur cette map : non bloquant

        // ========================================================================
        // SCÉNARIO 4 : MUR POSÉ DYNAMIQUEMENT (re-bake local) — sur les Blocs
        // On calcule un chemin, on bloque une boîte au milieu (comme un mur posé), et on
        // vérifie : (a) des cases sont bien bloquées, (b) le chemin reste joignable mais
        // CONTOURNE la zone, (c) un mur placé tout en haut (plafond) ne bloque rien.
        // ========================================================================
        DechargerMapActuelle();
        mapModel = Raylib.LoadModel(chemins[3]); // Blocs
        ExtraireTrianglesMap();
        LoadMapSpawns(3);
        NavGrid.Bake(simulation, Raylib.GetModelBoundingBox(mapModel), mapScale, mapPosition);

        bool murInconclusif = false, okMurBloque = false, okMurDetour = false, okMurHaut = false;

        Vector3 murA = NavGrid.SnapAuSol(enemySpawnPoints[0], out Vector3 sa) ? sa : enemySpawnPoints[0];
        Vector3 murB = NavGrid.SnapAuSol(enemySpawnPoints[5], out Vector3 sb2) ? sb2 : enemySpawnPoints[5];
        var chemin1 = NavGrid.TrouverChemin(murA, murB, out bool cAvant);

        if (chemin1 != null && cAvant && chemin1.Count >= 3)
        {
            Vector3 milieu = chemin1[chemin1.Count / 2].pos;
            float minAvant = CheminMinDist(chemin1, milieu);

            // (a) mur au SOL en travers du chemin (haut de 2.5m -> gêne le zombie)
            var bloquees = NavGrid.BloquerBoite(milieu - new Vector3(1f, 0.5f, 1f), milieu + new Vector3(1f, 2.5f, 1f));
            okMurBloque = bloquees.Count > 0;

            var chemin2 = NavGrid.TrouverChemin(murA, murB, out bool cApres);
            float minApres = chemin2 != null ? CheminMinDist(chemin2, milieu) : 0f;
            // Toujours joignable, mais le nouveau chemin s'écarte du point bloqué (contournement).
            okMurDetour = cApres && chemin2 != null && minApres > minAvant + 0.4f;

            // (c) mur tout en HAUT (plafond à +3m) : ne doit bloquer AUCUNE case au sol.
            var bloqueesHaut = NavGrid.BloquerBoite(murB + new Vector3(-1f, 3f, -1f), murB + new Vector3(1f, 5f, 1f));
            okMurHaut = bloqueesHaut.Count == 0;
        }
        else murInconclusif = true;

        // --- Verdict ---
        deltasVitesse.Sort();
        float p95 = deltasVitesse.Count > 0 ? deltasVitesse[(int)(deltasVitesse.Count * 0.95f)] : 0f;
        float deltaMax = deltasVitesse.Count > 0 ? deltasVitesse[^1] : 0f;
        float fractionGel = framesTestGel > 0 ? (float)framesGel / framesTestGel : 0f;
        // Seuil de fluidité : ACCEL(45)*dt ≈ 0.75 m/s par frame ; on tolère x2.2 pour le bruit
        // des contacts physiques. Les sauts, eux, apparaissent comme des pics au-delà (deltaMax).
        float seuilFluidite = 45f * dt * 2.2f;

        bool okReache = minDistPhaseA < 2.0f;
        bool okBornee = maxVitesse < 8.5f;          // < SAUT_VITESSE_H_MAX(7) + marge
        bool okFinite = !nanDetecte;
        bool okGel = fractionGel < 0.15f;
        bool okFluide = p95 <= seuilFluidite;
        // Saut : soit on a observé un saut propre (décollage + atterrissage, pas de chute
        // dans le vide, cible atteinte), soit le test n'a trouvé aucun lien (inconcluant, non bloquant).
        bool okSaut = sautInconclusif || (sautObserve && atterriProprement && sautFini && sautReache);
        bool okHauteur = hauteurInconclusif || (okHauteurJoignable && okHauteurPerchoir);
        bool okMur = murInconclusif || (okMurBloque && okMurDetour && okMurHaut);
        bool tout = okReache && okBornee && okFinite && okGel && okFluide && okSaut && okHauteur && okMur;

        Console.WriteLine("[AITEST] --- Comportement runtime ---");
        Console.WriteLine($"[AITEST] [Blocs] Distance de départ : {distDepart:F1} m");
        Console.WriteLine($"[AITEST] [Blocs] Rejoint le joueur (phase A)   : min {minDistPhaseA:F2} m           -> {(okReache ? "OK" : "ECHEC")}");
        Console.WriteLine($"[AITEST] [Blocs] Vitesse bornée & finie         : max {maxVitesse:F2} m/s, NaN={nanDetecte} -> {((okBornee && okFinite) ? "OK" : "ECHEC")}");
        Console.WriteLine($"[AITEST] [Blocs] Anti-gel (phase micro-pas)     : {framesGel}/{framesTestGel} frames figées ({fractionGel * 100f:F0}%) -> {(okGel ? "OK" : "ECHEC")}");
        Console.WriteLine($"[AITEST] [Blocs] Fluidité (Δvitesse p95)        : {p95:F2} m/s (seuil {seuilFluidite:F2}, pic saut {deltaMax:F2}) -> {(okFluide ? "OK" : "ECHEC")}");
        if (sautInconclusif)
            Console.WriteLine("[AITEST] [Sandbox] Saut : aucun lien de saut trouvé -> INCONCLUANT (non bloquant)");
        else
            Console.WriteLine($"[AITEST] [Sandbox] Saut balistique : {nbSauts} saut(s), vY max {maxVy:F1} m/s, atterri={atterriProprement}, cible atteinte={sautReache} -> {(okSaut ? "OK" : "ECHEC")}");
        if (hauteurInconclusif)
            Console.WriteLine("[AITEST] [Sandbox] Hauteur : aucune plateforme joignable +3m -> INCONCLUANT");
        else
            Console.WriteLine($"[AITEST] [Sandbox] Pathing hauteur : plateforme joignable={okHauteurJoignable}, perchoir isolé=approche partielle={okHauteurPerchoir} -> {(okHauteur ? "OK" : "ECHEC")}");
        if (murInconclusif)
            Console.WriteLine("[AITEST] [Blocs] Mur dynamique : pas de chemin de base -> INCONCLUANT");
        else
            Console.WriteLine($"[AITEST] [Blocs] Mur posé (re-bake) : cases bloquées={okMurBloque}, contournement={okMurDetour}, plafond ignoré={okMurHaut} -> {(okMur ? "OK" : "ECHEC")}");

        Raylib.CloseWindow();
        Console.WriteLine(tout ? "[AITEST] TOUT EST OK" : "[AITEST] COMPORTEMENT A REVOIR (voir ci-dessus)");
        Environment.Exit(tout ? 0 : 1);
    }

    // Distance horizontale minimale entre un point et les waypoints d'un chemin.
    static float CheminMinDist(List<NavGrid.Waypoint> chemin, Vector3 point)
    {
        float min = float.MaxValue;
        foreach (var w in chemin) min = MathF.Min(min, DistHoriz(w.pos, point));
        return min;
    }

    static float DistHoriz(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
