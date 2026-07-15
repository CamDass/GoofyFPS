using System.Numerics;
using Raylib_cs;
using BepuPhysics;
using BepuPhysics.Collidables;

// ========================================================================================
// ENNEMI V2 : LE CHASSEUR
// ----------------------------------------------------------------------------------------
// Fini le zombie omniscient qui fonce en ligne droite. Maintenant :
//   - PERCEPTION : vision 360°, 75m en horizontal, 100m en vertical, bloquée par les murs.
//   - POURSUITE : chemin A* sur la NavGrid, suivi en "string pulling" (on vise le waypoint
//     le plus loin en ligne de vue -> trajectoires douces, plus de zig-zag de case en case).
//   - ERRANCE : sans cible, promenade vers des points aléatoires, pauses de 2 à 4 secondes.
//   - SAUT : franchissement balistique des liens de saut, atterrissage re-vérifié.
//
// FLUIDITÉ (retour de playtest) :
//   - On n'écrit JAMAIS directement dans la vitesse. Chaque état calcule une vitesse VOULUE
//     (desiredVelXZ), puis on rapproche la vitesse réelle de cette cible à accélération
//     bornée (ACCEL). Résultat : décélération douce (fini le "zombie gelé" qui se fige sans
//     taper) ET virages progressifs (fini le mouvement robotique).
//   - Au corps à corps : steering "Arrival" (on ralentit en approchant mais on garde un
//     plancher de vitesse) -> le zombie maintient la pression et recolle le moindre écart.
//   - La vitesse n'est mise à zéro QUE sur la frame exacte où la claque part.
//   - L'orientation visuelle est lissée (rotation à vitesse angulaire bornée), plus de snap.
// ========================================================================================

public class Enemy
{
    // --- Réglages combat ---
    const float PORTEE_ATTAQUE = 1.7f;    // distance horizontale pour mettre une claque
    const float HAUTEUR_ATTAQUE = 3.5f;   // écart vertical max pour attaquer

    // --- Réglages perception ---
    const float DETECTION_HORIZONTALE = 75f;   // rayon de détection à 360°
    const float DETECTION_VERTICALE = 100f;    // il te voit même tout en haut d'un échafaudage

    // --- Réglages déplacement ---
    const float FACTEUR_ERRANCE = 0.45f;  // vitesse de promenade = 45% de la vitesse de chasse
    const float GRAVITE = 10f;            // doit correspondre à la gravité BEPU (0,-10,0)
    const float SAUT_VITESSE_H_MAX = 7f;  // vitesse horizontale max pendant un bond

    // --- Réglages fluidité (nouveaux) ---
    const float ACCEL = 45f;              // accél/décél horizontale (m/s²) : coule les à-coups
    const float RAYON_ARRIVEE = 1.3f;     // sous cette distance, ralentissement progressif
    const float PLANCHER_ARRIVEE = 0.22f; // fraction de vitesse conservée au contact (pression)
    const float VITESSE_ROTATION = 9f;    // rad/s : lissage de l'orientation visuelle
    const int LOOKAHEAD_MAX = 5;          // waypoints scannés en avant (string pulling)
    const float LOOKAHEAD_PORTEE = 12f;   // distance max d'un raccourci de string pulling (m)

    enum Etat { Errance, Poursuite, Saut }

    public float attackCooldown;
    public BodyHandle bodyId;
    public int health;
    public float speed;
    public bool isAlive;
    public Vector3 regard = new Vector3(0, 0, 1); // direction "visuelle" LISSÉE (pour le rendu)

    public Sound attackSound;

    // >0 : force ce pas de temps au lieu de Raylib.GetFrameTime(). Réservé aux tests headless
    // (--aitest) où il n'y a pas de rendu : GetFrameTime() y renvoie un delta réel minuscule
    // et non déterministe. En jeu, reste à 0 (comportement normal).
    public static float dtOverride = 0f;

    static readonly Random rngIA = new Random();

    // --- Mémoire du cerveau ---
    Etat etat = Etat.Errance;
    List<NavGrid.Waypoint> chemin;
    int indexChemin;
    Vector3 butChemin;          // destination du dernier calcul A*
    Vector3 dernierePosConnue;  // là où on a vu (ou entendu) le joueur pour la dernière fois
    bool aUneCible;
    float repathTimer;
    float pauseErrance;
    float sautTimer;
    Vector3 desiredVelXZ;       // vitesse horizontale VOULUE ce frame (posée par l'état actif)

    // --- Anti-coincement (capsule wedged sur un pilier/coin qu'aucun chemin ne peut éviter) ---
    Vector3 posPrec;            // position à la frame précédente (pour détecter l'immobilité forcée)
    bool posPrecInit;
    float stuckTimer;          // temps passé à "vouloir avancer sans y arriver"
    float debloqueTimer;       // pendant ce temps : on contourne en pas-de-côté
    float debloqueSigne = 1f;  // côté du contournement (gauche/droite)

    public Enemy(Vector3 startPos, int hp, float spd)
    {
        health = hp;
        speed = spd;
        isAlive = true;
        attackSound = Raylib.LoadSound("assets\\sounds\\raaaah.mp3");
        Raylib.SetSoundVolume(attackSound, Settings.SFXVolume);

        // Chacun démarre avec un petit décalage de repath : 40 zombies ne recalculent
        // pas tous leur A* dans la même frame.
        repathTimer = (float)rngIA.NextDouble() * 0.25f;
        pauseErrance = (float)rngIA.NextDouble() * 2f;

        Capsule shape = new Capsule(0.5f, 1f);
        TypedIndex shapeIndex = Program.simulation.Shapes.Add(shape);

        BodyInertia inertia = shape.ComputeInertia(1f);
        inertia.InverseInertiaTensor = new BepuUtilities.Symmetric3x3();

        BodyDescription bodyDesc = BodyDescription.CreateDynamic(startPos, inertia, shapeIndex, 0.01f);
        bodyId = Program.simulation.Bodies.Add(bodyDesc);
    }

    // ==========================================
    // LE CERVEAU (machine à états : Errance / Poursuite / Saut)
    // ==========================================
    public void Maj(Vector3 playerPos, ref BodyReference PlayerBody)
    {
        if (!isAlive) return;

        float dt = dtOverride > 0f ? dtOverride : Raylib.GetFrameTime();
        if (dt <= 0f) dt = 1f / 60f; // garde-fou (frame 0 / pause) pour ACCEL*dt
        if (attackCooldown > 0) attackCooldown -= dt;

        BodyReference enemyBody;
        try
        {
            enemyBody = Program.simulation.Bodies.GetBodyReference(bodyId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Enemy.Maj ignored invalid bodyId {bodyId}: {ex.Message}");
            isAlive = false;
            return;
        }

        enemyBody.Awake = true;
        Vector3 maPos = enemyBody.Pose.Position;
        Vector3 posPieds = maPos - new Vector3(0, 1f, 0); // centre de capsule -> sol

        // Déplacement RÉEL depuis la frame précédente (pour détecter un coincement physique :
        // capsule wedgée sur un pilier/coin qu'aucun chemin ne peut totalement éviter).
        float bougeReel = posPrecInit ? MathF.Sqrt((maPos.X - posPrec.X) * (maPos.X - posPrec.X) + (maPos.Z - posPrec.Z) * (maPos.Z - posPrec.Z)) : 1f;
        posPrec = maPos; posPrecInit = true;
        if (debloqueTimer > 0f) debloqueTimer -= dt;

        // Distances "à plat" (on ignore la hauteur Y)
        Vector3 positionEnnemiPlate = maPos; positionEnnemiPlate.Y = 0;
        Vector3 positionJoueurPlate = playerPos; positionJoueurPlate.Y = 0;
        float distanceHorizontale = Vector3.Distance(positionEnnemiPlate, positionJoueurPlate);
        float differenceHauteur = playerPos.Y - maPos.Y;

        // ==========================================
        // 1. PERCEPTION (360°, occlusion par les murs)
        // ==========================================
        bool joueurVisible = PeutVoir(playerPos, maPos, distanceHorizontale);
        if (joueurVisible)
        {
            dernierePosConnue = playerPos;
            aUneCible = true;
            if (etat != Etat.Saut) etat = Etat.Poursuite;
        }

        // ==========================================
        // 2. ATTAQUE (la claque + knockback)
        // ==========================================
        bool attaqueCeFrame = false;
        if (joueurVisible && distanceHorizontale < PORTEE_ATTAQUE
            && MathF.Abs(differenceHauteur) < HAUTEUR_ATTAQUE && attackCooldown <= 0)
        {
            attaqueCeFrame = true;
            Program.PlaySound3D(attackSound, maPos, 15f);
            Program.localPlayer.TakeDamage(10);
            attackCooldown = 0.5f;

            // LE KNOCKBACK (réduit pour éviter les envolées verticales)
            Vector3 pushDir = positionJoueurPlate - positionEnnemiPlate;
            if (pushDir.LengthSquared() > 0.001f) pushDir = Vector3.Normalize(pushDir);
            pushDir.Y = 0.1f;
            PlayerBody.Velocity.Linear += pushDir * 15f;
        }

        // ==========================================
        // 3. MACHINE À ÉTATS -> pose desiredVelXZ (sauf en Saut où la vitesse est balistique)
        // ==========================================
        desiredVelXZ = Vector3.Zero;
        switch (etat)
        {
            case Etat.Saut:
                GererSaut(ref enemyBody, dt);
                break;

            case Etat.Poursuite:
                Poursuivre(ref enemyBody, posPieds, playerPos, joueurVisible, distanceHorizontale, differenceHauteur, dt);
                break;

            case Etat.Errance:
                Errer(ref enemyBody, posPieds, dt);
                break;
        }

        // ==========================================
        // 3bis. ANTI-COINCEMENT : si on est en train de se débloquer, on force un PAS DE CÔTÉ
        // (perpendiculaire à la cible + un peu vers l'avant) pour glisser autour du pilier/coin.
        // ==========================================
        if (debloqueTimer > 0f && etat != Etat.Saut)
        {
            Vector3 baseDir = desiredVelXZ.LengthSquared() > 0.01f ? Vector3.Normalize(desiredVelXZ) : regard;
            Vector3 perp = new Vector3(-baseDir.Z, 0, baseDir.X) * debloqueSigne;
            Vector3 dir = baseDir * 0.35f + perp;
            if (dir.LengthSquared() > 0.0001f) desiredVelXZ = Vector3.Normalize(dir) * speed;
        }

        // ==========================================
        // 4. INTÉGRATION DOUCE DE LA VITESSE (le coeur du correctif fluidité)
        // On ne fixe jamais la vitesse d'un coup : on la rapproche de la vitesse voulue à
        // accélération bornée. Décélération progressive -> plus de gel ; virages progressifs
        // -> plus de mouvement robotique. (En vol, on ne touche à rien : gravité + inertie.)
        // ==========================================
        if (etat != Etat.Saut)
        {
            // La vitesse n'est annulée QUE sur la frame de la claque (bref "plante-toi et tape").
            if (attaqueCeFrame) desiredVelXZ = Vector3.Zero;

            Vector3 velAct = new Vector3(enemyBody.Velocity.Linear.X, 0, enemyBody.Velocity.Linear.Z);
            Vector3 velNouv = VersVitesse(velAct, desiredVelXZ, ACCEL * dt);
            enemyBody.Velocity.Linear.X = velNouv.X;
            enemyBody.Velocity.Linear.Z = velNouv.Z;

            // Détection de coincement : on VEUT avancer franchement mais on ne bouge quasiment
            // pas (capsule bloquée par la géométrie). Au bout de 0.35s -> on déclenche un
            // pas-de-côté et on recalcule le chemin depuis la position dégagée.
            float attendu = desiredVelXZ.Length() * dt;
            if (attendu > 0.02f && bougeReel < attendu * 0.3f) stuckTimer += dt;
            else stuckTimer = 0f;

            if (stuckTimer > 0.35f && debloqueTimer <= 0f)
            {
                debloqueTimer = 0.3f;
                debloqueSigne = rngIA.NextDouble() < 0.5 ? 1f : -1f; // on tente un côté (l'autre au prochain cycle)
                InvaliderChemin();  // repath depuis là où on se sera dégagé
                stuckTimer = 0f;
            }
        }

        // ==========================================
        // 5. ORIENTATION LISSÉE (rotation à vitesse angulaire bornée, plus de snap)
        // ==========================================
        Vector3 dirRegard;
        if (etat == Etat.Saut) { dirRegard = enemyBody.Velocity.Linear; dirRegard.Y = 0; }
        else dirRegard = desiredVelXZ;

        // À l'arrêt face au joueur : on continue de le fixer plutôt que de figer une direction morte.
        if (dirRegard.LengthSquared() < 0.04f && joueurVisible && distanceHorizontale > 0.05f)
            dirRegard = positionJoueurPlate - positionEnnemiPlate;

        regard = TournerVers(regard, dirRegard, VITESSE_ROTATION * dt);
    }

    // ==========================================
    // PERCEPTION : distance + occlusion (pas de cône : vision à 360°)
    // ==========================================
    bool PeutVoir(Vector3 playerPos, Vector3 maPos, float distanceHorizontale)
    {
        if (!Program.localPlayer.IsAlive) return false;
        if (distanceHorizontale > DETECTION_HORIZONTALE) return false;
        if (MathF.Abs(playerPos.Y - maPos.Y) > DETECTION_VERTICALE) return false;

        // Rayon des yeux (haut de la capsule) vers le centre du joueur.
        // TOUS les statics bloquent la vue : map, barils, murs posés en cours de partie.
        Vector3 oeil = maPos + new Vector3(0, 0.7f, 0);
        Vector3 delta = playerPos - oeil;
        float dist = delta.Length();
        if (dist < 0.5f) return true;

        NavRaySensor vue = new NavRaySensor(false);
        Program.simulation.RayCast(oeil, delta / dist, dist - 0.3f, ref vue);
        return !vue.aTouche;
    }

    // ==========================================
    // POURSUITE : A* vers le joueur (ou vers sa dernière position connue)
    // ==========================================
    void Poursuivre(ref BodyReference body, Vector3 posPieds, Vector3 playerPos, bool joueurVisible,
                    float distanceHorizontale, float differenceHauteur, float dt)
    {
        Vector3 cible = joueurVisible ? playerPos : dernierePosConnue;

        // --- INVESTIGATION TERMINÉE ? ---
        // On a atteint la dernière position connue sans revoir le joueur -> retour en errance.
        if (!joueurVisible)
        {
            Vector3 platMoi = body.Pose.Position; platMoi.Y = 0;
            Vector3 platCible = dernierePosConnue; platCible.Y = 0;
            if (Vector3.Distance(platMoi, platCible) < 1.5f)
            {
                aUneCible = false;
                etat = Etat.Errance;
                pauseErrance = 2f + (float)rngIA.NextDouble() * 2f; // pause 2-4s
                chemin = null;
                Freiner();
                return;
            }
        }

        // --- CORPS À CORPS : arrival directe vers le joueur ---
        // Steering "Arrival" : on ralentit en approchant MAIS on garde un plancher de vitesse.
        // Jamais d'arrêt dur -> si le joueur recule d'un cheveu (knockback, micro-pas), le
        // zombie a encore de l'élan pour recoller les 5cm manquants et enchaîner la claque.
        //
        // IMPORTANT : on ne prend ce raccourci QUE s'il existe une ligne droite MARCHABLE
        // jusqu'au joueur. Sinon (trou, ledge, mur entre nous), on laisse la NavGrid gérer :
        // c'est elle qui déclenche le SAUT. Sans ce garde-fou, un zombie face au joueur de
        // l'autre côté d'un petit trou (<4m) resterait planté au bord au lieu de bondir.
        if (joueurVisible && distanceHorizontale < 4f && MathF.Abs(differenceHauteur) < 1.5f
            && NavGrid.CheminLibre(posPieds, cible - new Vector3(0, 0.9f, 0)))
        {
            chemin = null;
            SteeringVers(ref body, cible, speed, arrivee: true, eviterVide: true);
            return;
        }

        // --- REPATH : toutes les 10-15 frames OU si la cible a bougé de plus de 2m ---
        repathTimer -= dt;
        if (repathTimer <= 0f)
        {
            Vector3 platCible = cible; platCible.Y = 0;
            Vector3 platBut = butChemin; platBut.Y = 0;
            bool cibleABouge = chemin == null || Vector3.Distance(platCible, platBut) > 2f;

            if (cibleABouge)
            {
                Vector3 ciblePieds = cible - new Vector3(0, 0.9f, 0);
                chemin = NavGrid.TrouverChemin(posPieds, ciblePieds, out bool complet);
                indexChemin = 0;
                butChemin = cible;

                // Cible injoignable (perchoir, autre îlot) : on suit le chemin d'approche
                // partiel et on espace les recalculs.
                repathTimer = complet
                    ? 0.16f + (float)rngIA.NextDouble() * 0.09f  // ~10-15 frames à 60fps
                    : 0.6f + (float)rngIA.NextDouble() * 0.4f;
            }
            else
            {
                repathTimer = 0.16f + (float)rngIA.NextDouble() * 0.09f;
            }
        }

        // Pas de chemin (zombie hors grille) : steering direct de secours vers la cible.
        if (chemin == null)
        {
            SteeringVers(ref body, cible, speed, arrivee: true, eviterVide: true);
            return;
        }

        SuivreChemin(ref body, posPieds, speed);
    }

    // ==========================================
    // ERRANCE : promenade vers des points aléatoires de la grille, avec pauses
    // ==========================================
    void Errer(ref BodyReference body, Vector3 posPieds, float dt)
    {
        if (pauseErrance > 0f)
        {
            pauseErrance -= dt;
            Freiner();
            return;
        }

        if (chemin == null)
        {
            // On choisit une destination de balade dans un rayon local (10m)
            if (NavGrid.PointErranceAleatoire(posPieds, 10f, out Vector3 destination))
            {
                chemin = NavGrid.TrouverChemin(posPieds, destination, out _);
                indexChemin = 0;
                butChemin = destination;
            }

            if (chemin == null)
            {
                // Pas de grille ou pas de point valide : on attend un peu avant de réessayer
                pauseErrance = 1f;
                Freiner();
                return;
            }
        }

        bool arrive = SuivreChemin(ref body, posPieds, speed * FACTEUR_ERRANCE);
        if (arrive)
        {
            chemin = null;
            pauseErrance = 2f + (float)rngIA.NextDouble() * 2f; // pause 2-4s avant le prochain point
        }
    }

    // ==========================================
    // SUIVI DE CHEMIN avec STRING PULLING (look-ahead en ligne de vue).
    // Au lieu de viser bêtement la case adjacente (mouvement en escalier), on vise le
    // waypoint le plus LOIN qu'on voit en ligne droite marchable -> trajectoire lissée.
    // Renvoie true quand le chemin vient d'être terminé.
    // ==========================================
    bool SuivreChemin(ref BodyReference body, Vector3 posPieds, float vitesse)
    {
        if (chemin == null || chemin.Count == 0) { Freiner(); return false; }

        // --- 1. Progression + saut au node courant ---
        while (indexChemin < chemin.Count)
        {
            NavGrid.Waypoint wp = chemin[indexChemin];
            Vector3 versWp = wp.pos - posPieds; versWp.Y = 0;
            float distWp = versWp.Length();

            // Au bord d'un lien de saut ? On bondit vers le waypoint d'atterrissage.
            if (wp.sautVersSuivant && distWp < 0.55f && indexChemin + 1 < chemin.Count)
            {
                LancerSaut(ref body, posPieds, chemin[indexChemin + 1].pos);
                return false;
            }

            if (distWp < 0.5f) { indexChemin++; continue; }
            break;
        }

        if (indexChemin >= chemin.Count) { chemin = null; Freiner(); return true; }

        // --- 2. STRING PULLING : viser le waypoint le plus loin en ligne de vue directe ---
        // (on ne franchit jamais un lien de saut par un raccourci : l'atterrissage doit rester exact)
        if (!chemin[indexChemin].sautVersSuivant)
        {
            int limite = Math.Min(indexChemin + LOOKAHEAD_MAX, chemin.Count - 1);
            for (int j = limite; j > indexChemin; j--)
            {
                if (Vector3.DistanceSquared(posPieds, chemin[j].pos) > LOOKAHEAD_PORTEE * LOOKAHEAD_PORTEE) continue;

                bool sautDedans = false;
                for (int k = indexChemin; k < j; k++)
                    if (chemin[k].sautVersSuivant) { sautDedans = true; break; }
                if (sautDedans) continue;

                if (NavGrid.CheminLibre(posPieds, chemin[j].pos)) { indexChemin = j; break; }
            }
        }

        // --- 3. Steering vers le point visé (arrival douce seulement sur le dernier noeud) ---
        bool dernierNoeud = indexChemin >= chemin.Count - 1;
        SteeringVers(ref body, chemin[indexChemin].pos, vitesse, arrivee: dernierNoeud, eviterVide: false);
        return false;
    }

    // ==========================================
    // STEERING : calcule la vitesse VOULUE vers une cible (glisse le long des murs
    // dynamiques, évite le vide si demandé, ralentit en "arrival" si demandé).
    // Pose desiredVelXZ (l'intégration douce dans Maj s'occupe de l'accélération).
    // ==========================================
    void SteeringVers(ref BodyReference body, Vector3 cible, float vitesse, bool arrivee, bool eviterVide)
    {
        Vector3 pos = body.Pose.Position;
        Vector3 dir = cible - pos; dir.Y = 0;
        float dist = dir.Length();
        if (dist < 0.05f) { desiredVelXZ = Vector3.Zero; return; }
        dir /= dist;

        // Glissement le long des murs DYNAMIQUES (murs posés, barils réapparus) que la grille
        // ne connaît pas. La marge de clearance de la grille gère déjà les murs statiques.
        WallSensor capteur = new WallSensor(body.CollidableReference);
        Program.simulation.RayCast(pos, dir, 1.2f, ref capteur);
        if (capteur.toucheMur)
        {
            float dot = Vector3.Dot(dir, capteur.normaleDuMur);
            dir = dir - capteur.normaleDuMur * dot + capteur.normaleDuMur * 0.2f;
            if (dir.LengthSquared() > 1e-4f) dir = Vector3.Normalize(dir);
            else { desiredVelXZ = Vector3.Zero; return; }
        }

        // Détection du vide (hors grille / corps à corps) : on ne marche pas dans le trou.
        // (En suivi de grille c'est inutile : la grille garantit déjà le sol.)
        if (eviterVide)
        {
            Vector3 devant = pos + dir * 1.5f; devant.Y += 0.5f;
            LaserSensor sol = new LaserSensor(body.CollidableReference);
            Program.simulation.RayCast(devant, new Vector3(0, -1f, 0), 2.5f, ref sol);
            if (!sol.aTouche) { desiredVelXZ = Vector3.Zero; return; }
        }

        // Arrival : ralentissement progressif près de la cible, avec un plancher de vitesse
        // pour garder la pression (ne jamais tomber à zéro tant qu'on n'est pas au contact).
        float v = vitesse;
        if (arrivee && dist < RAYON_ARRIVEE)
            v = vitesse * MathF.Max(dist / RAYON_ARRIVEE, PLANCHER_ARRIVEE);

        desiredVelXZ = dir * v;
    }

    // ==========================================
    // LE SAUT : impulsion balistique calculée depuis la gravité, avec
    // RE-VÉRIFICATION de l'atterrissage juste avant (jamais de saut dans le vide).
    // ==========================================
    void LancerSaut(ref BodyReference body, Vector3 posPieds, Vector3 atterrissage)
    {
        // Vérification runtime : y a-t-il TOUJOURS un sol à l'arrivée ?
        NavRaySensor verif = new NavRaySensor(true);
        Program.simulation.RayCast(atterrissage + new Vector3(0, 1f, 0), new Vector3(0, -1, 0), 2.5f, ref verif);
        if (!verif.aTouche)
        {
            // Plus de plateforme (bake périmé ?) : chemin invalidé, on recalculera un détour.
            chemin = null;
            repathTimer = 0f;
            Freiner();
            return;
        }

        Vector3 delta = atterrissage - posPieds;
        float dy = delta.Y;
        delta.Y = 0;
        float dH = delta.Length();
        if (dH < 0.1f) { chemin = null; return; }
        Vector3 dirH = delta / dH;

        // Balistique : on monte assez haut pour passer (marge 2.5 m/s), puis on résout
        // le temps de vol t tel que dy = vy*t - g*t²/2, et la vitesse horizontale = dH/t.
        float vy = MathF.Sqrt(2f * GRAVITE * MathF.Max(dy, 0f)) + 2.5f;
        float t = (vy + MathF.Sqrt(vy * vy - 2f * GRAVITE * dy)) / GRAVITE;
        float vH = dH / t;
        if (vH > SAUT_VITESSE_H_MAX)
        {
            // Trop loin pour ce bond-là : on rallonge le temps de vol en sautant plus haut
            t = dH / SAUT_VITESSE_H_MAX;
            vy = dy / t + 0.5f * GRAVITE * t;
            vH = SAUT_VITESSE_H_MAX;
        }

        body.Velocity.Linear = new Vector3(dirH.X * vH, vy, dirH.Z * vH);
        etat = Etat.Saut;
        sautTimer = t + 1.5f; // temps de vol + marge de sécurité
        indexChemin++;        // on vise maintenant le waypoint d'atterrissage
    }

    void GererSaut(ref BodyReference body, float dt)
    {
        sautTimer -= dt;

        bool atterri = false;
        if (body.Velocity.Linear.Y <= 0.1f)
        {
            // En phase descendante : sol à moins de 15cm sous les pieds ?
            NavRaySensor sol = new NavRaySensor(false);
            Program.simulation.RayCast(body.Pose.Position, new Vector3(0, -1, 0), 1.15f, ref sol);
            atterri = sol.aTouche;
        }

        if (atterri || sautTimer <= 0f)
        {
            etat = aUneCible ? Etat.Poursuite : Etat.Errance;
            repathTimer = 0f; // on repart sur un chemin frais
        }
        // Pendant le vol : on ne touche PAS à la vitesse, la gravité fait le travail.
    }

    // desiredVelXZ = 0 : l'intégration douce dans Maj fera décélérer proprement (pas de gel).
    void Freiner() => desiredVelXZ = Vector3.Zero;

    // Rapproche "actuel" de "cible" d'au plus maxDelta (clamp de la variation de vitesse).
    static Vector3 VersVitesse(Vector3 actuel, Vector3 cible, float maxDelta)
    {
        Vector3 d = cible - actuel;
        float l = d.Length();
        if (l <= maxDelta || l < 1e-5f) return cible;
        return actuel + d / l * maxDelta;
    }

    // Fait pivoter la direction "actuel" vers "cible" d'au plus maxRad radians (rotation lissée).
    // Convention alignée sur le rendu : angle = atan2(X, Z).
    static Vector3 TournerVers(Vector3 actuel, Vector3 cible, float maxRad)
    {
        if (cible.LengthSquared() < 1e-6f) return actuel;
        if (actuel.LengthSquared() < 1e-6f) return Vector3.Normalize(cible);

        float angleActuel = MathF.Atan2(actuel.X, actuel.Z);
        float angleCible = MathF.Atan2(cible.X, cible.Z);
        float d = angleCible - angleActuel;
        while (d > MathF.PI) d -= MathF.Tau;
        while (d < -MathF.PI) d += MathF.Tau;
        d = Math.Clamp(d, -maxRad, maxRad);
        float a = angleActuel + d;
        return new Vector3(MathF.Sin(a), 0, MathF.Cos(a));
    }

    // ==========================================
    // LA GESTION DES DÉGÂTS
    // ==========================================
    // Sans source : dégâts "environnement" (chute dans le vide...) -> pas d'aggro.
    public void TakeDamage(int damage) { TakeDamageInterne(damage, null); }

    // Avec source : dégâts d'ARME -> aggro immédiat vers les coordonnées du tireur,
    // même si le coup vient de derrière ou de hors de vue (spécification "Damage Aggro").
    public void TakeDamage(int damage, Vector3 sourceAttaque) { TakeDamageInterne(damage, sourceAttaque); }

    void TakeDamageInterne(int damage, Vector3? sourceAttaque)
    {
        if (!isAlive) return;

        Vector3 headPosition;
        try
        {
            headPosition = GetPosition() + new Vector3(0, 1.8f, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Enemy.TakeDamage ignored invalid bodyId {bodyId}: {ex.Message}");
            isAlive = false;
            return;
        }

        Random rand = new Random();
        float randomX = (float)(rand.NextDouble() * 0.6f - 0.3f);
        float randomZ = (float)(rand.NextDouble() * 0.6f - 0.3f);
        headPosition.X += randomX;
        headPosition.Z += randomZ;

        // On ajoute le texte à notre liste d'affichage
        Program.activeDamageTexts.Add(new Program.DamageText(headPosition, damage));

        health -= damage;
        if (health <= 0)
        {
            isAlive = false;

            // 1. LE JOUEUR A TUÉ L'ENNEMI : Son de caisse enregistreuse !
            Program.PlaySoundWithPriority(Program.killSound, Program.SoundPriority.High);

            // Le cri de mort vient du zombie, on l'entend jusqu'à 30 mètres !
            Program.PlaySound3D(Program.deathSounds[Program.deathSoundIndex], GetPosition(), 30f);
            Program.deathSoundIndex = (Program.deathSoundIndex + 1) % Program.deathSounds.Length;
            return;
        }

        // --- DAMAGE AGGRO : blessé = furieux, il fonce vers l'origine du tir ---
        if (sourceAttaque.HasValue)
        {
            dernierePosConnue = sourceAttaque.Value;
            aUneCible = true;
            if (etat == Etat.Errance)
            {
                etat = Etat.Poursuite;
                chemin = null;
                repathTimer = 0f; // recalcul immédiat du chemin
                pauseErrance = 0f;
            }
        }
    }

    // ==========================================
    // MUR POSÉ DYNAMIQUEMENT : si mon chemin (ou ma position) touche une des cases
    // fraîchement bloquées, je jette mon chemin et je recalcule immédiatement un contournement.
    // ==========================================
    public void NotifierZoneBloquee(List<Vector3> centresBloques)
    {
        if (!isAlive || centresBloques == null || centresBloques.Count == 0) return;

        // 1. Mur posé juste à côté de moi (<6m) : je réévalue par précaution (je pourrais être
        //    en steering direct "corps à corps" sans chemin, et foncer droit dans le mur).
        Vector3 me = GetPosition();
        foreach (Vector3 c in centresBloques)
        {
            float dx = me.X - c.X, dz = me.Z - c.Z;
            if (dx * dx + dz * dz < 36f) { InvaliderChemin(); return; }
        }

        // 2. Un de mes futurs waypoints traverse une case bloquée : mon chemin est périmé.
        if (chemin != null)
        {
            for (int k = indexChemin; k < chemin.Count; k++)
            {
                Vector3 w = chemin[k].pos;
                foreach (Vector3 c in centresBloques)
                {
                    float dx = w.X - c.X, dz = w.Z - c.Z;
                    if (dx * dx + dz * dz < 1.0f && MathF.Abs(w.Y - c.Y) < 1.2f) { InvaliderChemin(); return; }
                }
            }
        }
    }

    void InvaliderChemin() { chemin = null; repathTimer = 0f; }

    // Pour savoir où dessiner le modèle 3D
    public Vector3 GetPosition()
    {
        if (!isAlive) return Vector3.Zero;
        try
        {
            return Program.simulation.Bodies.GetBodyReference(bodyId).Pose.Position;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Enemy.GetPosition invalid bodyId {bodyId}: {ex.Message}");
            isAlive = false;
            return Vector3.Zero;
        }
    }

    public int GetLife()
    {
        if (!isAlive) return 0;
        return health;
    }
}
