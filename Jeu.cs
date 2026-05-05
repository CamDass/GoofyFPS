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
    static Weapon karambitknife = new Weapon("Karambit", 75, 3, 0.4f, 1, 0, karambit, karambitshot, 0f);
    static Weapon bazookaWeapon = new Weapon("Bazooka", 100, 200, 3.0f, 1, 4, bazooka, bazookashot, 15.0f);
    static Weapon shotgunWeapon = new Weapon("Shotgun", 90, 10, 2f, 5, 2, shotgun, shotgunshot, 7.0f);
    static Weapon pistolWeapon = new Weapon("Pistol", 20, 500, 0.3f, 12, 2, pistol, pistolshot, 0f);
    static Weapon revolverWeapon = new Weapon("Revolver", 35, 100, 0.8f, 6, 2, revolver, revolvershot, 15.0f);
    static Weapon swordWeapon = new Weapon("Sword", 8, 100, 0.15f, 30, 4, sword, swordslash, 4f);

    static List<Weapon> weapons = new List<Weapon> { sniperrifle, karambitknife, bazookaWeapon, shotgunWeapon, pistolWeapon, revolverWeapon, swordWeapon };
    static Weapon currentWeapon = revolverWeapon;
    static float laserTimer = 0.0f;
    static Vector3 laserStart = new Vector3();
    static Vector3 laserEnd = new Vector3();
    static float recoilAngle = 0.0f;

    static Random random = new Random();
    static float barrelRespawnSeconds = 30.0f;
    static float barrelScale = 1.0f;
    static int initialBarrelCount = 10;

    static bool showweapon = true;

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

    static List<BarrelSpot> barrelSpots = new List<BarrelSpot>
    {
        // Remplace ces coordonnées par tes propres positions XYZ
        new BarrelSpot(new Vector3(-57f, -0.55f, 31.92f)),
        new BarrelSpot(new Vector3(-49f, 42.0f, 101f)),
        new BarrelSpot(new Vector3(-53f, 7.5f, 130f)),
        new BarrelSpot(new Vector3(-22f, -0.55f, 128f)),
        new BarrelSpot(new Vector3(3f,-0.55f, 103f)),
        new BarrelSpot(new Vector3(3f, 4.5f, 54f)),
        new BarrelSpot(new Vector3(-15f, -0.55f, 22f)),
        new BarrelSpot(new Vector3(-76.5f, 8f, 10.5f)),
        new BarrelSpot(new Vector3(-81.5f, 5f, 42f)),
        new BarrelSpot(new Vector3(-110f, -0.55f, 50.5f)),
        new BarrelSpot(new Vector3(-72f, 9.0f, 91f)),
        new BarrelSpot(new Vector3(-76f, -0.55f, 114f)),
        new BarrelSpot(new Vector3(-46f, -0.55f, 52f)),
        new BarrelSpot(new Vector3(-35f, 3.7f, 87f)),
        new BarrelSpot(new Vector3(-18f, -0.55f, 71f)),
    };

    static void SwitchWeaponFromBarrel()
    {
        if (weapons.Count <= 1) return;
        Weapon newWeapon = currentWeapon;
        while (newWeapon == currentWeapon)
        {
            newWeapon = weapons[random.Next(weapons.Count)];
        }
        currentWeapon = newWeapon;
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
                barrelSpots[i].handlePhysique = simulation.Statics.Add(new StaticDescription(barrelSpots[i].position + new Vector3(0, 0.5f, 0), formeBarilIndex));
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
                        barrelSpots[targetIndex].handlePhysique = simulation.Statics.Add(new StaticDescription(barrelSpots[targetIndex].position + new Vector3(0, 0.5f, 0), formeBarilIndex));
                        barrelSpots[targetIndex].estSolide = true;
                    }
                }
                barrelSpots[i] = spot;
            }
        }
    }

    public static void OnBarrelHit(int index)
    {
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

        SwitchWeaponFromBarrel();
        Raylib.PlaySound(explosion);
        activeExplosions.Add(new ExplosionEffect(barrelPos, 0.5f, barrelScale * 0.8f, barrelScale * 3.5f));
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
            return initialSize + (maxSize - initialSize) * progress;
        }

        public float GetAlpha()
        {
            return (timer / duration) * 255f;
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

        // 2. On définit tes points de Spawn (Coordonnées à adapter selon ta map !)
        enemySpawnPoints.Clear();
        enemySpawnPoints.Add(new Vector3(2f, 2f, 2f));
        enemySpawnPoints.Add(new Vector3(-27f, 2f, 64f));
        enemySpawnPoints.Add(new Vector3(-100f, 2f, 99f));
        enemySpawnPoints.Add(new Vector3(-104f, 2f, 149f));
        enemySpawnPoints.Add(new Vector3(-28f, 2f, 127f));
        enemySpawnPoints.Add(new Vector3(4f, 2f, 83f));
        enemySpawnPoints.Add(new Vector3(-80f, 2f, 27f));
        enemySpawnPoints.Add(new Vector3(-63f, 2f, 3f));


        // 3. On réinitialise les chronos
        survivalTime = 0f;
        enemySpawnTimer = 10; //premier chrono plus long de 10s

        // 4. On fait apparaître 2 ennemis de base
        enemiesList.Add(new Enemy(enemySpawnPoints[0], 100, 4.0f));
        enemiesList.Add(new Enemy(enemySpawnPoints[1], 100, 4.0f));
    }









    static bool debugInfo = false;

    //===== BOUCLE DU JEU =====
    public static void BouclePrincipale()
    {
        
        // ========================================================
        // [ZONE ILIAN] 1. GESTION DES MENUS ET TIMERS
        // ========================================================
        if (Raylib.IsKeyPressed(KeyboardKey.LeftAlt))
        {
            isMenuGameOpen = !isMenuGameOpen; 
            if (isMenuGameOpen) { Raylib.EnableCursor(); Raylib.PlaySound(unselect); }
            else { Raylib.DisableCursor(); Raylib.PlaySound(select); }
        }

        if (isMenuGameOpen) { Menugame(); return; }

        // Le changement d'arme se fait désormais uniquement via les barrils touchés

        float deltaTime = Raylib.GetFrameTime(); 
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



        enemiesList.RemoveAll(e => !e.isAlive);

        // ACTIVATION DES CERVEAUX (IA)
        foreach (Enemy enemy in enemiesList)
        {
            // On leur donne la position de ton Cube Espion pour qu'ils te poursuivent !
            enemy.Maj(posCube);
        }



        GroundSensor capteurGlissade = new GroundSensor(espionCube.CollidableReference);
        float longueurLaserGlissade = 1.2f;
        simulation.RayCast(posCube, directionLaser, longueurLaserGlissade, ref capteurGlissade);

        if (capteurSol.toucheSol) {
            NbJump = NbJumpMax + 1;
            CanDash = true;
        }

        //90 frame = 1.5s 
        if (dashChrono < 90)dashChrono ++;
        


        if (Raylib.IsKeyDown(KeyboardKey.Tab))
        {
            espionCube.Pose.Position = new Vector3(0,50,0);
            NbJump = NbJumpMax + 1;

            Raylib.EnableCursor();
            Raylib.PlaySound(unselect);
            endroit = "menu";
        }

        // LE CERVEAU DE LA CAMÉRA FPS
        Vector2 mouseDelta = Raylib.GetMouseDelta();
        CameraYaw -= mouseDelta.X * MouseSensi;
        CameraPitch -= mouseDelta.Y * MouseSensi;

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
        float longueurLaserMur = 0.8f;
        simulation.RayCast(posCube, GroundRight, longueurLaserMur, ref capteurMurDroit);
        simulation.RayCast(posCube, -GroundRight, longueurLaserMur, ref capteurMurGauche);



        //vide
        if (Raylib.IsKeyPressed(KeyboardKey.P))
            {
                // 1. On modifie les coordonnées instantanément (ex: on le remet à 10m de haut au centre)
                espionCube.Pose.Position = new Vector3(0, 10f, 0); 
                
                // 2. On remet l'inertie et la vitesse à zéro pour un arrêt net !
                espionCube.Velocity.Linear = Vector3.Zero;  // Stop le déplacement
                espionCube.Velocity.Angular = Vector3.Zero; // Stop la rotation sur lui-même

            }
        
        if (Raylib.IsKeyPressed(KeyboardKey.K)) localPlayer.TakeDamage(15);
        if (Raylib.IsKeyPressed(KeyboardKey.H)) localPlayer.Heal(25);
        
        if (espionCube.Pose.Position.Y < -30f) localPlayer.TakeDamage(1);

        if (espionCube.Pose.Position.Y < -50f)
        {
            // 1. On modifie les coordonnées instantanément (ex: on le remet à 10m de haut au centre)
                espionCube.Pose.Position = new Vector3(0, 10f, 0); 
                
                // 2. On remet l'inertie et la vitesse à zéro pour un arrêt net !
                espionCube.Velocity.Linear = Vector3.Zero;  // Stop le déplacement
                espionCube.Velocity.Angular = Vector3.Zero; // Stop la rotation sur lui-même

        }


        //print coordonés 
        if (Raylib.IsKeyPressed(KeyboardKey.RightShift)) Console.WriteLine($"{espionCube.Pose.Position}");



        // Saut
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            if (NbJump > NbJumpMax)
            {
                NbJump--;
                if (espionCube.Velocity.Linear.Y > 0) { espionCube.Velocity.Linear.Y += 5f; } else { espionCube.Velocity.Linear.Y = 5f; }
            }
        }

        // Déplacements & WallRun
        if (!capteurSol.toucheSol)
        {
            if ((capteurMurDroit.toucheMur && Raylib.IsKeyDown(KeyboardKey.D)) || (capteurMurGauche.toucheMur && Raylib.IsKeyDown(KeyboardKey.A))) IsWallRunning = true; 
        }

        if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) deplacementVoulu += GroundForward;
        if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) deplacementVoulu -= GroundForward;
        if ((Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) && !capteurMurGauche.toucheMur) deplacementVoulu -= GroundRight; 
        if ((Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) && !capteurMurDroit.toucheMur) deplacementVoulu += GroundRight;

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

        bool IsSprinting = Raylib.IsKeyDown(KeyboardKey.LeftShift);
        float SpeedCoef = IsSprinting ? 1.7f : 1f;
        float vMax = 8f; 
        float fAcceleration = 0.2f; 
        float rollActuel = 0f;

        if (IsWallRunning)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Space))
            {
                espionCube.Velocity.Linear.Y += 2f;
                if (capteurMurDroit.toucheMur) deplacementVoulu -= GroundRight*20;
                if (capteurMurGauche.toucheMur) deplacementVoulu += GroundRight*20;
                Console.WriteLine("wall jump");
            }

            NbJump = NbJumpMax + 1;
            CanDash = true;

            fAcceleration = 0.1f;
            if (espionCube.Velocity.Linear.Y < 0) espionCube.Velocity.Linear.Y = -1f;
            if (Raylib.IsKeyDown(KeyboardKey.A)) rollActuel = 0.25f;
            else if (Raylib.IsKeyDown(KeyboardKey.D)) rollActuel = -0.25f;
        } 
        else { rollActuel = 0f; fAcceleration = 0.2f; }

        // Accroupir & Glissade
        float vitesseHorizontale = new Vector2(espionCube.Velocity.Linear.X, espionCube.Velocity.Linear.Z).Length();
        float vitesseVerticale = MathF.Abs(espionCube.Velocity.Linear.Y);
        float vitesseDescente = 0f;

        if (Raylib.IsKeyDown(KeyboardKey.C) && !Raylib.IsKeyDown(KeyboardKey.Space))
        {
            if (!capteurGlissade.toucheSol) 
            {
                vitesseDescente--;
                if (vitesseDescente < -20) vitesseDescente = -20; 
                espionCube.Velocity.Linear.Y += vitesseDescente;
            } 
            else 
            {
                hauteurVoulue = 0.5f;
                if (vitesseVerticale > 0) { espionCube.Velocity.Linear += GroundForward * (vitesseVerticale / 2); espionCube.Velocity.Linear.Y = 0.1f; }
                espionCube.SetShape(PlayerTicketAccroupi);
                if (vitesseHorizontale > 4) fAcceleration = 0.01f; else vMax = 3f;
            }
            if (Raylib.IsKeyPressed(KeyboardKey.C) && !Raylib.IsKeyDown(KeyboardKey.Space) && capteurSol.toucheSol && vitesseHorizontale > 4) espionCube.Velocity.Linear += GroundForward * 5;
        } 
        else 
        {
            espionCube.SetShape(PlayerTicket);
            hauteurVoulue = 0.8f; vMax = 8f; fAcceleration = 0.2f; vitesseDescente = 0;
        }

        Vector3 targetVelocity = deplacementVoulu * vMax * SpeedCoef;
        espionCube.Velocity.Linear.X += (targetVelocity.X - espionCube.Velocity.Linear.X) * fAcceleration;        
        espionCube.Velocity.Linear.Z += (targetVelocity.Z - espionCube.Velocity.Linear.Z) * fAcceleration;

        if (capteurSol.toucheSol && !Raylib.IsKeyDown(KeyboardKey.Space))
        {
            if (targetVelocity.Y > 0) 
            {
                // On aide activement la capsule à monter la pente en appliquant la vitesse Y voulue !
                espionCube.Velocity.Linear.Y = targetVelocity.Y;
            }
        }

        // Dash
        if (Raylib.IsKeyPressed(KeyboardKey.LeftControl) && CanDash && dashChrono >= 90){
            //Raylib.PlaySound(swoosh);
            Vector3 directionDash = GroundForward; 

            if (deplacementVoulu.LengthSquared() > 0)
            {
                directionDash = deplacementVoulu; 
            }
            espionCube.Velocity.Linear += directionDash * 30;

            CanDash = false ;
            dashChrono = 0;
        }

        // Jump pad (Sandbox)
        //if (posCube.X > 9 && posCube.X < 11 && posCube.Z > -1 && posCube.Z < 1 && capteurSol.toucheSol) espionCube.Velocity.Linear.Y += 20f;

        // On fait passer le temps
        simulation.Timestep(1f / 60f);

        // Application finale de la Caméra
        camera.Position = new Vector3(posCube.X, posCube.Y + hauteurVoulue, posCube.Z);
        camera.Up = Vector3.Transform(new Vector3(0, 1f, 0), Matrix4x4.CreateFromAxisAngle(CamFroward, rollActuel));
        camera.Target = camera.Position + CamFroward;



        if (Raylib.IsKeyPressed(KeyboardKey.F2)){
            if (debugInfo)
            {
                debugInfo = false;
            } else
            {
                debugInfo = true;
            }
        }

        


        // === SYSTÈME DE SURVIE : LE TEMPS PASSE ===
        if (localPlayer.IsAlive) // Le chrono tourne seulement si on est en vie
        {
            survivalTime += deltaTime;
            enemySpawnTimer -= deltaTime;

            // Si le temps est écoulé, et qu'il n'y a pas déjà trop de zombies (ex: limite de 30)
            if (enemySpawnTimer <= 0f && enemiesList.Count < 70)
            {
                // On choisit un point de spawn au hasard
                int randomSpawnIndex = random.Next(enemySpawnPoints.Count);
                
                // On crée le zombie
                enemiesList.Add(new Enemy(enemySpawnPoints[randomSpawnIndex], 100, 4.0f));
                
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
        
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.SkyBlue);
        Color couleurZenith = new Color(20, 25, 45, 255);   // Bleu très sombre en haut
        Color couleurHorizon = new Color(120, 60, 50, 255); // Orange/Rouge sale à l'horizon
        
        Raylib.DrawRectangleGradientV(0, 0, LargeurFenetre, HauteurFenetre, couleurZenith, couleurHorizon);

        Raylib.BeginMode3D(camera);

            // REMET CETTE LIGNE :
            Raylib.DrawModel(mapModel, mapPosition, mapScale, Color.White);
            if (debugInfo) Raylib.DrawModelWires(mapModel, mapPosition, mapScale, Color.Black);
            


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

            //dessiner le model du joueur
            Vector3 PointHaut = new Vector3(posCube.X, posCube.Y + 0.5f, posCube.Z);
            Vector3 PointBas = new Vector3(posCube.X, posCube.Y - 0.5f, posCube.Z);
            Raylib.DrawCapsule(PointHaut,PointBas,0.5f,8,8,Color.White); 




            // Dessiner les ennemis (Nouvelle version Physique)
            // Dessiner les ennemis (Nouvelle version Physique et LookAt)
            foreach (Enemy enemy in enemiesList)
            {
                Vector3 positionPhysique = enemy.GetPosition();
                
                // La position physique est au centre de la capsule (à 0.5m du sol)
                Vector3 positionDessin = new Vector3(positionPhysique.X, positionPhysique.Y - 0.5f, positionPhysique.Z);
                
                // ==========================================
                // NOUVEAU : FAIRE TOURNER LE MODÈLE VERS LE JOUEUR
                // ==========================================
                // 1. On calcule la direction entre l'ennemi et le joueur
                Vector3 directionVersJoueur = posCube - positionDessin;
                
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

            // Dessiner les barrils
            foreach (BarrelSpot spot in barrelSpots)
            {
                if (spot.hasBarrel)
                {
                    Raylib.DrawModel(barrelModel, spot.position, barrelScale, Color.White);
                }
            }

            // Dessiner les explosions
            foreach (ExplosionEffect exp in activeExplosions)
            {
                float size = exp.GetSize();
                byte alpha = (byte)exp.GetAlpha();
                Color expColor = new Color((byte)255, (byte)255, (byte)255, alpha);
                Raylib.DrawBillboard(camera, imageexplosion, exp.position, size, expColor);
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
            
        Raylib.EndMode3D();

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

        bool hasWeapon = true;   
        bool hasAmmo = currentWeapon.ammo > 0;    
        Model actualWeapon = currentWeapon.modelname;
        
        // --- LOGIQUE DE CATÉGORIES D'ARMES ---
        bool isMelee = (currentWeapon == karambitknife || currentWeapon == bazookaWeapon);
        bool isScopedWeapon = (currentWeapon == sniperrifle || currentWeapon == pistolWeapon); 
        
        bool isAiming = Raylib.IsMouseButtonDown(MouseButton.Right) && hasWeapon && !isMelee;
        
        showweapon = true;

        if (Raylib.IsKeyDown(KeyboardKey.F3)) showweapon = false;

        


        // --- GESTION DU ZOOM (FOV) ---
        float targetFov = 60.0f; // FOV de base

        if (isAiming)
        {
            if (isScopedWeapon)
            {
                targetFov = 20.0f; // Gros zoom
                showweapon = false; // On cache l'arme 3D
                Raylib.DrawTextureEx(sniperaim, new Vector2(0,0), 0, 1, Color.White); // Image lunette
            }
            else
            {
                
                targetFov = 40.0f; // Petit zoom pour les armes normales centrées
            }
        }

        // Interpolation fluide du zoom de la caméra principale !
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
                
                Raylib.DrawModelEx(actualWeapon, weaponPos, Vector3.UnitX, recoilAngle, weaponScale, Color.White);

                

            Raylib.EndMode3D();
            
            // ==========================================
            // HUD MUNITIONS 
            // ==========================================
            Raylib.DrawCircle(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2, 3, Color.Green); // Réticule
            
            string ammoStr = currentWeapon.ammo.ToString();
            string maxAmmoStr = currentWeapon.maxammo.ToString();

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
        if (hasWeapon && Raylib.IsMouseButtonDown(MouseButton.Left))
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
            }
        }

        currentWeapon.Reload();





        if (debugInfo)
        {
            //infos 
            Raylib.DrawText("le moteur tourne.", 10,10,20, Color.DarkGreen);
            Raylib.DrawText($"Position XYZ : X={posCube.X:F2} Y={posCube.Y:F2} Z={posCube.Z:F2}", 10,40,20, Color.DarkGreen);
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
        }

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
        
        // ==========================================
        // HUD : LE CHRONOMÈTRE DE SURVIE
        // ==========================================
        // On convertit les secondes en un texte propre (ex: "Survie : 45s")
        string chronoTexte = $"SURVIE : {MathF.Floor(survivalTime)}s";
        int tailleChrono = 40;
        int largeurChrono = Raylib.MeasureText(chronoTexte, tailleChrono);
        
        int chronoX = (LargeurFenetre - largeurChrono) / 2; // Centré en haut
        int chronoY = 30;

        // Effet d'ombre pour que ce soit bien lisible
        Raylib.DrawText(chronoTexte, chronoX + 3, chronoY + 3, tailleChrono, Color.Black);
        Raylib.DrawText(chronoTexte, chronoX, chronoY, tailleChrono, Color.White);

        
        Raylib.DrawFPS(LargeurFenetre-90,10);

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



        Raylib.EndDrawing();
    }
}