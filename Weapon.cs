using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;

using BepuPhysics; // physique
using System.Linq; // listes
using BepuPhysics.Collidables;

public class Weapon
{
    // parametres
    public string name;
    public int damage;
    public int range;
    public float fireRate;
    public int maxammo;
    public int ammo;
    public int reloadtime;
    public Model modelname;
    public Sound soundname;
    public float lastShotTime;
    public bool isReloading;
    public float reloadStartTime;
    public float force;

    // constructeur
    public Weapon(string nom, int degats, int portee, float cadence, int munitionsMax, int tempsRecharge, Model modele3D, Sound son, float power)
    {
        name = nom;
        damage = degats;
        range = portee;
        fireRate = cadence;
        maxammo = munitionsMax;
        ammo = munitionsMax;
        reloadtime = tempsRecharge;
        modelname = modele3D;
        soundname = son;
        lastShotTime = 0.0f;
        isReloading = false;
        reloadStartTime = 0.0f;
        force = power;
    }



    // 1. On met à jour la signature avec List<Enemy> enemiesList
    public bool Shoot(Vector3 direction, Camera3D camera, ref BodyReference playerBody, List<Program.BarrelSpot> barrelSpots, List<Enemy> enemiesList, out Vector3 startLaser, out Vector3 endLaser)
    {
        startLaser = Vector3.Zero;
        endLaser = Vector3.Zero;

        // 1. Vérification : A-t-on le droit de tirer ?
        if (ammo <= 0 || isReloading || (float)Raylib.GetTime() - lastShotTime < fireRate)
        {
            return false; // Le tir ne part pas
        }

        // 2. Le tir part !
        Raylib.PlaySound(soundname);
        lastShotTime = (float)Raylib.GetTime();
        ammo--;

        // 3. Le recul physique sur le joueur
        float forceRecul = 0.5f;
        playerBody.Velocity.Linear -= direction * forceRecul * force;

        // ==========================================
        // 1. LE VISUEL (Part du canon de l'arme)
        // ==========================================
        Vector3 right = Vector3.Normalize(Vector3.Cross(direction, camera.Up));
        startLaser = camera.Position + direction * 0.5f + right * 0.25f;

        // ==========================================
        // 2. LA PHYSIQUE (Part exactement des yeux !)
        // ==========================================
        Vector3 physiqueStart = camera.Position;
        LaserSensor capteurLaser = new LaserSensor(playerBody.CollidableReference);
        capteurLaser.aTouche = false;

        Program.simulation.RayCast(physiqueStart, direction, range, ref capteurLaser);

        float distanceEffective = range;

        /// --- DÉTECTION BEPU STRICTE (Hitscan pour Fusils) ---
        if (capteurLaser.aTouche)
        {
            distanceEffective = capteurLaser.distanceImpact;

            // 1. EST-CE QU'ON TOUCHE UN ENNEMI ? (Objet Dynamique)
            if (capteurLaser.ObjetTouche.Mobility == CollidableMobility.Dynamic)
            {
                if (range >= 10)
                {
                    BodyHandle hitHandle = capteurLaser.ObjetTouche.BodyHandle;
                    
                    foreach (Enemy enemy in enemiesList)
                    {
                        if (enemy.isAlive && enemy.bodyId == hitHandle)
                        {
                            enemy.TakeDamage(damage);
                            Program.hitmarkerTimer = 0.3f;
                            break;
                        }
                    }
                }
            }
            // 2. NOUVEAU : EST-CE QU'ON TOUCHE UN BARIL ? (Objet Statique)
            else if (capteurLaser.ObjetTouche.Mobility == CollidableMobility.Static)
            {
                StaticHandle hitStaticHandle = capteurLaser.ObjetTouche.StaticHandle;
                
                // On vérifie si le mur qu'on vient de toucher est en fait un baril
                for (int i = 0; i < barrelSpots.Count; i++)
                {
                    // Si ce spot a un baril physique ET que son Ticket correspond à ce qu'on a touché
                    if (barrelSpots[i].hasBarrel && barrelSpots[i].estSolide && barrelSpots[i].handlePhysique == hitStaticHandle)
                    {
                        Program.OnBarrelHit(i); // BOUM !
                        break; // Le baril a explosé, on arrête de chercher
                    }
                }
            }
        }

        // On arrête le visuel du laser au point d'impact
        endLaser = startLaser + direction * distanceEffective;

        // ==========================================
        // 3. LE HACK DE LA MÊLÉE (Cône large pour le couteau)
        // ==========================================
        if (range < 10)
        {
            foreach (Enemy enemy in enemiesList)
            {
                if (!enemy.isAlive) continue;
                
                Vector3 enemyCenter = enemy.GetPosition() + new Vector3(0, 0.5f, 0); 
                float dist = Vector3.Distance(physiqueStart, enemyCenter);

                if (dist <= range)
                {
                    Vector3 toEnemy = Vector3.Normalize(enemyCenter - physiqueStart);
                    float dot = Vector3.Dot(direction, toEnemy);

                    if (dot > 0.5f) 
                    {
                        enemy.TakeDamage(damage);
                        Program.hitmarkerTimer = 0.3f;
                    }
                }
            }
        }

        return true;
    }
    

    public void Reload()
    {
        if ((Raylib.IsKeyPressed(KeyboardKey.R) || ammo <= 0) && !isReloading)
        {
            isReloading = true;
            reloadStartTime = (float)Raylib.GetTime();
        }
        if (isReloading && (float)Raylib.GetTime() - reloadStartTime >= reloadtime)
        {
            ammo = maxammo;
            isReloading = false;
        }
    }

    public void Switch()
    {
        
    }
}


