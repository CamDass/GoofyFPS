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
    public void Maj(Vector3 playerPos)
    {
        if (!isAlive) return;

        if (attackCooldown > 0) attackCooldown -= Raylib_cs.Raylib.GetFrameTime();

        BodyReference enemyBody = Program.simulation.Bodies.GetBodyReference(bodyId);
        
        // Si l'ennemi est à moins de 1.5 mètre de toi et qu'il est prêt à attaquer
        float distanceToPlayer = Vector3.Distance(enemyBody.Pose.Position, playerPos);
        if (distanceToPlayer < 2f && attackCooldown <= 0)
        {
            // Il te met une claque de 15 dégâts !
            Program.localPlayer.TakeDamage(15); 
            attackCooldown = 0.5f; // Il doit attendre 1 seconde avant de refrapper
        }

        enemyBody.Awake = true;
        

        // 1. L'instinct primaire : aller vers le joueur
        Vector3 dirVoulue = playerPos - enemyBody.Pose.Position;
        dirVoulue.Y = 0; 

        if (dirVoulue.LengthSquared() > 0.5f)
        {
            dirVoulue = Vector3.Normalize(dirVoulue);

            // --- L'ANTENNE ANTI-MURS (Ton code précédent) ---
            WallSensor capteurVue = new WallSensor(enemyBody.CollidableReference);
            Program.simulation.RayCast(enemyBody.Pose.Position, dirVoulue, 2.0f, ref capteurVue);

            if (capteurVue.toucheMur)
            {
                float dotProduct = Vector3.Dot(dirVoulue, capteurVue.normaleDuMur);
                dirVoulue = dirVoulue - (capteurVue.normaleDuMur * dotProduct);
                dirVoulue += capteurVue.normaleDuMur * 0.2f;

                if (dirVoulue.LengthSquared() > 0) dirVoulue = Vector3.Normalize(dirVoulue);
            }

            // ==========================================
            // NOUVEAU : LE DÉTECTEUR DE VIDE (CLIFF SENSOR)
            // ==========================================

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
    }

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
            Raylib.PlaySound(Program.death);
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