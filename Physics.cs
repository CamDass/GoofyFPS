using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuPhysics.Trees;
using BepuUtilities;

// ========================================================================================
// LES CONTRATS DE PHYSIQUE
// ========================================================================================

public unsafe struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    public void Initialize(Simulation simulation) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
    { return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) 
    { return true; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pairMaterial.FrictionCoefficient = 1f; // Force de frottement par défaut
        pairMaterial.MaximumRecoveryVelocity = 2f;
        pairMaterial.SpringSettings = new SpringSettings(30, 1);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
    { return true; }

    public void Dispose() { }
}

public struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    public Vector3 Gravity;
    public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
    public readonly bool AllowSubstepsForUnconstrainedBodies => false;
    public readonly bool IntegrateVelocityForKinematics => false;

    public void Initialize(Simulation simulation) { }

    public PoseIntegratorCallbacks(Vector3 gravity) { Gravity = gravity; }

    public void PrepareForIntegration(float dt) { }

    public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
    {
        // 1. On clone notre petite gravité dans une grosse boîte Wide
        Vector3Wide.Broadcast(Gravity, out Vector3Wide gravityWide);
        
        // 2. On applique cette grosse boîte à la vitesse des objets !
        velocity.Linear += gravityWide * dt;
    }
}

public struct GroundSensor : IRayHitHandler
{
    public bool toucheSol;
    public CollidableReference IdJoueur ;

    public GroundSensor(CollidableReference idJoueur)
    {
        toucheSol = false;
        IdJoueur = idJoueur;
    }

    public bool AllowTest(CollidableReference collidable)
    {
        //comparer le code bare de l'objet touché et celui du joueur pour eviter de bloquer le saut
        //.packed est une maniere qu'a bepu de comaprer rapidement les id
        //si c nous meme, c faux donc le laser passe a travers. 
        return collidable.Packed != IdJoueur.Packed;
    }

    //garder pour plustard si on a des objets complexe
    public bool AllowTest(CollidableReference collidable, int childIndex)
    {
        //pour l'instant on garde ça simple, on autorise le laser a toucher toutes les sous parties
        return true;
    }

    public void OnRayHit(in RayData ray, ref float maximumT, float t, in Vector3 normal, CollidableReference collidable, int childIndex)
    {
        //si bepu appel cette fonction c que ça a touché le sol
        toucheSol = true;
        //on dit au laser de s'arreter net a la distance de l'impact. 
        maximumT = t;
    }
}