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
    public float reloadtime; // Changé en float pour précision
    public Model modelname;
    public Sound soundname;
    public float lastShotTime;
    public bool isReloading;
    public float reloadStartTime;
    public float force;
    public bool requiresReload; // true si l'arme a besoin de recharger, false pour les armes infinies
    private bool reloadSoundPlayed = false; // Pour jouer le son une seule fois

    // constructeur
    public Weapon(string nom, int degats, int portee, float cadence, int munitionsMax, int tempsRecharge, Model modele3D, Sound son, float power, bool needsReload = true)
    {
        name = nom;
        damage = degats;
        range = portee;
        fireRate = cadence;
        maxammo = munitionsMax;
        ammo = munitionsMax;
        reloadtime = 1.7f; // Temps de rechargement fixe à 1.7s
        modelname = modele3D;
        soundname = son;
        lastShotTime = 0.0f;
        isReloading = false;
        reloadStartTime = 0.0f;
        force = power;
        requiresReload = needsReload;
        reloadSoundPlayed = false;
    }



    // 1. On met à jour la signature avec List<Enemy> enemiesList
    public bool Shoot(Vector3 direction, Camera3D camera, ref BodyReference playerBody, List<Program.BarrelSpot> barrelSpots, List<Enemy> enemiesList, out Vector3 startLaser, out Vector3 endLaser)
    {
        startLaser = Vector3.Zero;
        endLaser = Vector3.Zero;

        // 1. Vérification : A-t-on le droit de tirer ?
        // Pour les armes qui ne se rechargent pas, ignorer la vérification des munitions
        bool outOfAmmo = requiresReload && ammo <= 0;
        if (outOfAmmo || isReloading || (float)Raylib.GetTime() - lastShotTime < fireRate)
        {
            // Jouer le son no-ammo si on essaie de tirer sans munitions
            if (outOfAmmo && !isReloading && (float)Raylib.GetTime() - lastShotTime >= fireRate)
            {
                Program.PlaySoundWithPriority(Program.noAmmoSound, Program.SoundPriority.Low);
            }
            return false; // Le tir ne part pas
        }

        // 2. Le tir part !
        bool isBazooka = string.Equals(name, "Bazooka", StringComparison.OrdinalIgnoreCase);

        if (isBazooka) 
            Program.PlaySoundWithPriority(soundname, Program.SoundPriority.Critical);
        else 
            Program.PlaySoundWithPriority(soundname, Program.SoundPriority.High);
        lastShotTime = (float)Raylib.GetTime();
        
        // Décrémenter les munitions seulement si l'arme en nécessite
        if (requiresReload)
            ammo--;

        // 3. Le recul physique sur le joueur
        float forceRecul = 1f;
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

        // --- A. LE LASER BEPU (Pour les Murs et les Barils) ---
        float distanceEffectiveMur = range;
        if (capteurLaser.aTouche)
        {
            distanceEffectiveMur = capteurLaser.distanceImpact;
            
            // EST-CE QU'ON TOUCHE UN BARIL ?
            if (capteurLaser.ObjetTouche.Mobility == CollidableMobility.Static)
            {
                StaticHandle hitStaticHandle = capteurLaser.ObjetTouche.StaticHandle;
                for (int i = 0; i < barrelSpots.Count; i++)
                {
                    if (barrelSpots[i].hasBarrel && barrelSpots[i].estSolide && barrelSpots[i].handlePhysique == hitStaticHandle)
                    {
                        Program.OnBarrelHit(i);
                        break; 
                    }
                }
            }
        }

        // --- B. LA HITBOX VIRTUELLE (Pour les Ennemis) ---
        float distanceEffectiveEnnemi = range;
        Enemy ennemiTouche = null;
        isBazooka = string.Equals(name, "Bazooka", StringComparison.OrdinalIgnoreCase);

        if (range >= 10) // Uniquement les armes Hitscan et le Bazooka
        {
            foreach (Enemy enemy in enemiesList)
            {
                if (!enemy.isAlive) continue;
                
                // ON CRÉE LA SPHÈRE SUR LE FANTÔME !
                // (On remonte le centre de 1.2m pour cibler le corps visuel flottant)
                Vector3 centreVisuel = enemy.GetPosition() + new Vector3(0, 1.2f, 0);
                float rayonHitbox = 0.6f; // La largeur de ta hitbox

                if (Program.RayIntersectsSphere(physiqueStart, direction, centreVisuel, rayonHitbox, out float hitDist))
                {
                    // Si on touche l'ennemi ET qu'il n'est pas caché derrière un mur !
                    if (hitDist < distanceEffectiveMur && hitDist < distanceEffectiveEnnemi)
                    {
                        distanceEffectiveEnnemi = hitDist;
                        ennemiTouche = enemy;
                    }
                }
            }
            
            // Si on a validé un tir DIRECT sur un ennemi (Et que ce n'est pas un bazooka)
            if (ennemiTouche != null && !isBazooka)
            {
                bool etatAvant = ennemiTouche.isAlive;
                ennemiTouche.TakeDamage(damage);
                
                if (etatAvant && !ennemiTouche.isAlive) Program.TriggerHitmarker(true); 
                else if (etatAvant) Program.TriggerHitmarker(false);
            }
        }

        // --- C. ARRÊT DU LASER ET IMPACT ---
        // Le laser s'arrête au premier obstacle rencontré (le mur ou le fantôme)
        float distanceVisuelle = Math.Min(distanceEffectiveMur, distanceEffectiveEnnemi);
        endLaser = startLaser + direction * distanceVisuelle;
        Vector3 impactPoint = physiqueStart + direction * distanceVisuelle; 
        
        // --- D. LOGIQUE DU BAZOOKA ---
        bool aToucheQuelqueChose = (distanceEffectiveMur < range || distanceEffectiveEnnemi < range);
        if (isBazooka && aToucheQuelqueChose)
        {
            float explosionRadius = 6f;
            int explosionResult = ExplodeAt(impactPoint, explosionRadius, damage, enemiesList);
            
            bool brokeAnyBarrel = Program.BreakBarrelsInRadius(impactPoint, explosionRadius);
            if (brokeAnyBarrel) Program.SwitchWeaponFromBarrel();
            
            Program.SpawnExplosionEffect(impactPoint);
            
            if (explosionResult == 2) Program.TriggerHitmarker(true);
            else if (explosionResult == 1) Program.TriggerHitmarker(false);
            
            Program.duckingTimer = 1.5f;
        }

        // --- E. LE HACK DE LA MÊLÉE (Couteau, Épée) ---
        if (range < 10)
        {
            foreach (Enemy enemy in enemiesList)
            {
                if (!enemy.isAlive) continue;
                
                // CORRECTION MÊLÉE : On centre les dégâts sur le modèle visuel (Y + 1.2f)
                Vector3 enemyCenter = enemy.GetPosition() + new Vector3(0, 1.2f, 0); 
                float dist = Vector3.Distance(physiqueStart, enemyCenter);
                if (dist <= range)
                {
                    Vector3 toEnemy = Vector3.Normalize(enemyCenter - physiqueStart);
                    float dot = Vector3.Dot(direction, toEnemy);

                    if (dot > 0.5f) 
                    {
                        bool etatAvant = enemy.isAlive;
                        enemy.TakeDamage(damage);
                        if (etatAvant && !enemy.isAlive) Program.TriggerHitmarker(true);
                        else if (etatAvant) Program.TriggerHitmarker(false);
                    }
                }
            }
        }

        return true;
    }
    
    // CORRECTION : La fonction renvoie maintenant un "bool" (vrai/faux) au lieu de "void"
    private int ExplodeAt(Vector3 center, float radius, int explosionDamage, List<Enemy> enemiesList)
    {
        int resultatExplosion = 0; // 0 = raté, 1 = touché, 2 = tué
        
        foreach (Enemy enemy in enemiesList)
        {
            if (!enemy.isAlive) continue;
            Vector3 enemyCenter = enemy.GetPosition() + new Vector3(0, 1.2f, 0);
            float distToEnemy = Vector3.Distance(center, enemyCenter);
            
            if (distToEnemy <= radius)
            {
                bool etatAvant = enemy.isAlive;
                enemy.TakeDamage(explosionDamage);
                
                // Le Kill prime sur le Hit (Si l'explosion tue un ennemi mais en blesse un autre, on veut le son de Kill)
                if (etatAvant && !enemy.isAlive) resultatExplosion = 2; 
                else if (resultatExplosion == 0) resultatExplosion = 1;
            }
        }
        return resultatExplosion;
    }

    public void Reload()
    {
        // Ne pas permettre le rechargement si l'arme ne le nécessite pas
        if (!requiresReload)
            return;

        // Recharge uniquement si on appuie sur R ET qu'on n'est pas déjà en train de recharger
        if (KeyBinds.IsReloadingPressed() && !isReloading)
        {
            isReloading = true;
            reloadStartTime = (float)Raylib.GetTime();
            reloadSoundPlayed = false; // Réinitialiser le flag du son
        }

        // Gérer le rechargement en cours
        if (isReloading)
        {
            // Jouer le son de rechargement UNE SEULE FOIS au début
            if (!reloadSoundPlayed)
            {
                Program.PlaySoundWithPriority(Program.reloadSound, Program.SoundPriority.Medium);
                reloadSoundPlayed = true;
            }

            // Vérifier si le rechargement est terminé
            if ((float)Raylib.GetTime() - reloadStartTime >= reloadtime)
            {
                ammo = maxammo;
                isReloading = false;
            }
        }
    }

    // Retourner l'angle de rotation pour l'inclinaison de l'arme pendant le rechargement
    // Phase 1 (0 à 1/3) : incliner progressivement vers -45°
    // Phase 2 (1/3 à 2/3) : rester à -45°
    // Phase 3 (2/3 à 1) : revenir progressivement à 0°
    public float GetReloadRotationAngle()
    {
        if (!isReloading)
            return 0f;

        float elapsed = (float)Raylib.GetTime() - reloadStartTime;
        float progress = Math.Min(elapsed / reloadtime, 1f); // 0 à 1
        
        float thirdTime = reloadtime / 5f;
        float twoThirdTime = 3f * reloadtime / 5f;
        
        if (elapsed <= thirdTime)
        {
            // Phase 1 : interpoler de 0° à -45°
            float phaseProgress = elapsed / thirdTime; // 0 à 1
            return -45f * phaseProgress;
        }
        else if (elapsed <= twoThirdTime)
        {
            // Phase 2 : rester à -45°
            return -45f;
        }
        else
        {
            // Phase 3 : interpoler de -45° à 0°
            float phaseProgress = (elapsed - twoThirdTime) / thirdTime; // 0 à 1
            float finalangle = -45f + (45f * phaseProgress);
            if (finalangle >= 0) finalangle = 0;
            return finalangle;
        }
    }
}


