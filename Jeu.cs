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
    static Weapon sniperrifle = new Weapon("Sniper", 100, 500, 1.0f, 5, 3, sniper, snipershot, 0f);
    static Weapon karambitknife = new Weapon("Karambit", 75, 10, 0.4f, 1, 0, karambit, karambitshot, 0f);
    static Weapon bazookaWeapon = new Weapon("Bazooka", 100, 200, 3.0f, 1, 4, bazooka, bazookashot, 15.0f);
    static Weapon shotgunWeapon = new Weapon("Shotgun", 90, 10, 2f, 5, 2, shotgun, shotgunshot, 7.0f);
    static Weapon pistolWeapon = new Weapon("Pistol", 20, 90, 0.3f, 12, 2, pistol, pistolshot, 0f);
    static Weapon revolverWeapon = new Weapon("Revolver", 34, 10, 0.8f, 6, 2, revolver, revolvershot, 15.0f);
    static Weapon swordWeapon = new Weapon("Sword", 20, 100, 0.3f, 15, 4, sword, swordslash, 4f);

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

    public class BarrelSpot
    {
        public Vector3 position;
        public bool hasBarrel;
        public float respawnTimer;
        public bool respawnPending;

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

    static void InitBarrels()
    {
        // Active aléatoirement 10 emplacements sur 15
        List<int> indices = Enumerable.Range(0, barrelSpots.Count).OrderBy(i => random.Next()).Take(initialBarrelCount).ToList();
        for (int i = 0; i < barrelSpots.Count; i++)
        {
            barrelSpots[i].hasBarrel = indices.Contains(i);
            barrelSpots[i].respawnPending = false;
            barrelSpots[i].respawnTimer = 0f;
        }
    }

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

                    List<int> freeSlots = barrelSpots
                        .Select((b, index) => new { b, index })
                        .Where(x => !x.b.hasBarrel && !x.b.respawnPending)
                        .Select(x => x.index)
                        .ToList();

                    if (freeSlots.Count > 0)
                    {
                        int targetIndex = freeSlots[random.Next(freeSlots.Count)];
                        barrelSpots[targetIndex].hasBarrel = true;
                    }
                }
                barrelSpots[i] = spot;
            }
        }
    }

    static void OnBarrelHit(int index)
    {
        if (index < 0 || index >= barrelSpots.Count) return;
        Vector3 barrelPos = barrelSpots[index].position;
        barrelSpots[index].hasBarrel = false;
        barrelSpots[index].respawnPending = true;
        barrelSpots[index].respawnTimer = barrelRespawnSeconds;
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

    static bool IsPointOnLaser(Vector3 point, Vector3 laserStart, Vector3 direction, float range)
    {
        Vector3 toPoint = point - laserStart;
        float distance = toPoint.Length();
        if (distance > range) return false;
        Vector3 toPointNorm = Vector3.Normalize(toPoint);
        float dot = Vector3.Dot(direction, toPointNorm);
        return dot > 0.995f;
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

        if (recoilAngle < 0) recoilAngle += deltaTime * 30.0f;
        if (recoilAngle > 0) recoilAngle = 0;


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

        if (espionCube.Pose.Position.Y < -50f)
        {
            // 1. On modifie les coordonnées instantanément (ex: on le remet à 10m de haut au centre)
                espionCube.Pose.Position = new Vector3(0, 10f, 0); 
                
                // 2. On remet l'inertie et la vitesse à zéro pour un arrêt net !
                espionCube.Velocity.Linear = Vector3.Zero;  // Stop le déplacement
                espionCube.Velocity.Angular = Vector3.Zero; // Stop la rotation sur lui-même

        }






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
            espionCube.Velocity.Linear += GroundForward * 30;
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





        // ========================================================
        // [ZONE MIXTE] 3. RENDU GRAPHIQUE (RAYLIB)
        // ========================================================
        Raylib.SetShaderValue(lightShader, lightPosLoc, lightPosition, ShaderUniformDataType.Vec3);

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.SkyBlue);

        Raylib.BeginMode3D(camera);

            // REMET CETTE LIGNE :
            Raylib.DrawModel(mapModel, mapPosition, mapScale, Color.White);
            if (debugInfo) Raylib.DrawModelWires(mapModel, mapPosition, mapScale, Color.Black);
            
            /*
            //sol 
            float taillePlatforme = 200f;
            Raylib.DrawCube(new Vector3(0,-taillePlatforme/2,0), taillePlatforme, taillePlatforme, taillePlatforme, Color.Gray);
            Raylib.DrawGrid((int)taillePlatforme, 1f); 

            
            //mur 
            Vector3 PosMur = new Vector3(-9.5f,2.5f,0);
            Vector3 PosMur2 = new Vector3(-9.5f,20f,0);
            Raylib.DrawCube(PosMur,1f,5f,20f,Color.Gray);
            Raylib.DrawCubeWires(PosMur,1f,5f,20f,Color.White);

            Raylib.DrawCube(PosMur2,1f,5f,20f,Color.Gray);
            Raylib.DrawCubeWires(PosMur2,1f,5f,20f,Color.White);

            Raylib.DrawCube(new Vector3(10f,-0.3f,0), 2f, 1f, 2f, Color.Blue); // Jump Pad


            Vector3 PosPlatforme1 = new Vector3(25,4,0);
            Vector3 PosPlatforme2 = new Vector3(22,10,-15);
            Vector3 PosPlatforme3 = new Vector3(10,18,-20);
            //Box Platforme = new Box(10f, 1f , 10f)
            Raylib.DrawCube(PosPlatforme1,10f, 1f , 10f,Color.Gray);
            Raylib.DrawCubeWires(PosPlatforme1,10f, 1f , 10f,Color.White);

            Raylib.DrawCube(PosPlatforme2,10f, 1f , 10f,Color.Gray);
            Raylib.DrawCubeWires(PosPlatforme2,10f, 1f , 10f,Color.White);

            Raylib.DrawCube(PosPlatforme3,10f, 1f , 10f,Color.Gray);
            Raylib.DrawCubeWires(PosPlatforme3,10f, 1f , 10f,Color.White);
            */


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




            // Dessiner les ennemis (Code ILIAN)
            foreach (Enemy enemy in enemies)
            {
                Raylib.DrawBoundingBox(enemy.GetBoundingBox(), Color.Blue);
                Raylib.DrawModel(enemyModel, enemy.position, 1.0f, Color.White);
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
                byte alpha = (byte)(laserTimer * 255);
                Color laserColor = new Color((byte)255, (byte)0, (byte)0, alpha);
                Raylib.DrawLine3D(laserStart, laserEnd, laserColor);
                Raylib.DrawSphere(laserEnd, 0.2f, laserColor);
            }
            
        Raylib.EndMode3D();

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
        bool isAiming = Raylib.IsMouseButtonDown(MouseButton.Right) && hasWeapon && (currentWeapon == sniperrifle || currentWeapon == pistolWeapon);
        bool showweapon = !isAiming;
        Model actualWeapon = currentWeapon.modelname;
        Sound actualSound = currentWeapon.soundname;
        Vector2 positionViseurSniper = new Vector2(0,0);

        if (isAiming && (currentWeapon == sniperrifle || currentWeapon == pistolWeapon))
        {
            Raylib.DrawTextureEx(sniperaim, positionViseurSniper, 0, 1, Color.White);
            camera.FovY = 20.0f;
        }
        else camera.FovY = 60.0f;

        if (hasAmmo && Raylib.IsMouseButtonDown(MouseButton.Left) && ((float)Raylib.GetTime() - currentWeapon.lastShotTime >= currentWeapon.fireRate))
        {   
            Vector3 direction = CamFroward;
            float forceRecul = 1.0f;
            espionCube.Velocity.Linear -= direction * forceRecul * currentWeapon.force;
        }

        if (showweapon)
        {
            Raylib.BeginMode3D(weaponCamera);
                Vector3 weaponPos = new Vector3(0.5f, -0.4f, 1.2f);
                Raylib.DrawModelEx(actualWeapon, weaponPos, Vector3.UnitX, recoilAngle, Vector3.One, Color.White);
            Raylib.EndMode3D();
            Raylib.DrawCircle(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2, 3, Color.Green);
            
            string texteMunitions = $"Munitions: {currentWeapon.ammo}/{currentWeapon.maxammo}";
            int posX = Raylib.GetScreenWidth() - 400;
            int posY = Raylib.GetScreenHeight() - 200;
            Raylib.DrawText(texteMunitions, posX, posY, 30, Color.Black);
        }

        // ========================================================
        // [ZONE ILIAN] 5. LOGIQUE DES TIRS
        // ========================================================
        if (hasWeapon && hasAmmo && !currentWeapon.isReloading && Raylib.IsMouseButtonDown(MouseButton.Left) && ((float)Raylib.GetTime() - currentWeapon.lastShotTime >= currentWeapon.fireRate))
        {
            Raylib.PlaySound(actualSound);
            laserTimer = 1.0f;
            recoilAngle = -15.0f;
            currentWeapon.lastShotTime = (float)Raylib.GetTime();
            currentWeapon.ammo--;
            
            // LA SEULE MODIFICATION POUR LE ILIAN : On utilise le regard BEPU (CamFroward)
            Vector3 direction = CamFroward; 
            Vector3 right = Vector3.Normalize(Vector3.Cross(direction, camera.Up));
            laserStart = camera.Position + direction * 0.5f + right * 0.25f;
            laserEnd = laserStart + direction * currentWeapon.range;

            int hitBarrelIndex = -1;
            for (int i = 0; i < barrelSpots.Count; i++)
            {
                BarrelSpot spot = barrelSpots[i];
                if (!spot.hasBarrel) continue;
                if (IsPointOnLaser(spot.position, laserStart, direction, currentWeapon.range))
                {
                    hitBarrelIndex = i;
                    break;
                }
            }

            if (hitBarrelIndex >= 0)
            {
                OnBarrelHit(hitBarrelIndex);
            }
            else
            {
                foreach (Enemy enemy in enemies.ToList())
                {
                    float distanceToEnemy = Vector3.Distance(laserStart, enemy.position);
                    if (distanceToEnemy <= currentWeapon.range)
                    {
                        Vector3 toEnemy = Vector3.Normalize(enemy.position - laserStart);
                        float dot = Vector3.Dot(direction, toEnemy);
                        if (dot > 0.995) // Hitbox précise
                        {
                            enemy.health -= currentWeapon.damage;
                            if (enemy.health <= 0) enemies.Remove(enemy);
                        }
                    }
                }
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
        Raylib.DrawCircle(LargeurFenetre/2,HauteurFenetre/2,3f,chrossairColor);



        Color missingLife = new Color(255, 50, 50, 40);
        float life = 80;
        int lifePixel = (int)(400*life/100);
        Raylib.DrawRectangle(100, HauteurFenetre - 150, 400,80,missingLife); //arriere plan pour si on enleve la vie on voit encore la barre
        if (life > 20)
        {
            Raylib.DrawRectangle(100, HauteurFenetre - 150, lifePixel,80,Color.White);
        } else
        {
            Raylib.DrawRectangle(100, HauteurFenetre - 150, lifePixel,80,Color.Red);
        }

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
        
        


        Raylib.DrawText($"{life}",100+10, HauteurFenetre - 150 + 25,50,Color.Black);
        

        
        Raylib.DrawFPS(LargeurFenetre-90,10);
        Raylib.EndDrawing();
    }
}