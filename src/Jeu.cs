using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;
using System.Linq;

// Moteur Physique
using BepuPhysics;
using BepuPhysics.Collidables;

partial class Program
{   
    // ========================================================
    // VARIABLES DES ARMES (ILIAN)
    // ========================================================
    static Weapon sniperrifle = new Weapon("Sniper", 100, 1000, 1.0f, 5, 3, sniper, snipershot, 0f);
    static Weapon karambitknife = new Weapon("Karambit", 75, 4, 0.4f, 1, 0, karambit, karambitshot, 0f, false);
    static Weapon bazookaWeapon = new Weapon("Bazooka", 80, 1000, 3.0f, 1, 4, bazooka, bazookashot, 15.0f);
    static Weapon shotgunWeapon = new Weapon("Shotgun", 75, 10, 1f, 5, 2, shotgun, shotgunshot, 15.0f);
    static Weapon pistolWeapon = new Weapon("Pistol", 8, 1000, 0.08f, 80, 2, pistol, pistolshot, 0f);
    static Weapon revolverWeapon = new Weapon("Revolver", 40, 1000, 0.8f, 6, 2, revolver, revolvershot, 15.0f);
    static Weapon swordWeapon = new Weapon("Sword", 10, 1000, 0.15f, 30, 4, sword, swordslash, 3f);

    static List<Weapon> weapons = new List<Weapon> { sniperrifle, karambitknife, bazookaWeapon, shotgunWeapon, pistolWeapon, revolverWeapon, swordWeapon };
    static Weapon currentWeapon = karambitknife;
    static float laserTimer = 0.0f;
    static Vector3 laserStart = new Vector3();
    static Vector3 laserEnd = new Vector3();
    static float recoilAngle = 0.0f;

    static Random random = new Random();
    static float barrelRespawnSeconds = 30.0f;
    static float barrelScale = 1.0f;
    public static int initialBarrelCount = 10;

    static bool showweapon = true;

    // ========================================================
    // ÉTAT DES MUTATEURS QUI ONT BESOIN DE MÉMOIRE D'UNE FRAME À L'AUTRE
    // ========================================================
    // "Dégâts de chute" : on compare la vitesse verticale de la frame précédente.
    const float ChuteSansDegat = 25f;      // en dessous de 25 m/s, atterrissage gratuit
    static bool etaitAuSol = true;
    static float vitesseChutePrecedente = 0f;
    // "Régénération" : on accumule les fractions de PV (2 PV/s à 144 fps = 0.014 PV/frame).
    static float regenAccumulateur = 0f;

    /// <summary>Durée de recharge du mur en frames (mutateur "Recharge du mur").</summary>
    public static int WallChronoMax => Math.Max(1, MatchRules.MurDelaiSec * FPS);

    public class BarrelSpot
    {
        public Vector3 position;
        public bool hasBarrel;
        public float respawnTimer;
        public bool respawnPending;


        public StaticHandle handlePhysique; 
        public bool estSolide;



        public BarrelSpot(Vector3 position, bool hasBarrel = false)
        {
            this.position = position;
            this.hasBarrel = hasBarrel;
            respawnTimer = 0f;
            respawnPending = false;
        }
    }






    // ======== bdd Barils ========
    static Vector3[][] mapBarrelCoords = new Vector3[][]
    {
        // Map 0 : Tutoriel
        [
            new Vector3(-45.5f, 12.35f, -22.5f),   // Plateforme de départ Ouest (West_Catwalk_S)
            new Vector3(-35.16f, 4.55f, -9.23f),   // Passerelle Ouest .001
            new Vector3(-47.74f, 3.95f, -4.03f),   // Passerelle Ouest .002[cite: 1]
            new Vector3(-30.85f, 2.95f, 47.76f),   // Passerelle haute tournante Ouest .003[cite: 2]
            new Vector3(-22.51f, 10.40f, 17.70f),  // Petite plateforme suspendue .014
        
            new Vector3(15.2f, 2.65f, -25.0f),     // Bloc Cyan 1 (décalé vers le centre du couloir)
            new Vector3(11.0f, 2.75f, -20.25f),    // Bloc Cyan 2 (décalé vers l'espace vide)
            new Vector3(6.79f, 3.25f, -26.0f),     // Bloc Cyan 3 (monté et avancé pour ne pas bugger)
            new Vector3(8.5f, 4.25f, -15.25f),     // Grand bloc Cyan 4 (centré sur la surface du bloc)
            new Vector3(17.29f, 2.05f, -11.5f),    // Bloc au sol Cyan 5 (rehaussé et éloigné du mur)
            new Vector3(4.73f, 2.70f, -7.0f),      // Bloc Cyan 8 (avancé vers la zone de jeu)

            new Vector3(9.71f, 5.15f, -42.5f),     // Structure Cyan massive 9 (posé sur le rebord)
            new Vector3(17.43f, 7.55f, -43.73f),   // Au sommet de l'arche en bloc Cyan 11

            // --- ARÈNE ET RAMPES (EST / NORD) ---
            new Vector3(-35.0f, 2.55f, 2.15f),     // Juste au sommet du tremplin de saut (Arena_JumpRamp)
            new Vector3(-44.5f, 7.65f, -39.44f),   // Haut de la rampe de descente (Descent_Ramp)
            new Vector3(44.76f, 4.85f, -10.42f),   // Palier d'atterrissage Est (Ramp_Landing.001)
            new Vector3(44.76f, 4.85f, 0.11f)      // Deuxième palier d'atterrissage (Ramp_Landing)
        
        ],


        // Map 1 : La Ville
        [ 
            new Vector3(-57f, -0.55f, 31.92f), 
            new Vector3(-49f, 42.0f, 101f), 
            new Vector3(-53f, 7.5f, 130f),
            new Vector3(-22f, -0.55f, 128f), 
            new Vector3(3f,-0.55f, 103f), 
            new Vector3(3f, 4.5f, 54f),
            new Vector3(-15f, -0.55f, 22f), 
            new Vector3(-76.5f, 8f, 10.5f), 
            new Vector3(-81.5f, 5f, 42f),
            new Vector3(-110f, -0.55f, 50.5f), 
            new Vector3(-72f, 9.0f, 91f), 
            new Vector3(-76f, -0.55f, 114f),
            new Vector3(-46f, -0.55f, 52f), 
            new Vector3(-35f, 3.7f, 87f), 
            new Vector3(-18f, -0.55f, 71f)
        ],


        // Map 2 : Sandbox
        [
        new Vector3(24f,1.02f,-18f), new Vector3(28f,1.02f,-18f), new Vector3(26f,1.02f,-22f), // grappe centrale
        new Vector3(70f,1.02f,-20f), new Vector3(74f,1.02f,-20f),                               // grappe est
        new Vector3(60f,1.02f, 47f), new Vector3( 0f,1.02f, 47f),                               // pièges couloir wall-run
        new Vector3(-38f,1.02f,40f),                                                            // base ascension ouest
        new Vector3(-6f,21f,-32f), new Vector3(20f,21f,-32f),                                   // sur hub / PE (récompense hauteur)
        new Vector3(90f,1.02f,-32f),                                                            // bas de rampe
        new Vector3(-6f,23f,-78f),                                                              // plateforme nord
        new Vector3(45f,1.02f,-40f), new Vector3(10f,1.02f,20f), new Vector3(88f,21.7f,-100f)
        ],


        // Map 3 : Blocs — barils au sol + quelques-uns sur les plateformes (dérivé de blockmap.glb)
        [
        new Vector3( 90.2f, 1.02f, -36.7f), new Vector3(-39.4f,1.02f,  21.3f),
        new Vector3( 38.6f, 1.02f,  21.3f), new Vector3(-1.4f, 1.02f, -36.7f),
        new Vector3( 88.6f, 1.02f,  15.3f), new Vector3(44.6f, 1.02f, -24.7f),
        new Vector3( -1.4f, 1.02f,  13.3f), new Vector3(-39.4f,1.02f, -32.7f),
        new Vector3( 72.6f, 1.02f, -10.7f), new Vector3(-19.4f,1.02f, -10.7f),
        new Vector3( 18.6f, 1.02f,  -8.7f), new Vector3(62.6f, 1.02f,  15.3f),
        new Vector3( 66.8f, 9.31f,  -6.7f), new Vector3(-7.4f, 9.31f, -12.2f),  // hautes plateformes
        new Vector3( 62.7f,11.34f,  -4.6f), new Vector3(-3.3f,11.34f, -14.4f),
        new Vector3( 40.7f, 8.60f,   4.9f), new Vector3(18.7f, 8.60f, -23.9f)
        ]
    };

    static List<BarrelSpot> barrelSpots = new List<BarrelSpot>();







    // ======== Spawns Ennemis bdd ========
    static Vector3[][] mapEnemySpawns = new Vector3[][]
    {
        // Map 0 : Tutoriel
        [
            new Vector3(10f, 2f, 10f), 
            new Vector3(-10f, 2f, -10f)
        ],


        // Map 1 : La Ville
        [ 
            new Vector3(2f, 2f, 2f), 
            new Vector3(-27f, 2f, 64f), 
            new Vector3(-100f, 2f, 99f), 
            new Vector3(-104f, 2f, 149f), 
            new Vector3(-28f, 2f, 127f), 
            new Vector3(4f, 2f, 83f),
            new Vector3(-80f, 2f, 27f), 
            new Vector3(-94f, 2f, 13f)
        ],


        // Map 2 : Sandbox
        [
        new Vector3(-44f,0.12f, 80f), new Vector3(96f,0.12f,  80f),
        new Vector3( 96f,0.12f,-145f), new Vector3(-44f,0.12f,-145f),
        new Vector3( 28f,0.12f,-146f), new Vector3(-44f,0.12f, -30f),
        new Vector3( 96f,0.12f, -60f), new Vector3(28f,0.12f,  83f)
        ],


        // Map 3 : Blocs — à l'écart des spawns joueurs (dérivé de blockmap.glb)
        // CORRECTION (navtest) : (-39.4, 21.4) était HORS de la map (le sol s'arrête à x≈-34),
        // les zombies y tombaient dans le vide. Déplacé sur le sol vérifié.
        [
        new Vector3(-30f, 1f,  20f), new Vector3(52.6f, 1f, -10.7f),
        new Vector3( 56.6f, 1f, -36.7f), new Vector3( 6.6f, 1f, -12.7f),
        new Vector3( 78.6f, 1f, -12.7f), new Vector3(-13.4f,1f,  19.3f),
        new Vector3( 32.6f, 1f,   1.3f), new Vector3(66.6f, 1f,  11.3f)
        ]
    };

    //======== on load tout ========
    public static void LoadMapSpawns(int mapIndex)
    {
        // 1. Charger les Spawns du Joueur
        Program.listeSpawns.Clear();
        foreach (Vector3 pos in mapPlayerSpawns[mapIndex])
        {
            Program.listeSpawns.Add(pos);
        }

        // 2. Charger les Barils
        barrelSpots.Clear();
        foreach (Vector3 pos in mapBarrelCoords[mapIndex])
        {
            barrelSpots.Add(new BarrelSpot(pos, false));
        }

        // 3. Charger les Spawns Ennemis
        enemySpawnPoints.Clear();
        foreach (Vector3 pos in mapEnemySpawns[mapIndex])
        {
            enemySpawnPoints.Add(pos);
        }
    }

    





    // ========================================================
    // L'ARME DE DÉPART + LA REMISE À ZÉRO DES STATS
    // ========================================================
    // currentWeapon, killCount, survivalTime... sont des statiques : elles survivent
    // à la partie. Sans ces deux fonctions, la partie suivante reprendrait le score
    // et l'arme tirée au hasard par le dernier baril de la précédente.

    // Toute partie (solo, tuto, réseau) démarre au karambit — sauf si le mutateur
    // "Armes autorisées" l'a décoché : on prend alors la première arme cochée.
    public static void EquiperArmeDeDepart()
    {
        currentWeapon = ArmeAutoriseeOuRepli(karambitknife);
    }

    /// <summary>Renvoie l'arme voulue si elle est autorisée, sinon la 1re arme cochée.</summary>
    static Weapon ArmeAutoriseeOuRepli(Weapon voulue)
    {
        if (MatchRules.ArmeAutorisee(weapons.IndexOf(voulue))) return voulue;
        for (int i = 0; i < weapons.Count; i++)
            if (MatchRules.ArmeAutorisee(i)) return weapons[i];
        return voulue; // ne devrait jamais arriver : MatchRules garde toujours une arme cochée
    }

    /// <summary>
    /// Recale les armes sur les mutateurs courants : chargeurs bornés à la nouvelle taille,
    /// rechargement en cours annulé, arme en main remplacée si elle vient d'être interdite.
    /// Appelé par MatchRules.Appliquer() (donc aussi quand l'hôte change une règle en direct).
    /// </summary>
    public static void RecalibrerArmes()
    {
        foreach (Weapon arme in weapons)
        {
            if (arme.ammo > arme.MaxAmmoEffectif) arme.ammo = arme.MaxAmmoEffectif;
            if (!arme.BesoinDeRecharger) arme.isReloading = false;
        }
        currentWeapon = ArmeAutoriseeOuRepli(currentWeapon);
    }

    // Appelée à chaque retour au menu principal.
    public static void ReinitialiserStatsPartie()
    {
        killCount = 0;
        deathCount = 0;
        survivalTime = 0f;
        localPlayer.Respawn();      // vie au maximum, joueur vivant
        EquiperArmeDeDepart();

        // Les Weapon sont des objets partagés : leurs munitions et leur rechargement
        // en cours survivent aussi à la partie.
        foreach (Weapon arme in weapons)
        {
            arme.ammo = arme.MaxAmmoEffectif;
            arme.isReloading = false;
        }
    }

    public static void SwitchWeaponFromBarrel(bool excludeBazooka = false)
    {
        // Le baril ne peut sortir que des armes cochées dans le mutateur "Armes autorisées".
        List<Weapon> allowedWeapons = weapons
            .Where(w => MatchRules.ArmeAutorisee(weapons.IndexOf(w)))
            .Where(w => !excludeBazooka || !string.Equals(w.name, "Bazooka", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (allowedWeapons.Count <= 1) return;

        Weapon newWeapon = currentWeapon;
        while (newWeapon == currentWeapon)
        {
            newWeapon = allowedWeapons[random.Next(allowedWeapons.Count)];
        }
        currentWeapon = newWeapon;
        
        // Jouer le son de changement d'arme
        Program.PlaySoundWithPriority(Program.weaponSwitchSound, Program.SoundPriority.Medium);
    }
static void InitBarrels()
    {
        List<int> indices = Enumerable.Range(0, barrelSpots.Count).OrderBy(i => random.Next()).Take(initialBarrelCount).ToList();
        for (int i = 0; i < barrelSpots.Count; i++)
        {
            // LA CORRECTION EST ICI : On force la variable à 'false' pour oublier l'ancienne partie
            barrelSpots[i].estSolide = false; 

            barrelSpots[i].hasBarrel = indices.Contains(i);
            barrelSpots[i].respawnPending = false;
            barrelSpots[i].respawnTimer = 0f;

            // Ajout physique initial tout neuf !
            if (barrelSpots[i].hasBarrel)
            {
                barrelSpots[i].handlePhysique = simulation.Statics.Add(new StaticDescription(barrelSpots[i].position + Program.barrelPhysicsCenterOffset * barrelScale, formeBarilIndex));
                barrelSpots[i].estSolide = true;
            }
        }
    }

    static void CheckBarrelRespawns(float deltaTime)
    {
        for (int i = 0; i < barrelSpots.Count; i++)
        {
            BarrelSpot spot = barrelSpots[i];
            if (!spot.hasBarrel && spot.respawnPending)
            {
                spot.respawnTimer -= deltaTime;
                if (spot.respawnTimer <= 0f)
                {
                    spot.respawnPending = false;
                    spot.respawnTimer = 0f;

                    List<int> freeSlots = barrelSpots.Select((b, index) => new { b, index }).Where(x => !x.b.hasBarrel && !x.b.respawnPending).Select(x => x.index).ToList();

                    if (freeSlots.Count > 0)
                    {
                        int targetIndex = freeSlots[random.Next(freeSlots.Count)];
                        barrelSpots[targetIndex].hasBarrel = true;
                        
                        // Le baril réapparaît, on lui redonne un mur physique !
                        barrelSpots[targetIndex].handlePhysique = simulation.Statics.Add(new StaticDescription(barrelSpots[targetIndex].position + Program.barrelPhysicsCenterOffset * barrelScale, formeBarilIndex));
                        barrelSpots[targetIndex].estSolide = true;
                    }
                }
                barrelSpots[i] = spot;
            }
        }
    }

    public static void OnBarrelHit(int index)
    {
        localPlayer.Heal(25);
        if (index < 0 || index >= barrelSpots.Count) return;
        Vector3 barrelPos = barrelSpots[index].position;
        
        barrelSpots[index].hasBarrel = false;
        barrelSpots[index].respawnPending = true;
        barrelSpots[index].respawnTimer = barrelRespawnSeconds;
        
        // Le baril explose, on supprime son mur physique !
        if (barrelSpots[index].estSolide)
        {
            simulation.Statics.Remove(barrelSpots[index].handlePhysique);
            barrelSpots[index].estSolide = false;
        }

        // Déclenche l'effet "Bouchon d'oreille" pendant 1.5 secondes !
        Program.duckingTimer = 1.5f;

        SwitchWeaponFromBarrel();
        Program.PlaySound3D(explosion, barrelPos, 120f);
        activeExplosions.Add(new ExplosionEffect(barrelPos, 0.5f, barrelScale * 0.8f, barrelScale * 3.5f));
    }

    public static void BreakBarrelAt(int index, bool playSound = false, bool spawnEffect = false)
    {
        if (index < 0 || index >= barrelSpots.Count) return;
        if (!barrelSpots[index].hasBarrel) return;

        barrelSpots[index].hasBarrel = false;
        barrelSpots[index].respawnPending = true;
        barrelSpots[index].respawnTimer = barrelRespawnSeconds;

        if (barrelSpots[index].estSolide)
        {
            simulation.Statics.Remove(barrelSpots[index].handlePhysique);
            barrelSpots[index].estSolide = false;
        }

        if (playSound)
        {
            Program.PlaySoundWithPriority(explosion, Program.SoundPriority.High);
        }

        if (spawnEffect)
        {
            activeExplosions.Add(new ExplosionEffect(barrelSpots[index].position, 0.5f, barrelScale * 0.8f, barrelScale * 3.5f));
        }
    }

    public static bool BreakBarrelsInRadius(Vector3 center, float radius)
    {
        bool anyBroken = false;
        for (int i = 0; i < barrelSpots.Count; i++)
        {
            if (!barrelSpots[i].hasBarrel) continue;
            float dist = Vector3.Distance(center, barrelSpots[i].position);
            if (dist <= radius)
            {
                BreakBarrelAt(i, true, true);
                anyBroken = true;
            }
        }
        return anyBroken;
    }

    public static void SpawnExplosionEffect(Vector3 position)
    {
        float effectDuration = 0.5f;
        float startSize = 0.5f;
        float maxSize = 6f;
        activeExplosions.Add(new ExplosionEffect(position, effectDuration, startSize, maxSize));
    }

    public class ExplosionEffect
    {
        public Vector3 position;
        public float timer;
        public float duration;
        public float initialSize;
        public float maxSize;

        public ExplosionEffect(Vector3 pos, float dur = 0.5f, float initSize = 0.5f, float maxSz = 7f)
        {
            position = pos;
            timer = dur;
            duration = dur;
            initialSize = initSize;
            maxSize = maxSz;
        }

        public float GetSize()
        {
            float progress = 1f - (timer / duration);
            float easedProgress = 1f - (1f - progress) * (1f - progress);
            return initialSize + (maxSize - initialSize) * easedProgress;
        }

        public float GetAlpha()
        {
            return (timer / duration) * 204f;
        }
    }


    static List<ExplosionEffect> activeExplosions = new List<ExplosionEffect>();

    public static bool IsPointOnLaser(Vector3 point, Vector3 laserStart, Vector3 direction, float range)
    {
        Vector3 toPoint = point - laserStart;
        float distance = toPoint.Length();
        if (distance > range) return false;
        Vector3 toPointNorm = Vector3.Normalize(toPoint);
        float dot = Vector3.Dot(direction, toPointNorm);
        return dot > 0.995f;
    }




    // =============== enemis ===============
    public static void InitEnnemis()
    {
        // 1. On nettoie proprement les anciens ennemis physiques
        foreach (Enemy enemy in enemiesList)
        {
            if (enemy.isAlive) simulation.Bodies.Remove(enemy.bodyId);
        }
        enemiesList.Clear();

        // NOUVEAU : On ne définit plus enemySpawnPoints ici, c'est LoadMapSpawns qui s'en charge !

        // 2. On réinitialise les chronos
        survivalTime = 0f;
        enemySpawnTimer = 10;

        // EN LIGNE (LAN) : pas de zombies (PvP). En solo : uniquement si l'interrupteur ZOMBIES est activé.
        // (Les ennemis ne sont pas synchronisés entre les machines, chacun verrait des zombies différents)
        if (isOnline || !zombiesActifs)
        {
            NavGrid.Vider(); // pas de zombies = pas besoin de grille de navigation
            return;
        }

        // 3. LE BAKE DE LA NAVGRID : la map physique vient d'être chargée (ExtraireTrianglesMap),
        // on cuit la grille de navigation A* dessus. Rebake automatique à chaque changement de map.
        NavGrid.Bake(simulation, Raylib.GetModelBoundingBox(mapModel), mapScale, mapPosition);

        // 4. On fait apparaître 2 ennemis de base (aux stats des mutateurs)
        if (enemySpawnPoints.Count >= 2)
        {
            enemiesList.Add(CreerZombie(enemySpawnPoints[0]));
            enemiesList.Add(CreerZombie(enemySpawnPoints[1]));
        }
    }

    /// <summary>Un zombie aux stats des mutateurs "Vie des zombies" / "Vitesse des zombies".</summary>
    static Enemy CreerZombie(Vector3 pointDeSpawn)
    {
        Enemy z = new Enemy(SpawnZombieSur(pointDeSpawn), MatchRules.ZombieVie, 4.0f * MatchRules.ZombieVitesseMul);
        // Le tirage "explosif" se fait UNE fois, à la naissance : un zombie ne change
        // pas de nature en cours de route même si l'hôte bouge le curseur.
        z.explosif = MatchRules.ZombieExplosifPct > 0f
                     && random.NextDouble() * 100.0 < MatchRules.ZombieExplosifPct;
        return z;
    }

    /// <summary>
    /// Le souffle d'un zombie explosif (mutateur "Zombies explosifs") : mêmes rayon et
    /// effets qu'un baril. Touche les autres zombies, le joueur et les barils alentour,
    /// ce qui permet les réactions en chaîne.
    /// </summary>
    static void FaireExploserZombie(Vector3 position, Vector3 posJoueur)
    {
        float rayon = Weapon.RayonExplosion;
        if (rayon <= 0f) return;
        int degats = Math.Max(1, (int)MathF.Round(40f * MatchRules.DegatsMul));

        foreach (Enemy autre in enemiesList)
        {
            if (!autre.isAlive) continue;
            Vector3 centre = autre.GetPosition(); // centre du corps = centre de la hitbox
            if (Vector3.Distance(position, centre) <= rayon) autre.TakeDamage(degats, position);
        }

        if (localPlayer.IsAlive && Vector3.Distance(position, posJoueur) <= rayon)
            localPlayer.TakeDamage(degats);

        BreakBarrelsInRadius(position, rayon);
        SpawnExplosionEffect(position);
        PlaySound3D(explosion, position, 120f);
        Program.duckingTimer = 1.5f;
    }

    // Recale un point de spawn zombie sur la grille de navigation : le corps (capsule de 2m,
    // centre à 1m des pieds) est posé PILE sur le sol marchable le plus proche. Si le point
    // est complètement hors grille, on le garde tel quel (l'IA de secours prendra le relais).
    static Vector3 SpawnZombieSur(Vector3 brut)
    {
        if (NavGrid.SnapAuSol(brut, out Vector3 sol))
            return sol + new Vector3(0, 1.05f, 0);
        Console.WriteLine($"[NAV] Spawn zombie {brut} hors grille : point conservé tel quel");
        return brut;
    }









    static bool debugInfo = false;

    // ==========================================
    // OVERLAY TUTORIEL
    // Affiche une mécanique (titre) + la touche à utiliser. Servira pendant le tuto,
    // déclenché selon la position du joueur. TEST : F2 (debug) puis O.
    // ==========================================
    public static string overlayTitre = "";
    public static string overlayTouche = "";
    public static string overlayDesc = "";
    public static float overlayTimer = 0f;
    public static float overlayDuree = 4f;

    /// <summary>Affiche un overlay tuto : titre de la mécanique, explication, touche.</summary>
    public static void DeclencherOverlayTuto(string titre, string touche, string desc = "", float duree = 4f)
    {
        overlayTitre = titre;
        overlayTouche = touche;
        overlayDesc = desc;
        overlayDuree = duree;
        overlayTimer = duree;
        PlaySoundWithPriority(select, SoundPriority.Low);
    }

    // ==========================================
    // ZONES DU TUTORIEL
    // La map est linéaire le long de -Z : chaque zone se déclenche UNE fois quand le
    // joueur passe sous son seuil Z. Ordonnées du plus proche au plus lointain.
    // ==========================================
    public class ZoneTuto
    {
        public float Z;             // seuil de déclenchement (le joueur avance vers -Z)
        public string Titre, Touche, Desc;
        public bool Fait;
        public ZoneTuto(float z, string titre, string touche, string desc)
        { Z = z; Titre = titre; Touche = touche; Desc = desc; }
    }
    public static List<ZoneTuto> zonesTuto = new List<ZoneTuto>();
    public static bool modeTuto = false;

    /// <summary>Construit les paliers d'explication du tuto (coordonnées de maptuto.glb).</summary>
    public static void PreparerZonesTuto()
    {
        zonesTuto = new List<ZoneTuto>
        {
            new ZoneTuto(  -6f, "SAUT",             "ESPACE",   "Franchis le trou devant toi."),
            new ZoneTuto( -17f, "DOUBLE SAUT",      "ESPACE x2","Appuie une 2e fois en l'air : tu rebondis plus haut."),
            new ZoneTuto( -36f, "DASH",             "CTRL",     "Fonce d'un coup. Enchaine dash + saut pour franchir les grands vides."),
            new ZoneTuto( -58f, "S'ACCROUPIR",      "C",        "Reste accroupi pour passer sous les obstacles bas."),
            new ZoneTuto( -63f, "SAUT AU MUR",      "ESPACE",   "En l'air, colle-toi a un mur et saute : tu rebondis dessus."),
            new ZoneTuto( -76f, "GLISSADE",         "MAJ + C",  "Sprinte puis accroupis-toi : tu glisses en gardant ta vitesse."),
            new ZoneTuto(-120f, "DE MUR EN MUR",    "ESPACE",   "Rebondis d'un mur a l'autre : chaque saut relance ta vitesse."),
            new ZoneTuto(-215f, "TESTE TES ARMES",  "CLIC G",   "Tire sur les zombies. Casse un tonneau pour changer d'arme."),
            new ZoneTuto(-232f, "GRIMPE LA BOITE",  "ESPACE",   "Enchaine sauts et sauts au mur jusqu'au sommet."),
            new ZoneTuto(-466f, "GG !",             "",         "Tu maitrises toutes les mecaniques. Bonne chance !"),
        };
        foreach (ZoneTuto z in zonesTuto) z.Fait = false;
    }

    /// <summary>Déclenche l'overlay de la zone atteinte (appelé chaque frame pendant le tuto).</summary>
    static void MajZonesTuto(Vector3 posJoueur)
    {
        if (!modeTuto) return;
        ZoneTuto aAfficher = null;
        foreach (ZoneTuto z in zonesTuto)
        {
            // On marque TOUTES les zones franchies, mais on n'affiche que la plus avancée
            // (evite un defilement d'overlays si le joueur saute plusieurs paliers d'un coup)
            if (!z.Fait && posJoueur.Z <= z.Z) { z.Fait = true; aAfficher = z; }
        }
        if (aAfficher != null)
            DeclencherOverlayTuto(aAfficher.Titre, aAfficher.Touche, aAfficher.Desc, 6f);
    }

    static void DessinerOverlayTuto(float deltaTime)
    {
        if (overlayTimer <= 0f) return;
        overlayTimer -= deltaTime;

        // Fondu : apparition rapide (10 % du temps), disparition douce (25 %)
        float t = overlayTimer / overlayDuree;
        float alpha = 1f;
        if (t > 0.9f) alpha = (1f - t) / 0.1f;
        else if (t < 0.25f) alpha = t / 0.25f;
        alpha = Math.Clamp(alpha, 0f, 1f);
        int a = (int)(alpha * 255);

        int cx = LargeurFenetre / 2;
        bool avecTouche = overlayTouche.Length > 0;
        int panW = 720, panH = avecTouche ? 184 : 116;
        int panX = cx - panW / 2, panY = 110;

        // Panneau sombre + liseré orange
        Raylib.DrawRectangle(panX, panY, panW, panH, new Color(12, 12, 18, (int)(alpha * 215)));
        Raylib.DrawRectangleLinesEx(new Rectangle(panX, panY, panW, panH), 3, new Color(255, 170, 50, a));

        // Titre de la mécanique (avec ombre pour la lisibilité)
        int tw = Raylib.MeasureText(overlayTitre, 40);
        Raylib.DrawText(overlayTitre, cx - tw / 2 + 2, panY + 20, 40, new Color(0, 0, 0, a));
        Raylib.DrawText(overlayTitre, cx - tw / 2, panY + 18, 40, new Color(255, 200, 90, a));

        // Explication de la technique
        if (overlayDesc.Length > 0)
        {
            int dw = Raylib.MeasureText(overlayDesc, 20);
            Raylib.DrawText(overlayDesc, cx - dw / 2, panY + 66, 20, new Color(215, 215, 220, a));
        }

        // La touche, façon "keycap" (omise pour les messages sans touche, ex. le GG final)
        if (avecTouche)
        {
            int kw = Raylib.MeasureText(overlayTouche, 30);
            int capW = Math.Max(kw + 40, 70), capH = 52;
            int capX = cx - capW / 2, capY = panY + 110;
            Raylib.DrawRectangle(capX, capY, capW, capH, new Color(235, 235, 235, a));
            Raylib.DrawRectangleLinesEx(new Rectangle(capX, capY, capW, capH), 3, new Color(30, 30, 30, a));
            Raylib.DrawText(overlayTouche, cx - kw / 2, capY + 12, 30, new Color(20, 20, 20, a));
        }
    }

    // --- Le tick à pas fixe 64 Hz (voir docs/NETWORK_ROADMAP.md §4.1) ---
    static float tickAccumulateur = 0f;          // temps réel en attente de simulation
    static Vector3 posJoueurTickPrec;            // position du joueur au tick précédent
    static Vector3 posJoueurTickCour;            // position du joueur au dernier tick
    static bool lissageCamInitialise = false;    // faux tant qu'aucun pas n'a tourné

    //===== BOUCLE DU JEU =====
    public static void BouclePrincipale()
    {
        SetActiveMusic(ActiveMusic.Game);
        UpdateActiveMusicStream();

        //if (Raylib.IsKeyPressed(KeyboardKey.N))Raylib.PlaySound(hitmarkerSound);
        
        // ========================================================
        // [ZONE ILIAN] 1. GESTION DES MENUS ET TIMERS
        // ========================================================
        // ÉCHAP en jeu : pris au RELÂCHEMENT (voir IsPauseToggleTriggered) pour ne pas être
        // compté deux fois. On recule d'UN cran à la fois : overlay Options -> menu pause -> jeu.
        if (KeyBinds.IsPauseToggleTriggered())
        {
            if (isOptionsMenuOpen)
            {
                // 1er cran : on referme l'overlay Options, on revient au menu pause.
                isOptionsMenuOpen = false;
                Program.PlaySoundWithPriority(unselect, Program.SoundPriority.Low);
            }
            else
            {
                // 2e cran : on ouvre / ferme le menu pause.
                isPaused = !isPaused;
                if (isPaused)
                {
                    Raylib.EnableCursor();
                    Program.PlaySoundWithPriority(unselect, Program.SoundPriority.Low);
                }
                else
                {
                    Raylib.DisableCursor();
                    Program.PlaySoundWithPriority(select, Program.SoundPriority.Low);
                }
            }
        }

        float deltaTime = Raylib.GetFrameTime();

        if (!localPlayer.IsAlive)
        {
            deathRespawnTimer -= deltaTime;

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            const string messageMort = "Vous êtes mort";
            int messageLargeur = Raylib.MeasureText(messageMort, 50);
            Raylib.DrawText(messageMort, (LargeurFenetre - messageLargeur) / 2, HauteurFenetre / 2 - 50, 50, Color.Red);

            string compteARebours = deathRespawnTimer > 0
                ? $"Réapparition dans {MathF.Ceiling(deathRespawnTimer)}"
                : "Réapparition...";
            int compteLargeur = Raylib.MeasureText(compteARebours, 30);
            Raylib.DrawText(compteARebours, (LargeurFenetre - compteLargeur) / 2, HauteurFenetre / 2 + 25, 30, Color.White);

            Raylib.EndDrawing();

            if (deathRespawnTimer > 0)
            {
                // On continue de faire tourner la physique ET le réseau pendant le
                // countdown (même code que le tick unifié 64 Hz plus bas, dupliqué ici
                // avec sa propre BodyReference car `espionCube` n'est pas encore
                // déclarée à ce stade de la frame). Sans ça, `simulation.Timestep` et
                // `TickReseauEnJeu` ne tournent plus du tout pendant les 1.5s de mort :
                // toute la simulation se fige, et côté hôte, plus personne ne reçoit le
                // snapshot du monde (tout le monde se fige en ligne, pas seulement celui
                // qui vient de mourir). On ne fait ça que tant qu'on reste mort : la
                // frame du respawn retombe plus bas dans le tick unifié habituel, pour
                // ne pas consommer deltaTime deux fois.
                if (!isPaused || isOnline)
                {
                    BodyReference corpsDuMort = simulation.Bodies.GetBodyReference(PlayerId);
                    tickAccumulateur += deltaTime;
                    if (tickAccumulateur > 0.25f) tickAccumulateur = 0.25f;

                    while (tickAccumulateur >= NetConfig.TickDt)
                    {
                        tickAccumulateur -= NetConfig.TickDt;
                        posJoueurTickPrec = lissageCamInitialise ? posJoueurTickCour : corpsDuMort.Pose.Position;
                        simulation.Timestep(NetConfig.TickDt);
                        posJoueurTickCour = corpsDuMort.Pose.Position;
                        lissageCamInitialise = true;

                        if (isOnline)
                            TickReseauEnJeu(posJoueurTickCour, CameraYaw, CameraPitch);
                    }
                }

                return;
            }

            if (isOnline)
                AnnoncerMaMort();

            localPlayer.Respawn();

            Vector3 nouveauSpawn = listeSpawns.Count > 0
                ? listeSpawns[Raylib.GetRandomValue(0, listeSpawns.Count - 1)]
                : new Vector3(0, 10, 0);
            BodyReference joueurVivant = simulation.Bodies.GetBodyReference(PlayerId);
            joueurVivant.Pose.Position = nouveauSpawn;
            joueurVivant.Velocity.Linear = Vector3.Zero;
            joueurVivant.Velocity.Angular = Vector3.Zero;

            // Pas de `return` ici : IsAlive vient de repasser à `true`, donc le reste de
            // la frame (mouvement, rendu) s'exécute normalement à partir du nouveau spawn,
            // exactement comme avant ce correctif.
        }

        // Le changement d'arme se fait désormais uniquement via les barrils touchés

        // ==========================================
        // LE MOTEUR AUDIO DUCKING (Effet Bouchon d'oreille)
        // ==========================================
        if (duckingTimer > 0)
        {
            duckingTimer -= deltaTime;
            // Retour progressif à la normale. (Commence à 0.2 de force, remonte vers 1.0)
            duckingStrength = 0.2f + (0.8f * (1f - (duckingTimer / 1.5f))); 
            if (duckingStrength > 1f) duckingStrength = 1f;
        }
        else
        {
            duckingStrength = 1f;
        }
        
        // On applique en direct le volume de la musique
        if (currentMusicState == ActiveMusic.Game) 
            Raylib.SetMusicVolume(gameMusic, Settings.MasterVolume * Settings.MusicVolume * duckingStrength);



        laserTimer -= deltaTime;
        if (laserTimer < 0) laserTimer = 0;

        CheckBarrelRespawns(deltaTime);

        for (int i = activeExplosions.Count - 1; i >= 0; i--)
        {
            activeExplosions[i].timer -= deltaTime;
            if (activeExplosions[i].timer <= 0)
            {
                activeExplosions.RemoveAt(i);
            }
        }

        // Gestion de l'overlay de dégâts
        if (damageOverlayOpacity > 0)
        {
            damageOverlayOpacity -= deltaTime * 2f; // Diminue en 0.5 seconde
            if (damageOverlayOpacity < 0) damageOverlayOpacity = 0;
        }


        recoilAngle = recoilAngle + (0.0f - recoilAngle) * 15f * deltaTime;
        
        // Anti-tremblement : on bloque à 0 quand on est presque arrivé
        if (recoilAngle > -0.01f) recoilAngle = 0;



        




        // ========================================================
        // [ZONE BEPU] 2. PHYSIQUE ET MOUVEMENTS (TON CODE COMPLET)
        // ========================================================
        float hauteurVoulue = 0.8f; 
        bool IsWallRunning = false;

        BodyReference espionCube = simulation.Bodies.GetBodyReference(PlayerId);
        espionCube.Awake = true;
        Vector3 posCube = espionCube.Pose.Position; 



        //UPGRADE raycast -> shpere cast : la fameuse balle
        GroundSensor capteurSol = new GroundSensor(espionCube.CollidableReference);
        Vector3 directionLaser = new Vector3(0, -1f, 0);

        // La vélocité contient DÉJÀ la direction
        BodyVelocity velocitySphere = new BodyVelocity(directionLaser);

        // On crée la forme
        Sphere sphere = new Sphere(0.45f); 

        // On la place au centre du joueur
        RigidPose posDepartSphere = new RigidPose(posCube, Quaternion.Identity);
        float distanceCheck = 0.5f + 0.5f;

        // Le Sweep avec les 6 BONS paramètres
        simulation.Sweep(
            sphere,             // 1. TShape (La sphère)
            posDepartSphere,    // 2. RigidPose (Le point de départ)
            velocitySphere,     // 3. BodyVelocity (La direction du balayage)
            distanceCheck,      // 4. float maximumT (La distance max)
            pool,               // 5. BufferPool (La mémoire)
            ref capteurSol      // 6. Le handler (Ton capteur)
        );



        // Nettoyage des ennemis morts : retirer d'abord leurs corps physiques, puis supprimer les instances.
        int deadEnemiesCount = 0;
        foreach (Enemy enemy in enemiesList)
        {
            if (!enemy.isAlive)
            {
                // Mutateur "Zombies explosifs" : le souffle part AVANT qu'on retire le
                // corps (GetPosition() renvoie Vector3.Zero une fois le zombie mort).
                if (enemy.explosif)
                {
                    try
                    {
                        Vector3 posMort = simulation.Bodies.GetBodyReference(enemy.bodyId).Pose.Position;
                        enemy.explosif = false; // une seule explosion par zombie
                        FaireExploserZombie(posMort, posCube);
                    }
                    catch (Exception ex) { Console.WriteLine($"[WARN] Explosion de zombie ignorée : {ex.Message}"); }
                }

                simulation.Bodies.Remove(enemy.bodyId);
                deadEnemiesCount++;
            }
        }
        Program.killCount += deadEnemiesCount;
        enemiesList.RemoveAll(e => !e.isAlive);

        // ACTIVATION DES CERVEAUX (IA)
        if (!isPaused || isOnline)
        {
            foreach (Enemy enemy in enemiesList)
            {
                if (modeTuto)
                {
                    // TUTO : les zombies sont des cibles FIXES (pas d'IA, pas de poursuite).
                    // On annule le déplacement horizontal, la gravité les garde posés.
                    BodyReference corpsZombie = Program.simulation.Bodies.GetBodyReference(enemy.bodyId);
                    corpsZombie.Velocity.Linear.X = 0f;
                    corpsZombie.Velocity.Linear.Z = 0f;
                    corpsZombie.Velocity.Angular = System.Numerics.Vector3.Zero;
                }
                else
                {
                    // On leur donne la position de ton Cube Espion pour qu'ils te poursuivent !
                    enemy.Maj(posCube, ref espionCube);
                }
                            
                // Vérifier si l'ennemi tombe dans le vide (limite de void)
                try
                {
                    BodyReference enemyBody = Program.simulation.Bodies.GetBodyReference(enemy.bodyId);
                    if (enemyBody.Pose.Position.Y < -30f)
                    {
                        enemy.TakeDamage(1);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Void damage check failed for enemy {enemy.bodyId}: {ex.Message}");
                }
            }
        }
        



        GroundSensor capteurGlissade = new GroundSensor(espionCube.CollidableReference);
        float longueurLaserGlissade = 1.2f;
        simulation.RayCast(posCube, directionLaser, longueurLaserGlissade, ref capteurGlissade);

        if (capteurSol.toucheSol) {
            NbJump = NbJumpMax + 1;
            CanDash = true;
        }

        // ==========================================
        // MUTATEUR "Dégâts de chute"
        // ==========================================
        // On regarde la vitesse verticale de la frame PRÉCÉDENTE : à la frame de
        // l'atterrissage, la collision l'a déjà ramenée à ~0.
        if (MatchRules.ChuteMortelle && capteurSol.toucheSol && !etaitAuSol
            && vitesseChutePrecedente < -ChuteSansDegat)
        {
            int degatsChute = (int)MathF.Round((-vitesseChutePrecedente - ChuteSansDegat) * 3f);
            if (degatsChute > 0) localPlayer.TakeDamage(degatsChute);
        }
        etaitAuSol = capteurSol.toucheSol;
        vitesseChutePrecedente = espionCube.Velocity.Linear.Y;

        // ==========================================
        // CHRONOMÈTRES ET SONS D'INTERFACE (UX)
        // ==========================================
        // 1. On mémorise l'état AVANT de faire passer le temps
        bool dashEtaitPret = dashChrono >= 90;
        bool murEtaitPret = wallChrono >= WallChronoMax;

        // 2. On fait avancer le temps (1 frame)
        if (dashChrono < 90) dashChrono++;
        if (wallChrono < WallChronoMax) wallChrono++;
        
        if (!murEtaitPret && wallChrono >= WallChronoMax)
        {
            Program.PlaySoundWithPriority(wallNotifSound, Program.SoundPriority.Low);
        }
        


        // LE CERVEAU DE LA CAMÉRA FPS
        bool hasWeapon = true; 
        // Si pause, la sensi = 0   
        float sensi = isPaused ? 0f : Settings.MouseSensitivity;

        // 1. On anticipe la vérification de l'arme pour savoir si on vise
        bool isMelee = (currentWeapon == karambitknife || currentWeapon == bazookaWeapon); 
        bool isScopedWeapon = (currentWeapon == sniperrifle || currentWeapon == pistolWeapon); 
        bool isAiming = KeyBinds.IsAimingHeld() && hasWeapon && !isMelee;
        Program.localIsAiming = isAiming; // pour que les autres voient notre arme centrée en visée

        // 2. LA REDUCTION DE SENSIBILITÉ (Le secret professionnel)
        if (isAiming)
        {
            if (isScopedWeapon) 
                sensi *= 0.2f; // Le Sniper divise la sensi par 5 (20%) pour être ultra précis !
            else 
                sensi *= 0.5f; // Les armes normales divisent la sensi par 2 (50%)
        }

        // 3. On tourne la caméra avec la bonne sensibilité (SOURIS)
        Vector2 mouseDelta = Raylib.GetMouseDelta();
        CameraYaw -= mouseDelta.X * sensi;
        CameraPitch -= mouseDelta.Y * sensi;

        // 3bis. MANETTE : rotation de la caméra au stick droit.
        // La sensibilité manette est en radians/seconde -> on multiplie par deltaTime pour
        // être indépendant du framerate (la souris, elle, donne déjà un delta en pixels).
        Vector2 padLook = isPaused ? Vector2.Zero : KeyBinds.GetGamepadLook();
        float padSensi = Settings.GamepadLookSensitivity;
        if (isAiming) padSensi *= isScopedWeapon ? 0.4f : 0.6f; // visée = plus précis (comme la souris)
        CameraYaw   -= padLook.X * padSensi * deltaTime;
        CameraPitch -= (Settings.GamepadInvertY ? -padLook.Y : padLook.Y) * padSensi * deltaTime;

        float PitchLimit = 1.55f;
        if (CameraPitch > PitchLimit) CameraPitch = PitchLimit;
        if (CameraPitch < -PitchLimit) CameraPitch = -PitchLimit;

        Vector3 CamFroward = new Vector3(
            MathF.Cos(CameraPitch) * MathF.Sin(CameraYaw),
            MathF.Sin(CameraPitch),
            MathF.Cos(CameraPitch) * MathF.Cos(CameraYaw)
        );

        // LES JAMBES DU JOUEUR 
        Vector3 GroundForward = new Vector3(CamFroward.X, 0, CamFroward.Z);
        if (GroundForward.LengthSquared() > 0.0001f) GroundForward = Vector3.Normalize(GroundForward); 

        Vector3 GroundRight = Vector3.Cross(GroundForward, new Vector3(0, 1f, 0));
        GroundRight = Vector3.Normalize(GroundRight);
        Vector3 deplacementVoulu = Vector3.Zero; 

        WallSensor capteurMurDroit = new WallSensor(espionCube.CollidableReference);
        WallSensor capteurMurGauche = new WallSensor(espionCube.CollidableReference);
        WallSensor capteurMurAvant = new WallSensor(espionCube.CollidableReference);
        WallSensor capteurMurArriere = new WallSensor(espionCube.CollidableReference);
        float longueurLaserMur = 0.8f;
        simulation.RayCast(posCube, GroundRight, longueurLaserMur, ref capteurMurDroit);
        simulation.RayCast(posCube, -GroundRight, longueurLaserMur, ref capteurMurGauche);
        simulation.RayCast(posCube, GroundForward, longueurLaserMur, ref capteurMurAvant);
        simulation.RayCast(posCube, -GroundForward, longueurLaserMur, ref capteurMurArriere);



       

        // ==========================================
        // MODE CONSTRUCTION (Touche F)
        // ==========================================
        // La préview porte notre couleur de skin, comme le mur une fois posé.
        // Alpha bas (70) : on doit voir les joueurs/ennemis À TRAVERS la préview du mur !
        Color couleurMonSkin = couleursSkin[skinCouleur];
        couleurMurTransparent = new Color(couleurMonSkin.R, couleurMonSkin.G, couleurMonSkin.B, (byte)70);
        modeConstruction = false;
        if (KeyBinds.IsBuildWallPressed())
        {
            modeConstruction = true;
            // On active/désactive
            //Console.WriteLine("construction");
        }

        // Variables pour mémoriser l'aperçu dynamique
        Vector3 positionPrevueMur = Vector3.Zero;
        Quaternion rotationPrevueMur = Quaternion.Identity;

        if (modeConstruction)
        {
            // 1. On garde la direction pure de la caméra (sans annuler le Y !)
            Vector3 directionRegard = CamFroward;
            
            // 2. Position : à 3 mètres devant LES YEUX (camera.Position), plus les pieds
            positionPrevueMur = camera.Position + (directionRegard * 3.0f);

            // 3. Calcul de la rotation 3D (Yaw = Horizontal, Pitch = Vertical)
            float yaw = MathF.Atan2(directionRegard.X, directionRegard.Z);
            float pitch = MathF.Asin(directionRegard.Y); 
            
            // On génère la rotation complexe pour BEPU
            rotationPrevueMur = Quaternion.CreateFromYawPitchRoll(yaw, -pitch, 0);

            // 4. Poser le mur au Clic Gauche
            
            if (KeyBinds.IsShootingPressed())
            {
                // S4 : le même plafond de murs que l'hôte applique aux paquets reçus.
                // Un joueur légitime ne peut donc JAMAIS être désynchronisé par le plafond.
                if (wallChrono >= WallChronoMax && (!isOnline || PeutPoserMur(myPlayerId)))
                {
                    Program.PlaySound3D(wallSound, positionPrevueMur, 20f);
                    wallChrono = 0;
                    PoserMur(positionPrevueMur, rotationPrevueMur, skinCouleur);

                    // En ligne : on annonce le mur pour qu'il existe chez TOUT le monde
                    // (visuel + physique), sinon les autres passent au travers !
                    if (isOnline) AnnoncerMurPose(positionPrevueMur, rotationPrevueMur);
                }
                else
                {
                    couleurMurTransparent = new Color(255, 255, 255, 140);
                    Raylib.PlaySound(noAmmoSound);
                }
                
            }
        }





        //vide
        if (Raylib.IsKeyPressed(KeyBinds.DebugTeleportCenter))
            {
                // 1. On modifie les coordonnées instantanément (ex: on le remet à 10m de haut au centre)
                espionCube.Pose.Position = new Vector3(0, 10f, 0); 
                
                // 2. On remet l'inertie et la vitesse à zéro pour un arrêt net !
                espionCube.Velocity.Linear = Vector3.Zero;  // Stop le déplacement
                espionCube.Velocity.Angular = Vector3.Zero; // Stop la rotation sur lui-même

            }
        
        if (Raylib.IsKeyPressed(KeyBinds.DebugTakeDamage)) localPlayer.TakeDamage(15);
        if (Raylib.IsKeyPressed(KeyBinds.DebugHeal)) localPlayer.Heal(25);
        
        if (espionCube.Pose.Position.Y < -30f) localPlayer.TakeDamage(1);

        if (espionCube.Pose.Position.Y < -50f)
        {
            // 1. On le fait réapparaître sur un point de spawn ALÉATOIRE de la map
                if (Program.listeSpawns.Count > 0)
                {
                    int idxRespawn = Raylib.GetRandomValue(0, Program.listeSpawns.Count - 1);
                    espionCube.Pose.Position = Program.listeSpawns[idxRespawn];
                }
                else
                {
                    espionCube.Pose.Position = new Vector3(0, 10f, 0); // secours si aucun spawn chargé
                }

                // 2. On remet l'inertie et la vitesse à zéro pour un arrêt net !
                espionCube.Velocity.Linear = Vector3.Zero;  // Stop le déplacement
                espionCube.Velocity.Angular = Vector3.Zero; // Stop la rotation sur lui-même

        }


        //print coordonés 
        if (Raylib.IsKeyPressed(KeyBinds.DebugPrintPosition)) Console.WriteLine($"{espionCube.Pose.Position}");


        float vitesseHorizontale = new Vector2(espionCube.Velocity.Linear.X, espionCube.Velocity.Linear.Z).Length();
        float vitesseVerticale = MathF.Abs(espionCube.Velocity.Linear.Y);
        float vitesseDescente = 0f;
        bool IsSprinting = KeyBinds.IsSprintingPressed();


        // ==========================================
            // SYSTÈME DE BRUITS DE PAS
            // ==========================================
            // Conditions : Toucher le sol, avancer, et ne pas être accroupi/en glissade
            if (capteurSol.toucheSol && vitesseHorizontale > 1f && !KeyBinds.IsCrouchingPressed())
            {
                footstepTimer -= deltaTime;
                
                if (footstepTimer <= 0f)
                {
                    // 1. Piocher un son au hasard entre 1, 2 et 3
                    int randomStep = Raylib.GetRandomValue(0, 2);
                    Sound stepSound = isLeftStep ? Program.footstepLeft[randomStep] : Program.footstepRight[randomStep];
                    
                    // 2. Jouer le son
                    Program.PlaySoundWithPriority(stepSound, Program.SoundPriority.Low, 0.7f);
                    
                    // 3. Inverser le pied pour la prochaine fois
                    isLeftStep = !isLeftStep;
                    
                    // 4. Définir le délai avant le prochain pas (plus court si on sprinte)
                    footstepTimer = IsSprinting ? 0.3f : 0.45f; 
                }
            }
            else
            {
                // Si on s'arrête ou qu'on saute, on remet le timer à zéro 
                // pour que le premier pas soit instantané quand on repart !
                footstepTimer = 0f; 
            }



        
        //la plus part des variables ici servent pour la glissade dynamique

        if (!isPaused || isOnline)
        {
            // Saut

            if (KeyBinds.IsJumpingPressed())
            {
                if (NbJump > NbJumpMax)
                {
                    NbJump--;
                    if (espionCube.Velocity.Linear.Y > 0) { espionCube.Velocity.Linear.Y += 5f; } else { espionCube.Velocity.Linear.Y = 5f; }
                    
                    // ==========================================
                    // NOUVEAU : LE SLIDE-JUMP (Bunny Hop) !
                    // ==========================================
                    // Si on saute PENDANT une glissade rapide, on gagne un boost massif vers l'avant !
                    if (KeyBinds.IsCrouchingPressed() && vitesseHorizontale > 5f)
                    {
                        Vector3 boostDir = GroundForward;
                        if (deplacementVoulu.LengthSquared() > 0) boostDir = Vector3.Normalize(deplacementVoulu);
                        
                        // L'impulsion du saut (ajoute +4m/s instantanément à ta vitesse)
                        espionCube.Velocity.Linear += boostDir * 4f; 
                        //Raylib.PlaySound(swoosh);
                    }
                }
            }

            // Déplacements & WallRun
            if (!capteurSol.toucheSol)
            {
                if ((capteurMurDroit.toucheMur && KeyBinds.IsMoveRightPressed()) || (capteurMurGauche.toucheMur && KeyBinds.IsMoveLeftPressed()) || (capteurMurAvant.toucheMur && KeyBinds.IsMoveForwardPressed()) || (capteurMurArriere.toucheMur && KeyBinds.IsMoveBackwardPressed()))
                {
                    IsWallRunning = true;
                }
            }

            if (KeyBinds.IsMoveForwardPressed() && !capteurMurAvant.toucheMur) deplacementVoulu += GroundForward;
            if (KeyBinds.IsMoveBackwardPressed() && !capteurMurArriere.toucheMur) deplacementVoulu -= GroundForward;
            if (KeyBinds.IsMoveLeftPressed() && !capteurMurGauche.toucheMur) deplacementVoulu -= GroundRight; 
            if (KeyBinds.IsMoveRightPressed() && !capteurMurDroit.toucheMur) deplacementVoulu += GroundRight;

            if (deplacementVoulu.LengthSquared() > 0)
            {
                deplacementVoulu = Vector3.Normalize(deplacementVoulu);

                // NOUVEAU : INCLINAISON SUR LA PENTE
                // Si on est sur le sol et que ce sol n'est pas parfaitement plat
                if (capteurSol.toucheSol && capteurSol.normaleDuSol != new Vector3(0, 1f, 0))
                {
                    // 1. On projette notre déplacement horizontal sur le plan incliné du sol
                    // Formule mathématique : Vecteur = Vecteur - (Vecteur . Normale) * Normale
                    float dotProduct = Vector3.Dot(deplacementVoulu, capteurSol.normaleDuSol);
                    deplacementVoulu = deplacementVoulu - (capteurSol.normaleDuSol * dotProduct);
                    
                    // 2. On renormalise pour ne pas perdre de vitesse dans la pente
                    if (deplacementVoulu.LengthSquared() > 0)
                    {
                        deplacementVoulu = Vector3.Normalize(deplacementVoulu);
                    }
                }
            }

            IsSprinting = KeyBinds.IsSprintingPressed();
            float SpeedCoef = IsSprinting ? 1.7f : 1f;
            // MUTATEUR "Vitesse de course" : marche ET sprint (ni le dash ni la glissade).
            float vMax = 8f * SpeedCoef * MatchRules.VitesseMul;
            float fAcceleration = 0.2f;
            float rollCible = 0f;

            if (IsWallRunning)
            {
                if (KeyBinds.IsJumpingPressed())
                {
                    espionCube.Velocity.Linear.Y += 2f;
                    if (capteurMurDroit.toucheMur) deplacementVoulu -= GroundRight*20;
                    if (capteurMurGauche.toucheMur) deplacementVoulu += GroundRight*20;
                    if (capteurMurAvant.toucheMur) deplacementVoulu -= GroundForward*20;
                    if (capteurMurArriere.toucheMur) deplacementVoulu += GroundForward*20;
                    Console.WriteLine("wall jump");
                }

                NbJump = NbJumpMax + 1;
                CanDash = true;

                fAcceleration = 0.1f;
                if (espionCube.Velocity.Linear.Y < 0) espionCube.Velocity.Linear.Y = -1f;
                // La caméra se penche du côté OPPOSÉ au mur sur lequel on court.
                // (Program.rollActuel positif = penche à droite, négatif = penche à gauche)
                if (capteurMurDroit.toucheMur) rollCible = -0.25f;       // mur à droite -> on se penche à gauche
                else if (capteurMurGauche.toucheMur) rollCible = 0.25f;  // mur à gauche -> on se penche à droite
            }
            else { rollCible = 0f; fAcceleration = 0.2f; }

            // Inclinaison douce de la caméra vers la cible (lue plus bas par camera.Up, ligne ~1140)
            Program.rollActuel += (rollCible - Program.rollActuel) * MathF.Min(1f, deltaTime * 10f);

            // Accroupir & Glissade

            if (KeyBinds.IsCrouchingPressed() && !KeyBinds.IsJumpHeld())
            {
                espionCube.SetShape(PlayerTicketAccroupi);
                hauteurVoulue = 0.5f;

                if (!capteurGlissade.toucheSol) 
                {
                    // On est en l'air et accroupi (Prêt à atterrir)
                    vitesseDescente--;
                    if (vitesseDescente < -20) vitesseDescente = -20; 
                    espionCube.Velocity.Linear.Y += vitesseDescente;
                    
                    // AIR CONTROL : Permet de tourner en l'air sans perdre de vitesse !
                    fAcceleration = 0.02f; 
                } 
                else 
                {
                    // ==========================================
                    // LA GLISSADE ULTRA DYNAMIQUE
                    // ==========================================
                    
                    // Boost d'entrée (Uniquement si on lance la glissade depuis un sprint)
                    if (KeyBinds.IsCrouchPressedEdge() && vitesseHorizontale > 6f)
                    {
                        espionCube.Velocity.Linear += GroundForward * 3f; 
                        //Raylib.PlaySound(swoosh); 
                    }

                    Vector3 normale = capteurSol.normaleDuSol;
                    bool surPente = normale.Y < 0.98f; 

                    // On enlève la bride de vitesse : Le joueur peut dépasser la limite !
                    vMax = 30f; 

                    if (surPente)
                    {
                        // --- MODE 1 : ASPIRATION PAR LA PENTE ---
                        Vector3 gravite = new Vector3(0, -1f, 0);
                        float dot = Vector3.Dot(gravite, normale);
                        Vector3 directionDescente = gravite - (normale * dot);

                        if (directionDescente.LengthSquared() > 0)
                        {
                            directionDescente = Vector3.Normalize(directionDescente);
                            // On ajoute constamment de la vitesse selon l'inclinaison
                            espionCube.Velocity.Linear += directionDescente * 45f * deltaTime;
                        }
                        
                        // Friction quasi-nulle : On est sur de la glace !
                        fAcceleration = 0.005f; 
                    }
                    else
                    {
                        // --- MODE 2 : GLISSADE SUR LE PLAT (Steering & Coasting) ---
                        if (vitesseHorizontale > 3f)
                        {
                            // On autorise le ZQSD à diriger la glissade sans gagner de vitesse
                            vMax = vitesseHorizontale; 
                            
                            // ==========================================
                            // CORRECTION : DES FRICTIONS MINUSCULES
                            // ==========================================
                            // Si tu arrives d'un dash (vitesse > 20), la friction est quasi nulle (0.002f) !
                            if (vitesseHorizontale > 20f) fAcceleration = 0.002f; 
                            else if (vitesseHorizontale > 12f) fAcceleration = 0.008f;
                            else if (vitesseHorizontale > 8f) fAcceleration = 0.015f;  
                            else fAcceleration = 0.03f;                                
                            
                            // Freinage naturel (Perte de seulement 0.2% par frame au lieu de 1.5%)
                            // Ça te permet de conserver ton élan sur plusieurs dizaines de mètres !
                            espionCube.Velocity.Linear.X *= 0.999f;
                            espionCube.Velocity.Linear.Z *= 0.999f;
                        }
                        else
                        {
                            // Fin de la glissade, on rampe
                            vMax = 3f;
                            fAcceleration = 0.2f;
                        }
                    }
                }
            } 
            else 
            {
                // Le joueur est debout
                espionCube.SetShape(PlayerTicket);
                hauteurVoulue = 0.8f; 
                
                // CORRECTION : On réapplique le multiplicateur de sprint ici !
                vMax = 8f * SpeedCoef * MatchRules.VitesseMul;
                
                // Air Control quand on est debout en l'air
                fAcceleration = capteurSol.toucheSol ? 0.2f : 0.02f; 
                vitesseDescente = 0;
            }


            // ==========================================
            // LA RÈGLE D'OR DE LA CONSERVATION DE VITESSE (Air Strafing)
            // ==========================================
            // Si tu vas plus vite que ta limite (ex: grâce à une pente ou un dash)
            // ET que tu es en l'air OU en train de glisser...
            if (vitesseHorizontale > vMax && (!capteurSol.toucheSol || KeyBinds.IsCrouchingPressed()))
            {
                // On empêche le jeu de te freiner en élevant la limite temporairement !
                // Tes touches ZQSD vont "tirer" ta trajectoire vers la caméra sans perdre l'élan.
                vMax = vitesseHorizontale; 
            }

            // MUTATEUR "Glisse du sol" : on divise l'accélération ET le freinage au sol.
            // À 100 % le facteur tombe à 0.05 : on met une éternité à lancer ET à s'arrêter.
            // Aucun effet en l'air (le contrôle aérien reste celui du jeu de base).
            if (capteurSol.toucheSol) fAcceleration *= MatchRules.GlisseFacteur;

            // --- CALCUL FINAL ---
            // CORRECTION 2 : On retire "* SpeedCoef" ici, car il est déjà dans le vMax de base !
            Vector3 targetVelocity = deplacementVoulu * vMax;
            espionCube.Velocity.Linear.X += (targetVelocity.X - espionCube.Velocity.Linear.X) * fAcceleration;        
            espionCube.Velocity.Linear.Z += (targetVelocity.Z - espionCube.Velocity.Linear.Z) * fAcceleration;


            if (capteurSol.toucheSol && !KeyBinds.IsJumpHeld())
            {
                if (targetVelocity.Y > 0) 
                {
                    // On aide activement la capsule à monter la pente en appliquant la vitesse Y voulue !
                    espionCube.Velocity.Linear.Y = targetVelocity.Y;
                }
            }

            // Dash
            int dashSpeed = 40;
            if (!capteurSol.toucheSol) dashSpeed = 18; // SI ON EST EN L AIR

            if (KeyBinds.IsDashingPressed() && CanDash && dashChrono >= 90){
                Raylib.PlaySound(swoosh);
                Vector3 directionDash = GroundForward; 

                if (deplacementVoulu.LengthSquared() > 0)
                {
                    directionDash = deplacementVoulu; 
                }
                espionCube.Velocity.Linear += directionDash * dashSpeed; //puissance du dash

                CanDash = false ;
                dashChrono = 0;
            }

            // ==========================================
            // SYSTÈMES AUDIO CONTINUS (Vent & Coeur)
            // ==========================================
            // --- 1. LE VENT DE VITESSE (Avec Lerp) ---
            float vitesseTotale = espionCube.Velocity.Linear.Length();
            
            // 1. On définit la CIBLE (Target)
            if (vitesseTotale > 12f && localPlayer.IsAlive)
            {
                Program.targetWindVolume = Math.Clamp((vitesseTotale - 12f) / 23f, 0f, 1f);
            }
            else
            {
                Program.targetWindVolume = 0f; // Silence voulu
            }

            // 2. LE LERP MAGIQUE ! 
            // La valeur actuelle se rapproche de la cible de "X" % par seconde.
            // Le "5f" est la vitesse de transition (plus c'est bas, plus c'est doux)
            Program.currentWindVolume += (Program.targetWindVolume - Program.currentWindVolume) * 5f * deltaTime;

            // 3. Application du son lissé
            if (Program.currentWindVolume > 0.01f) // Si on entend un minimum d'air
            {
                if (!Raylib.IsSoundPlaying(Program.windSound)) Raylib.PlaySound(Program.windSound);
                
                Raylib.SetSoundVolume(Program.windSound, Program.currentWindVolume * Settings.SFXVolume * duckingStrength * 0.7f);
                
                // Le Pitch est aussi lissé puisqu'il se base sur le volume actuel !
                float windPitch = 1f + (Program.currentWindVolume * 0.4f); 
                Raylib.SetSoundPitch(Program.windSound, windPitch);
            }
            else
            {
                // Si le volume est vraiment retombé à zéro, on coupe le fichier audio
                if (Raylib.IsSoundPlaying(Program.windSound)) Raylib.StopSound(Program.windSound);
            }

            // --- 2. LE BATTEMENT DE CŒUR (Santé critique) ---
            if (localPlayer.Health <= 25 && localPlayer.Health > 0)
            {
                if (!Raylib.IsSoundPlaying(Program.heartbeatSound)) Raylib.PlaySound(Program.heartbeatSound);
                
                // Plus on est proche de 0 HP, plus le cœur tape fort dans les oreilles !
                float dangerLevel = 1f - ((float)localPlayer.Health / 25f);
                float heartVolume = 0.5f + (dangerLevel * 3f);
                
                Raylib.SetSoundVolume(Program.heartbeatSound, heartVolume * Settings.SFXVolume * duckingStrength);
            }
            else
            {
                // On s'est soigné ou on est mort : le cœur s'arrête
                if (Raylib.IsSoundPlaying(Program.heartbeatSound)) Raylib.StopSound(Program.heartbeatSound);
            }


        }

        if (isPaused && !isOnline)
        {
            if (Raylib.IsSoundPlaying(Program.windSound)) Raylib.StopSound(Program.windSound);
            if (Raylib.IsSoundPlaying(Program.heartbeatSound)) Raylib.StopSound(Program.heartbeatSound);
        }

        // Jump pad (Sandbox)
        //if (posCube.X > 9 && posCube.X < 11 && posCube.Z > -1 && posCube.Z < 1 && capteurSol.toucheSol) espionCube.Velocity.Linear.Y += 20f;

        // ========================================================
        // LE TICK UNIFIÉ 64 Hz (Phase 0, §4.1 de la roadmap)
        // La physique ET le réseau battent sur la MÊME horloge à pas fixe,
        // découplée du framerate d'affichage. L'accumulateur transforme le
        // temps réel (irrégulier) en pas de simulation exacts de 1/64 s.
        // ========================================================
        if (!isPaused || isOnline)
        {
            tickAccumulateur += deltaTime;
            // Garde-fou "spirale de la mort" : après un gros freeze (chargement,
            // alt-tab...), on ne rattrape pas plus de 0.25 s de simulation d'un coup.
            if (tickAccumulateur > 0.25f) tickAccumulateur = 0.25f;

            while (tickAccumulateur >= NetConfig.TickDt)
            {
                tickAccumulateur -= NetConfig.TickDt;

                // On mémorise la position AVANT le pas pour lisser la caméra entre deux ticks
                posJoueurTickPrec = lissageCamInitialise ? posJoueurTickCour : espionCube.Pose.Position;
                simulation.Timestep(NetConfig.TickDt);
                posJoueurTickCour = espionCube.Pose.Position;
                lissageCamInitialise = true;

                // LE BATTEMENT DE COEUR DU MULTIJOUEUR : notre état part à CHAQUE tick
                // (et l'hôte diffuse le snapshot du monde entier)
                if (isOnline)
                {
                    TickReseauEnJeu(posJoueurTickCour, CameraYaw, CameraPitch);
                }
            }
        }

        // Application finale de la Caméra : position INTERPOLÉE entre les deux
        // derniers pas physiques (sinon, à 60 im/s pour 64 ticks/s, la vue
        // sautillerait 4 fois par seconde au battement des deux horloges).
        Vector3 posCamera = posCube;
        if (lissageCamInitialise)
        {
            // Respawn/téléportation : on saute directement, pas de glissade
            if (Vector3.Distance(posJoueurTickPrec, posJoueurTickCour) > NetConfig.SeuilTeleportation)
                posJoueurTickPrec = posJoueurTickCour;
            float alphaCam = Math.Clamp(tickAccumulateur / NetConfig.TickDt, 0f, 1f);
            posCamera = Vector3.Lerp(posJoueurTickPrec, posJoueurTickCour, alphaCam);
        }
        camera.Position = new Vector3(posCamera.X, posCamera.Y + hauteurVoulue, posCamera.Z);
        camera.Up = Vector3.Transform(new Vector3(0, 1f, 0), Matrix4x4.CreateFromAxisAngle(CamFroward, rollActuel));
        camera.Target = camera.Position + CamFroward;

        // L'INTERPOLATION des joueurs distants + vieillissement des tirs (par frame, pas par tick)
        if (isOnline)
        {
            MettreAJourReseauVisuel(deltaTime);
        }



        if (Raylib.IsKeyPressed(KeyBinds.DebugToggleInfo)){
            if (debugInfo)
            {
                debugInfo = false;
            } else
            {
                debugInfo = true;
            }
        }

        // TEST OVERLAY : mode debug (F2) actif + touche O
        if (debugInfo && Raylib.IsKeyPressed(KeyboardKey.O))
            DeclencherOverlayTuto("TEST OVERLAY", "O", "Overlay de test (F2 + O).");

        // TUTORIEL : explique la mécanique dès que le joueur atteint le palier correspondant
        MajZonesTuto(posCube);

        


        // === SYSTÈME DE SURVIE : LE TEMPS PASSE ===
        if (localPlayer.IsAlive && (!isPaused || isOnline)) // Le chrono tourne seulement si on est en vie
        {
            survivalTime += deltaTime;
            enemySpawnTimer -= deltaTime;

            // ==========================================
            // MUTATEUR "Régénération" : soin auto après 5 s sans prendre de dégâts.
            // (tempsSansDegats est remis à zéro par Player.TakeDamage)
            // ==========================================
            tempsSansDegats += deltaTime;
            if (MatchRules.RegenParSec > 0f && tempsSansDegats >= 5f && localPlayer.Health < localPlayer.MaxHealth)
            {
                regenAccumulateur += MatchRules.RegenParSec * deltaTime;
                int pvRendus = (int)regenAccumulateur;
                if (pvRendus > 0) { localPlayer.Heal(pvRendus); regenAccumulateur -= pvRendus; }
            }
            else regenAccumulateur = 0f;

            // MUTATEUR "Modificateur aléatoire" : un réglage tiré au sort toutes les 60 s.
            TickModificateurAleatoire(deltaTime);

            // Si le temps est écoulé, et qu'il n'y a pas déjà trop de zombies (ex: limite de 30)
            // EN LIGNE : pas de zombies, c'est du PvP !
            // (enemySpawnPoints vide = aucun zombie possible : c'est le cas du TUTO)
            if (!isOnline && zombiesActifs && enemySpawnPoints.Count > 0 && enemySpawnTimer <= 0f && enemiesList.Count < maxZombies)
            {
                // On choisit un point de spawn au hasard
                int randomSpawnIndex = random.Next(enemySpawnPoints.Count);

                // On crée le zombie (recalé sur la grille de navigation)
                enemiesList.Add(CreerZombie(enemySpawnPoints[randomSpawnIndex]));
                
                // On relance le chrono d'apparition
                enemySpawnTimer = timeBetweenSpawns;
            }
        }


        // ========================================================
        // [ZONE MIXTE] 3. RENDU GRAPHIQUE (RAYLIB)
        // ========================================================
        Vector3 soleilPosition = new Vector3(50.0f, 100.0f, 50.0f);
        Vector4 soleilCouleur = new Vector4(1.0f, 0.9f, 0.8f, 1.0f); 

        Raylib.SetShaderValue(lightShader, lightPosLoc, soleilPosition, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(lightShader, lightColorLoc, soleilCouleur, ShaderUniformDataType.Vec4);
        
        // On envoie la position de la caméra pour le brouillard 
        Raylib.SetShaderValue(lightShader, viewPosLoc, camera.Position, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(lightShader, Program.applyFogLoc, new int[] { 1 }, ShaderUniformDataType.Int);

        // MUTATEUR "Distance de vue" : le brouillard se referme (60 m) ou s'ouvre (600 m).
        // On garde la proportion d'origine du jeu (début = 27 % de la fin, soit 40/150).
        float porteeVue = MatchRules.DistanceVue;
        Raylib.SetShaderValue(lightShader, Program.fogStartLoc, porteeVue * 0.27f, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(lightShader, Program.fogEndLoc, porteeVue, ShaderUniformDataType.Float);

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.SkyBlue);
        Color couleurZenith = new Color(20, 25, 45, 255);   // Bleu très sombre en haut
        Color couleurHorizon = new Color(120, 60, 50, 255); // Orange/Rouge sale à l'horizon
        
        Raylib.DrawRectangleGradientV(0, 0, LargeurFenetre, HauteurFenetre, couleurZenith, couleurHorizon);

        Raylib.BeginMode3D(camera);

            // REMET CETTE LIGNE :
            Raylib.DrawModel(mapModel, mapPosition, mapScale, Color.White);
            if (debugInfo) Raylib.DrawModelWires(mapModel, mapPosition, mapScale, Color.Black);
            

            // ==========================================
            // DESSIN DES MURS DU JOUEUR (3D Dynamique)
            // ==========================================
            foreach (MurPose mur in listeMur)
            {
                // Mathématiques : Quaternion BEPU -> Axe/Angle Raylib
                float angleRadians = 2.0f * MathF.Acos(mur.rotation.W);
                Vector3 axe = new Vector3(mur.rotation.X, mur.rotation.Y, mur.rotation.Z);
                if (axe.LengthSquared() > 0.0001f) axe = Vector3.Normalize(axe); else axe = new Vector3(0, 1, 0);

                float angleDegres = angleRadians * (180.0f / MathF.PI);
                Color couleurMur = couleursSkin[mur.couleurIdx]; // la couleur du skin de son poseur

                Raylib.DrawModelEx(visuelMur, mur.position, axe, angleDegres, Vector3.One, couleurMur);
                //Raylib.DrawModelWiresEx(visuelMur, mur.position, axe, angleDegres, Vector3.One, Color.Black);
            }

            





            //COUCHES TRANSPARENTES (bas vers le haut)
            Color gazRouge = new Color(255, 50, 50, 8);
            //vide 
            int nbrCouche = 50;
            float espaceCouche = 1f;
            float hauteurVide = -30f; 

            //boucle commence au fond (i = 50) et remonte jusqu'à la surface (i = 1)
            if (espionCube.Pose.Position.Y <= -1)
            {
                for (int i = nbrCouche; i > 0; i--)
                {
                    float hauteur = hauteurVide - (i * espaceCouche);
                    
                    // On dessine une plaque très fine (épaisseur 0.1f au lieu de 1f)
                    Raylib.DrawCube(new Vector3(0, hauteur, 0), 1000f, 0.1f, 1000f, gazRouge);
                }
            }

            //dessiner le model du joueur (avec la couleur de son skin !)
            // IMPORTANT : on dessine la capsule à la MÊME position interpolée que l'oeil
            // (posCamera), pas à la position physique brute (posCube). Sinon, à grande
            // vitesse, la caméra (interpolée) prend du retard sur la capsule (brute) et
            // on aperçoit sa propre couleur de skin traverser le champ de vision.
            Vector3 PointHaut = new Vector3(posCamera.X, posCamera.Y + 0.5f, posCamera.Z);
            Vector3 PointBas = new Vector3(posCamera.X, posCamera.Y - 0.5f, posCamera.Z);
            Raylib.DrawCapsule(PointHaut,PointBas,0.5f,8,8, Program.couleursSkin[Program.skinCouleur]);

            // ==========================================
            // DESSINER LES AUTRES JOUEURS (Multijoueur LAN)
            // ==========================================
            if (isOnline) DessinerJoueursDistants();




            // Dessiner les ennemis (Nouvelle version Physique)
            // Dessiner les ennemis (Nouvelle version Physique et LookAt)
            foreach (Enemy enemy in enemiesList)
            {
                Vector3 positionPhysique = enemy.GetPosition();
                
                // La position physique est au centre de la capsule (à 0.5m du sol)
                Vector3 positionDessin = new Vector3(positionPhysique.X, positionPhysique.Y - 0.5f, positionPhysique.Z);
                
                // ==========================================
                // ORIENTATION : le zombie regarde là où son cerveau regarde (direction de
                // marche en errance, cible en poursuite) — plus de "stare" permanent.
                // ==========================================
                // 1. La direction du regard vient de l'IA (enemy.regard)
                Vector3 directionVersJoueur = enemy.regard;

                // 2. On calcule l'angle sur le plan horizontal (X et Z) en Radians
                float angleRadians = MathF.Atan2(directionVersJoueur.X, directionVersJoueur.Z);
                
                // 3. On convertit les Radians en Degrés (Raylib attend des degrés)
                float angleDegres = angleRadians * (180.0f / MathF.PI);
                
                // --- ASTUCE BLENDER ---
                // Si c'est le cas, décommente l'une de ces lignes pour le redresser :
                angleDegres += 180.0f; // S'il te tourne le dos
                // angleDegres += 90.0f;  // S'il marche en crabe
                
                // 4. On dessine avec DrawModelEx pour appliquer la rotation !
                Vector3 axeRotation = new Vector3(0, 1, 0); // On tourne autour de l'axe vertical Y
                Vector3 echelle = new Vector3(1.0f, 1.0f, 1.0f); // Taille normale
                
                Raylib.DrawModelEx(ennemiModel, positionDessin, axeRotation, angleDegres, echelle, Color.White);
                
                // Bonus Debug : Afficher la vraie hitbox BEPU
                if (debugInfo) Raylib.DrawBoundingBox(new BoundingBox(positionPhysique - new Vector3(0.5f,1f,0.5f), positionPhysique + new Vector3(0.5f,1f,0.5f)), Color.Red);
            }

            // Debug : la grille de navigation des zombies autour du joueur
            // (points verts = cases marchables, lignes jaunes = liens de saut)
            if (debugInfo) NavGrid.DessinerDebug(posCube, 20f);

            // Dessiner les barrils
            foreach (BarrelSpot spot in barrelSpots)
            {
                if (spot.hasBarrel)
                {
                    Raylib.DrawModel(barrelModel, spot.position, barrelScale, Color.White);
                }
            }

            // Dessiner les explosions en sphère orange avec un coeur rouge plus petit
            foreach (ExplosionEffect exp in activeExplosions)
            {
                float size = exp.GetSize();
                byte alpha = (byte)exp.GetAlpha();
                Color orangeColor = new Color((byte)255, (byte)165, (byte)0, alpha);
                Raylib.DrawSphere(exp.position, size, orangeColor);

                float redSize = size * 0.9f;
                Color redColor = new Color((byte)255, (byte)0, (byte)0, alpha);
                Raylib.DrawSphere(exp.position, redSize, redColor);
            }

            
            // Dessiner le laser (Code ILIAN)
            if (laserTimer > 0)
            {
                int alpha = (int)(laserTimer * 255);

                if (debugInfo)
                {
                    // MODE DÉBUG (F2) : Rouge pur
                    Color debugColor = new Color(255, 0, 0, alpha);
                    Raylib.DrawLine3D(laserStart, laserEnd, debugColor);
                    Raylib.DrawSphere(laserEnd, 0.2f, debugColor);
                }
                else
                {
                    // MODE NORMAL : esthétique Fumée et Étincelle
                    Color smokeColor = new Color(180, 180, 180, alpha); // Gris clair pour la fumée
                    Color sparkColor = new Color(255, 120, 0, alpha);   // Orange vif pour le flash
                    
                    //fumée
                    Raylib.DrawLine3D(laserStart, laserEnd, smokeColor);
                    
                    //Flash
                    if (laserTimer > 0.8f) 
                    {
                        // Grosse étincelle là où la balle touche
                        Raylib.DrawSphere(laserEnd, 0.3f, sparkColor);   
                    }
                } 
            }


            // APERÇU TRANSPARENT (À Mettre tout en bas du rendu 3D)
            if (modeConstruction)
            {
                float angleRadians = 2.0f * MathF.Acos(rotationPrevueMur.W);
                Vector3 axe = new Vector3(rotationPrevueMur.X, rotationPrevueMur.Y, rotationPrevueMur.Z);
                if (axe.LengthSquared() > 0.0001f) axe = Vector3.Normalize(axe); else axe = new Vector3(0, 1, 0);

                float angleDegresPreview = angleRadians * (180.0f / MathF.PI);
                Raylib.DrawModelEx(visuelMur, positionPrevueMur, axe, angleDegresPreview, Vector3.One, couleurMurTransparent);
                // Le contour rend la préview lisible malgré la forte transparence
                Color contourPreview = new Color(couleurMonSkin.R, couleurMonSkin.G, couleurMonSkin.B, (byte)200);
                Raylib.DrawModelWiresEx(visuelMur, positionPrevueMur, axe, angleDegresPreview, Vector3.One, contourPreview);
            }
            
        Raylib.EndMode3D();

        // ==========================================
        // PSEUDOS + VIE DES AUTRES JOUEURS (Multijoueur LAN)
        // ==========================================
        if (isOnline) DessinerNomsJoueursDistants(camera, CamFroward);

        // Chrono du match + objectif + toasts d'arrivée/départ
        if (isOnline) DessinerHudSession();

        // 1. LES BARRES DE VIE
        // 1. LES BARRES DE VIE (Affichage ciblé)
        foreach (Enemy enemy in enemiesList)
        {
            Vector3 headPos3D = enemy.GetPosition() + new Vector3(0, 2.0f, 0);
            Vector3 dirToEnemy = Vector3.Normalize(headPos3D - camera.Position);
            float precisionRegard = Vector3.Dot(dirToEnemy, CamFroward);

            if (precisionRegard > 0.95f)
            {
                Vector2 screenPos = Raylib.GetWorldToScreen(headPos3D, camera);
                
                float hpPercent = (float)enemy.health / 100f; 
                if (hpPercent < 0) hpPercent = 0;

                int barWidth = 80;
                int barHeight = 10;
                int posX = (int)screenPos.X - (barWidth / 2); 
                int posY = (int)screenPos.Y;

                Raylib.DrawRectangle(posX, posY, barWidth, barHeight, Color.Red);
                Raylib.DrawRectangle(posX, posY, (int)(barWidth * hpPercent), barHeight, Color.Green);
                Raylib.DrawRectangleLines(posX, posY, barWidth, barHeight, Color.Black);
            }
        }

        // 2. LES TEXTES DE DÉGÂTS VOLANTS
        for (int i = activeDamageTexts.Count - 1; i >= 0; i--)
        {
            DamageText dt = activeDamageTexts[i];
            
            // Le chronomètre tourne
            dt.timer -= deltaTime;
            if (dt.timer <= 0)
            {
                activeDamageTexts.RemoveAt(i);
                continue;
            }

            // LE MOUVEMENT : Le texte monte vers le ciel (axe Y)
            dt.position.Y += deltaTime * 1.5f;

            // Comme pour la jauge, on vérifie si le texte est devant nous
            Vector3 dirToText = dt.position - camera.Position;
            if (Vector3.Dot(dirToText, CamFroward) > 0)
            {
                Vector2 screenPos = Raylib.GetWorldToScreen(dt.position, camera);
                
                // LE FONDU : Plus le timer approche de 0, plus l'alpha devient transparent
                int alpha = (int)((dt.timer / dt.maxTimer) * 255);
                alpha = Math.Clamp(alpha, 0, 255);
                Color textColor = new Color(255, 200, 0, alpha); // Jaune/Orange pétant
                Color shadowColor = new Color(0, 0, 0, alpha);   // Ombre noire

                // On dessine le texte (avec son ombre)
                int textSize = 25;
                int textWidth = Raylib.MeasureText(dt.text, textSize);
                Raylib.DrawText(dt.text, (int)screenPos.X - textWidth/2 + 2, (int)screenPos.Y + 2, textSize, shadowColor);
                Raylib.DrawText(dt.text, (int)screenPos.X - textWidth/2, (int)screenPos.Y, textSize, textColor);
            }
        }



        
        
        // ========================================================
        // [ZONE ILIAN] 4. HUD ET ARMES 2D
        // ========================================================
        Camera3D weaponCamera = new Camera3D();
        weaponCamera.Position = new Vector3(0,0,0);
        weaponCamera.Target = new Vector3(0, 0, 1);
        weaponCamera.Up = new Vector3(0, 1, 0);
        weaponCamera.FovY = 45.0f;
        weaponCamera.Projection = CameraProjection.Perspective;

        
        bool hasAmmo = currentWeapon.ammo > 0;    
        Model actualWeapon = currentWeapon.modelname;
        
        // --- LOGIQUE DE CATÉGORIES D'ARMES ---

        //defini plus tot avec les parametre de caméra
        //bool isMelee = (currentWeapon == karambitknife || currentWeapon == bazookaWeapon);
        //bool isScopedWeapon = (currentWeapon == sniperrifle || currentWeapon == pistolWeapon); 
        
        //bool isAiming = Raylib.IsMouseButtonDown(MouseButton.Right) && hasWeapon && !isMelee;
        
        showweapon = true;

        if (Raylib.IsKeyDown(KeyBinds.DebugToggleWeapon)) showweapon = false;




        // --- GESTION DU ZOOM ET FOV DYNAMIQUE ---
        float targetFov = Settings.BaseFOV; // FOV de base depuis les paramètres [cite: 333]

        // 1. LA FOV DYNAMIQUE (Vitesse)
        // Si le joueur va plus vite que la marche normale (8m/s)
        if (vitesseHorizontale > 8f)
        {
            // On calcule l'excès de vitesse
            float excesVitesse = vitesseHorizontale - 8f;
            
            // On ajoute 1.2 degré de FOV pour chaque m/s de vitesse supplémentaire
            float bonusFov = excesVitesse; 
            
            // SÉCURITÉ : On bloque le bonus à +35° maximum pour éviter de casser l'image
            if (bonusFov > 35f) bonusFov = 35f; 
            
            targetFov += bonusFov;
        }

        // 2. L'ÉCRASEMENT PAR LA VISÉE (Aiming)
        // La visée doit avoir la priorité absolue sur la vitesse ! 
        // (Tu veux pouvoir sniper précisément même en glissant à 30km/h)
        if (isAiming)
        {
            if (isScopedWeapon)
            {
                targetFov = 20.0f; // Gros zoom [cite: 334]
                showweapon = false; // On cache l'arme 3D [cite: 335]
                Raylib.DrawTextureEx(sniperaim, new Vector2(0,0), 0, 1, Color.White); // Image lunette [cite: 336]
            }
            else
            {
                targetFov = 40.0f; // Petit zoom pour les armes normales centrées [cite: 337]
            }
        }

        // 3. L'INTERPOLATION FLUIDE
        // La caméra glisse doucement vers le targetFov (Lissage) 
        camera.FovY = camera.FovY + (targetFov - camera.FovY) * 15f * deltaTime;


        // --- DESSIN DE L'ARME 3D ---
        if (showweapon)
        {
            Raylib.BeginMode3D(weaponCamera);
                
                // 1. Calcul du balancement (Headbobbing de l'étape 3)
                float balancementY = 0f;
                float balancementX = 0f;
                if (capteurSol.toucheSol)
                {
                    balancementY = MathF.Sin((float)Raylib.GetTime() * 12f) * (vitesseHorizontale * 0.002f);
                    balancementX = MathF.Cos((float)Raylib.GetTime() * 6f) * (vitesseHorizontale * 0.001f);
                }
                else
                {
                    balancementY = -espionCube.Velocity.Linear.Y * 0.003f;
                    if (balancementY > 0.08f) balancementY = 0.08f;
                    if (balancementY < -0.08f) balancementY = -0.08f;
                }

                // 2. Position de base de l'arme (sur le côté)
                float posX = 0.05f;
                float posY = -0.04f;
                float posZ = 0.12f;

                // 3. SI ON VISE : On centre l'arme !
                if (isAiming && !isScopedWeapon)
                {
                    posX = 0.1f;   // Au centre horizontalement
                    posY = -0.04f; // On la remonte un peu vers les yeux
                    posZ = 0.10f;  // On la rapproche légèrement
                }

                // Application finale de la position
                Vector3 weaponPos = new Vector3(posX + balancementX, posY + balancementY, posZ); 
                Vector3 weaponScale = new Vector3(0.1f, 0.1f, 0.1f);
                
                // Combiner le recul avec l'inclinaison du rechargement
                float reloadRotation = currentWeapon.GetReloadRotationAngle();
                float totalRotation = recoilAngle + reloadRotation;

                // ==========================================
                // LA LUMIÈRE DYNAMIQUE DU VIEWMODEL
                // ==========================================
                // L'arme vit dans sa propre mini-scène (weaponCamera fixe), donc le soleil
                // "monde" ne bouge jamais par rapport à elle : elle était toujours éclairée pareil.
                // L'astuce : exprimer la direction du soleil DANS le repère de la caméra du joueur.
                // Quand on tourne sur soi-même, cette direction change, et l'arme réagit à la lumière !
                Vector3 dirSoleil = Vector3.Normalize(soleilPosition - camera.Position);
                Vector3 axeAvant = CamFroward;
                Vector3 axeHaut = camera.Up;
                Vector3 axeDroite = Vector3.Normalize(Vector3.Cross(axeHaut, axeAvant));
                // La weaponCamera regarde vers +Z avec up +Y : son repère est l'identité,
                // donc les composantes (droite, haut, avant) se réutilisent telles quelles.
                Vector3 soleilVueArme = new Vector3(
                    Vector3.Dot(dirSoleil, axeDroite),
                    Vector3.Dot(dirSoleil, axeHaut),
                    Vector3.Dot(dirSoleil, axeAvant)
                ) * 100f; // loin, pour un éclairage quasi directionnel

                Raylib.SetShaderValue(lightShader, lightPosLoc, soleilVueArme, ShaderUniformDataType.Vec3);
                Raylib.SetShaderValue(lightShader, viewPosLoc, weaponCamera.Position, ShaderUniformDataType.Vec3);

                Raylib.SetShaderValue(lightShader, Program.applyFogLoc, new int[] { 0 }, ShaderUniformDataType.Int);
                Raylib.DrawModelEx(actualWeapon, weaponPos, Vector3.UnitX, totalRotation, weaponScale, Color.White);
                Raylib.SetShaderValue(lightShader, Program.applyFogLoc, new int[] { 1 }, ShaderUniformDataType.Int);

                // On remet la lumière "monde" pour tout ce qui sera dessiné ensuite
                Raylib.SetShaderValue(lightShader, lightPosLoc, soleilPosition, ShaderUniformDataType.Vec3);
                Raylib.SetShaderValue(lightShader, viewPosLoc, camera.Position, ShaderUniformDataType.Vec3);

            Raylib.EndMode3D();
            
            // ==========================================
            // HUD MUNITIONS 
            // ==========================================
            Raylib.DrawCircle(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2, 3, Color.Green); // Réticule
            
            // Mutateur "Munitions" : le chargeur affiché est celui du match, et "INF"
            // remplace le total quand les munitions infinies sont activées.
            string ammoStr = currentWeapon.ammo.ToString();
            string maxAmmoStr = MatchRules.MunitionsInfinies ? "INF" : currentWeapon.MaxAmmoEffectif.ToString();

            int tailleGrandTexte = 70;
            int taillePetitTexte = 35;

            // Position globale du bloc de munitions (En bas à droite)
            posX = Raylib.GetScreenWidth() - 250;
            posY = Raylib.GetScreenHeight() - 120;

            //Le grand chiffre
            Raylib.DrawText(ammoStr, (int)posX + 3, (int)posY + 3, tailleGrandTexte, Color.Black);
            Raylib.DrawText(ammoStr, (int)posX, (int)posY, tailleGrandTexte, Color.White);

            //Petit
            int largeurGrandTexte = Raylib.MeasureText(ammoStr, tailleGrandTexte);
            
            int petitPosX = (int)posX + largeurGrandTexte + 10; // Décalé vers la droite
            int petitPosY = (int)posY + (tailleGrandTexte - taillePetitTexte) - 5; // Aligné vers le bas du grand chiffre
            
            // Ombre + Texte pour le petit chiffre (en gris clair)
            Raylib.DrawText(maxAmmoStr, petitPosX + 2, petitPosY + 2, taillePetitTexte, Color.Black);
            Raylib.DrawText(maxAmmoStr, petitPosX, petitPosY, taillePetitTexte, Color.LightGray);

        }
        

        // ========================================================
        // [ZONE ILIAN] 5. LOGIQUE DES TIRS
        // ========================================================
        // Si on a une arme et qu'on clique
        if (hasWeapon && KeyBinds.IsShootingHeld() && !modeConstruction && !isPaused)
        {
            Vector3 direction = CamFroward;
            
            // On dit à l'arme : "Essaye de tirer avec ces infos !"
            bool aTire = currentWeapon.Shoot(direction, camera, ref espionCube, barrelSpots, enemiesList, out Vector3 startL, out Vector3 endL);

            // Si l'arme a répondu "Oui, le tir est parti" : on gère le visuel
            if (aTire)
            {
                laserTimer = 1.0f;
                recoilAngle = -15.0f;
                laserStart = startL;
                laserEnd = endL;

                // ==========================================
                // MULTIJOUEUR : on annonce le tir aux autres
                // et on vérifie si on a touché un joueur !
                // ==========================================
                if (isOnline) TraiterTirLocalReseau(currentWeapon, camera.Position, direction, startL, endL);
            }
        }

        currentWeapon.Reload();





        if (debugInfo)
        {
            //infos 
            Raylib.DrawText("le moteur tourne.", 10,10,20, Color.DarkGreen);
            Raylib.DrawText($"Position XYZ : X={posCube.X:F2} Y={posCube.Y:F2} Z={posCube.Z:F2}", 10,40,20, Color.DarkGreen);
            // Les mutateurs actifs, pour savoir tout de suite avec quelles règles on joue
            Raylib.DrawText($"Mutateurs : {MatchRules.Resume(4)}", 10, HauteurFenetre - 30, 18,
                            MatchRules.ReglesModifiees() ? Color.Gold : Color.DarkGreen);
            Raylib.DrawText($"Hauteur du cube : {posCube.Y:F2}", 10,70,20,Color.DarkGreen);
            if (NbJump >NbJumpMax)
            {
                Raylib.DrawText("Jump allowed", 10,100,20,Color.DarkGreen);
            }
            else
            {
                Raylib.DrawText("Jump not allowed", 10,100,20,Color.Red);
            }

            //wall jump 
            if (capteurMurDroit.toucheMur)
            {
                Raylib.DrawText("saut droit", 10,130,20,Color.DarkGreen);
            }
            else
            {
                Raylib.DrawText("saut droit",10,130,20,Color.Red);
            }
            if (capteurMurGauche.toucheMur)
            {
                Raylib.DrawText("saut gauche", 10,160,20,Color.DarkGreen);
            }
            else
            {
                Raylib.DrawText("saut gauche",10,160,20,Color.Red);
            }

            //sprint 
            if (IsSprinting)
            {
                Raylib.DrawText("sprint", 10,190,20,Color.DarkGreen);
            }
            else
            {
                Raylib.DrawText("sprint",10,190,20,Color.Red);
            }

            if (IsWallRunning)
            {
                Raylib.DrawText("WallRun", 10,220,20,Color.DarkGreen);
            }
            else
            {
                Raylib.DrawText("WallRun",10,220,20,Color.Red);
            }

            Raylib.DrawText($"Vitesse horizontale: {vitesseHorizontale:F2}", 10,240,20,Color.DarkGreen);
            Raylib.DrawText($"Vitesse verticale : {vitesseVerticale:F2}", 10,270,20,Color.DarkGreen);
            
            if (capteurGlissade.toucheSol) Raylib.DrawText("touche sol glissade", 10,300,20,Color.DarkGreen);
            else Raylib.DrawText("touche sol glissade", 10,300,20,Color.Red);

            if (laserTimer > 0) Raylib.DrawText($"Laser actif: {laserTimer:F2}", 10, 380, 20, Color.Red);

            // Vitesse Debug BEPU
            Raylib.DrawText($"Vitesse horizontale: {vitesseHorizontale:F2}", 10, 340, 20, Color.DarkGreen);

            // LE HUD RÉSEAU (Phase 0) : ping, débits, âge du snapshot, tampons d'interpolation
            if (isOnline)
            {
                Raylib.DrawText(hudReseauLigne1, 10, 410, 20, Color.SkyBlue);
                Raylib.DrawText(hudReseauLigne2, 10, 440, 20, Color.SkyBlue);
            }
        }

        

        if (showweapon){
            Color chrossairColor = Color.Red;
            //wall run 
            if (capteurMurDroit.toucheMur)
            {
                Raylib.DrawRectangle(LargeurFenetre/2+50,HauteurFenetre/2-5,3,10,chrossairColor);
            }
            if (capteurMurGauche.toucheMur)
            {
                Raylib.DrawRectangle(LargeurFenetre/2-50,HauteurFenetre/2-5,3,10,chrossairColor);
            }

            //crosshair 

            int centreX = LargeurFenetre / 2;
            int centreY = HauteurFenetre / 2;

            Raylib.DrawCircle(centreX, centreY, 3f, chrossairColor);

            if (hitmarkerTimer > 0)
            {
                hitmarkerTimer -= deltaTime; 
                
                int alphaHit = (int)((hitmarkerTimer / 0.3f) * 255);
                alphaHit = Math.Clamp(alphaHit, 0, 255);
                Color hitColor = new Color(255, 255, 255, alphaHit); 

                int taille = 15;      // Longueur de la branche
                int trou = 7;         // L'espace vide au centre
                float epaisseur = 3f; // L'ÉPAISSEUR DU HITMARKER !

                // Haut-Gauche
                Raylib.DrawLineEx(new Vector2(centreX - trou, centreY - trou), new Vector2(centreX - taille, centreY - taille), epaisseur, hitColor);
                // Bas-Droite
                Raylib.DrawLineEx(new Vector2(centreX + trou, centreY + trou), new Vector2(centreX + taille, centreY + taille), epaisseur, hitColor);
                // Bas-Gauche
                Raylib.DrawLineEx(new Vector2(centreX - trou, centreY + trou), new Vector2(centreX - taille, centreY + taille), epaisseur, hitColor);
                // Haut-Droite
                Raylib.DrawLineEx(new Vector2(centreX + trou, centreY - trou), new Vector2(centreX + taille, centreY - taille), epaisseur, hitColor);
            }





            Color missingLife = new Color(255, 50, 50, 40);

            float lifePercentage = (float)localPlayer.Health / localPlayer.MaxHealth;

            int lifePixel = (int)(400 * lifePercentage);

            Raylib.DrawRectangle(100, HauteurFenetre - 150, 400, 80, missingLife); 
            
            // La barre de vie réelle (Blanche si > 25%, Rouge clignotant si <= 25%)
            if (lifePercentage > 0.25f)
            {
                Raylib.DrawRectangle(100, HauteurFenetre - 150, lifePixel, 80, Color.White);
            } 
            else if (localPlayer.IsAlive)
            {
                // Effet Urgence : Clignotement rouge si la vie est critique
                if (MathF.Sin((float)Raylib.GetTime() * 10f) > 0)
                    Raylib.DrawRectangle(100, HauteurFenetre - 150, lifePixel, 80, Color.Red);
                else
                    Raylib.DrawRectangle(100, HauteurFenetre - 150, lifePixel, 80, Color.DarkGray);
            }

            // Le texte des HP (Noir)
            Raylib.DrawText($"{localPlayer.Health}", 100 + 10, HauteurFenetre - 150 + 25, 50, Color.Black);

            Color missingDash = new Color(150, 150, 150, 50);
            Color dashColor = new Color(150, 255, 150, 255);
            int dashPixel = (int)(100*dashChrono/90);

            Raylib.DrawRectangle(100, HauteurFenetre - 200, 100,40,missingDash); //arriere plan pour si on enleve la vie on voit encore la barre
            if (CanDash && dashChrono >= 90)
            {
                Raylib.DrawRectangle(100, HauteurFenetre - 200, dashPixel,40,dashColor);
            } else
            {
                Raylib.DrawRectangle(100, HauteurFenetre - 200, dashPixel,40,Color.LightGray);
            }

            

            Color missingWall = new Color(150, 150, 150, 50);
            Color WallColor = new Color(255, 140, 60, 255);
            int WallPixel = Math.Min(100, 100 * wallChrono / WallChronoMax);

            Raylib.DrawRectangle(100, HauteurFenetre - 250, 100,40,missingWall); //arriere plan pour si on enleve la vie on voit encore la barre
            if (wallChrono >= WallChronoMax) // mutateur "Recharge du mur"
            {
                Raylib.DrawRectangle(100, HauteurFenetre - 250, WallPixel,40,WallColor);
            } else
            {
                Raylib.DrawRectangle(100, HauteurFenetre - 250, WallPixel,40,Color.LightGray);
            }
            




            
            // ==========================================
            // HUD : LE CHRONOMÈTRE DE SURVIE
            // ==========================================
            // On convertit les secondes en un texte propre (ex: "Survie : 45s")
            // Masqué en multijoueur : l'overlay survie n'a de sens qu'en solo/zombies.
            if (!isOnline)
            {
                string chronoTexte = $"SURVIE : {MathF.Floor(survivalTime)}s";
                int tailleChrono = 40;
                int largeurChrono = Raylib.MeasureText(chronoTexte, tailleChrono);

                int chronoX = (LargeurFenetre - largeurChrono) / 2; // Centré en haut
                int chronoY = 30;

                // Effet d'ombre pour que ce soit bien lisible
                Raylib.DrawText(chronoTexte, chronoX + 3, chronoY + 3, tailleChrono, Color.Black);
                Raylib.DrawText(chronoTexte, chronoX, chronoY, tailleChrono, Color.White);
            }



            
            // ==========================================
            // HUD KILL COUNT (Top Right)
            // ==========================================
            {
                int iconSize = 50;
                int textSize = 40;
                int padding = 25;

                
                // Position en haut à droite
                int iconX = Raylib.GetScreenWidth() - iconSize - padding;
                int iconY = padding*2;
                
                // Dessiner l'icône cible
                Raylib.DrawTextureEx(Program.cibleTexture, new Vector2(iconX, iconY), 0, (float)iconSize / Program.cibleTexture.Width, Color.White);
                
                // Texte du nombre de kills
                string killText = Program.killCount.ToString();
                int textWidth = Raylib.MeasureText(killText, textSize);
                
                // Position du texte à gauche de l'icône
                int textX = iconX - textWidth - padding;
                int textY = iconY + (iconSize - textSize) / 2; // Centré verticalement avec l'icône
                
                // Ombre + Texte
                Raylib.DrawText(killText, textX + 2, textY + 2, textSize, Color.Black);
                Raylib.DrawText(killText, textX, textY, textSize, Color.White);
            }





            
            Raylib.DrawFPS(LargeurFenetre-90,10);
        }



        // ==========================================
        // POST-PROCESSING BASIQUE (VIGNETTAGE)
        // ==========================================
        // On dessine de grands rectangles noirs semi-transparents sur les bords de l'écran
        Color ombreBord = new Color(0, 0, 0, 80); // Noir transparent
        
        // Bordure Haut
        Raylib.DrawRectangleGradientV(0, 0, LargeurFenetre, 100, ombreBord, Color.Blank);
        // Bordure Bas
        Raylib.DrawRectangleGradientV(0, HauteurFenetre - 100, LargeurFenetre, 100, Color.Blank, ombreBord);
        // Bordure Gauche
        Raylib.DrawRectangleGradientH(0, 0, 100, HauteurFenetre, ombreBord, Color.Blank);
        // Bordure Droite
        Raylib.DrawRectangleGradientH(LargeurFenetre - 100, 0, 100, HauteurFenetre, Color.Blank, ombreBord);

        // Overlay de dégâts rouge
        if (damageOverlayOpacity > 0)
        {
            Color damageColor = new Color(255, 0, 0, (int)(damageOverlayOpacity * 255));
            Raylib.DrawRectangle(0, 0, LargeurFenetre, HauteurFenetre, damageColor);
        }

        // Overlay du tutoriel (mécanique + touche), par-dessus le HUD
        DessinerOverlayTuto(deltaTime);
        
        // ==========================================
        // AFFICHAGE DU MENU PAUSE ET OPTIONS (PAR-DESSUS LE JEU)
        // ==========================================
        if (isPaused)
        {
            // Navigation clavier/manette + curseur auto pour le menu pause ET l'overlay Options.
            // (verticalOnly quand Options est ouvert : Gauche/Droite règlent les sliders.)
            MenuNav.Begin(90000 + (isOptionsMenuOpen ? 1000 + ongletOptionActif : 0), isOptionsMenuOpen);

            // Si le joueur a cliqué sur Options, on affiche le calque des paramètres
            if (isOptionsMenuOpen)
            {
                string dummy = "";
                AfficherMenuOptions(ref dummy);

            }
            // Sinon, on affiche le menu pause normal (Online ou Offline)
            else if (isOnline)
            {
                MenugameOnline();
            }
            else
            {
                MenugameOffline();
            }

            MenuNav.End();
        }


        Raylib.EndDrawing();
    }
}