using System.Numerics;
using Raylib_cs;
using BepuPhysics;
using BepuPhysics.Collidables;


//List<Vector3> ListPosEnnemis = ();



public class Enemy
{
    public float attackCooldown;
    public BodyHandle bodyId ;
    public int health;
    public float speed;
    public bool isAlive;

    public Enemy(Vector3 startPos, int hp, float spd)
    {
        health = hp;
        speed = spd;
        isAlive = true;

        Capsule shape = new Capsule(0.5f,1f);
        TypedIndex shapeIndex = Program.simulation.Shapes.Add(shape);
        
        BodyInertia inertia = shape.ComputeInertia(1f);
        inertia.InverseInertiaTensor = new BepuUtilities.Symmetric3x3(); 
          
        BodyDescription bodyDesc = BodyDescription.CreateDynamic(startPos, inertia, shapeIndex, 0.01f);
        bodyId = Program.simulation.Bodies.Add(bodyDesc);
    }

    //le cervaux (aled ça va etre chaud ici)
    // Le Cerveau (Avec système d'évitement d'obstacles)
    // Le Cerveau (Avec évitement d'obstacles ET détection du vide)
    public void Maj(Vector3 playerPos, ref BodyReference PlayerBody)
    {
        if (!isAlive) return;

        if (attackCooldown > 0) attackCooldown -= Raylib_cs.Raylib.GetFrameTime();

        BodyReference enemyBody = Program.simulation.Bodies.GetBodyReference(bodyId);
        
        // ==========================================
        // CORRECTION DE LA DISTANCE (On ignore la hauteur Y)
        // ==========================================
        Vector3 positionEnnemiPlate = enemyBody.Pose.Position;
        positionEnnemiPlate.Y = 0;
        
        Vector3 positionJoueurPlate = playerPos;
        positionJoueurPlate.Y = 0;

        float distanceHorizontale = Vector3.Distance(positionEnnemiPlate, positionJoueurPlate);
        float distanceToPlayer = Vector3.Distance(positionEnnemiPlate, positionJoueurPlate);
        float differenceHauteur = playerPos.Y - enemyBody.Pose.Position.Y;
        
        if (distanceHorizontale < 2.2f && differenceHauteur < 3.5f && attackCooldown <= 0)
        {
            // Il te met une claque !
            Program.localPlayer.TakeDamage(10); 
            attackCooldown = 0.5f; 

            // ==========================================
            // NOUVEAU : LE KNOCKBACK !
            // ==========================================
            // 1. On calcule la direction de la claque (de l'ennemi vers toi)
            Vector3 pushDir = Vector3.Normalize(positionJoueurPlate - positionEnnemiPlate);
            
            // 2. On ajoute un petit saut vers le haut pour bien désorienter la caméra
            pushDir.Y = 0.3f; 
            
            // 3. On applique une force violente sur le corps physique de ton joueur (espionCube)
            PlayerBody.Velocity.Linear += pushDir * 20f; // Modifie le '15f' si ça pousse trop ou pas assez !
        }

        enemyBody.Awake = true;
        
        // --- 2. LE DÉPLACEMENT (Cerveau) ---
        Vector3 dirVoulue = playerPos - enemyBody.Pose.Position;
        dirVoulue.Y = 0; 

        // CORRECTION DU BUG : On utilise une seule logique de distance
        // Si on est à plus de 1.8 mètre, on court vers le joueur
        if (distanceToPlayer > 1.4f) 
        {
            dirVoulue = Vector3.Normalize(dirVoulue);

            WallSensor capteurVue = new WallSensor(enemyBody.CollidableReference);
            Program.simulation.RayCast(enemyBody.Pose.Position, dirVoulue, 2.0f, ref capteurVue);

            if (capteurVue.toucheMur)
            {
                float dotProduct = Vector3.Dot(dirVoulue, capteurVue.normaleDuMur);
                dirVoulue = dirVoulue - (capteurVue.normaleDuMur * dotProduct);
                dirVoulue += capteurVue.normaleDuMur * 0.2f;

                if (dirVoulue.LengthSquared() > 0) dirVoulue = Vector3.Normalize(dirVoulue);
            }

            Vector3 positionDevant = enemyBody.Pose.Position + (dirVoulue * 1.5f);
            positionDevant.Y += 0.5f; 
            
            LaserSensor capteurVide = new LaserSensor(enemyBody.CollidableReference);
            Program.simulation.RayCast(positionDevant, new Vector3(0, -1f, 0), 2.5f, ref capteurVide);

            if (!capteurVide.aTouche)
            {
                dirVoulue = Vector3.Zero; 
            }
            
            enemyBody.Velocity.Linear.X = dirVoulue.X * speed;
            enemyBody.Velocity.Linear.Z = dirVoulue.Z * speed;
        }
        else 
        {
            // ==========================================
            // CORRECTION DU BUG : LE FREINAGE
            // ==========================================
            // Si on est à portée de frappe, on coupe le moteur des jambes proprement !
            enemyBody.Velocity.Linear.X = 0;
            enemyBody.Velocity.Linear.Z = 0;
        }
    } // test

    // La Gestion des dégâts
    public void TakeDamage(int damage)
    {
        if (!isAlive) return;

        //la pos a sa grosse buz cut
        Vector3 headPosition = GetPosition() + new Vector3(0, 1.8f, 0);

        Random rand = new Random();
        float randomX = (float)(rand.NextDouble() * 0.6f - 0.3f);
        float randomZ = (float)(rand.NextDouble() * 0.6f - 0.3f);
        headPosition.X += randomX;
        headPosition.Z += randomZ;

        // 3. On ajoute le texte à notre liste d'affichage
        Program.activeDamageTexts.Add(new Program.DamageText(headPosition, damage));



        health -= damage;
        if (health <= 0)
        {
            isAlive = false;
            // Utiliser le pool de sounds de death pour permettre plusieurs lectures simultan\u00e9es
            Raylib.PlaySound(Program.deathSounds[Program.deathSoundIndex]);
            Program.deathSoundIndex = (Program.deathSoundIndex + 1) % Program.deathSounds.Length;
            // On le supprime physiquement du monde
            Program.simulation.Bodies.Remove(bodyId); 
        }
    }

    // Pour savoir où dessiner le modèle 3D
    public Vector3 GetPosition()
    {
        if (!isAlive) return Vector3.Zero;
        return Program.simulation.Bodies.GetBodyReference(bodyId).Pose.Position;
    }

    public int GetLife()
    {
        if (!isAlive) return 0;
        return health;
    }
}