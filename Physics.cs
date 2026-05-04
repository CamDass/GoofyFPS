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


// On ajoute le "diplôme" ISweepHitHandler (et on garde IRayHitHandler au cas où tu en aurais besoin ailleurs)// On déclare fièrement les DEUX interfaces (Le diplôme Sphère ET le diplôme Laser)using BepuPhysics;

public struct GroundSensor : ISweepHitHandler, IRayHitHandler
{
    public CollidableReference IgnoreCollidable;
    public bool toucheSol;
    // NOUVEAU : On mémorise l'inclinaison du sol touché
    public Vector3 normaleDuSol; 

    public GroundSensor(CollidableReference ignoreCollidable)
    {
        IgnoreCollidable = ignoreCollidable;
        toucheSol = false;
        normaleDuSol = new Vector3(0, 1, 0); // Par défaut, on imagine un sol plat
    }

    public bool AllowTest(CollidableReference collidable) { return collidable != IgnoreCollidable; }
    public bool AllowTest(CollidableReference collidable, int childIndex) { return true; }

    // ==========================================
    // LE SWEEP (La Balle)
    // ==========================================
    public void OnHit(ref float maximumT, float t, in Vector3 hitLocation, in Vector3 hitNormal, CollidableReference collidable)
    {
        // LE FAMEUX PRODUIT SCALAIRE (Dot Product)
        // On compare la normale du sol avec le vecteur "Haut" (0, 1, 0)
        // 0.7f correspond à une pente maximale d'environ 45 degrés.
        if (Vector3.Dot(hitNormal, new Vector3(0, 1, 0)) > 0.7f)
        {
            toucheSol = true;
            normaleDuSol = hitNormal; // On enregistre la pente !
            if (t < maximumT) maximumT = t;
        }
    }

    public void OnHitAtZeroT(ref float maximumT, CollidableReference collidable)
    {
        toucheSol = true;
        // On remet une normale droite par sécurité si on frotte bizarrement au départ
        normaleDuSol = new Vector3(0, 1, 0); 
    }

    // ==========================================
    // LE RAYCAST (La Glissade)
    // ==========================================
    public void OnRayHit(in RayData ray, ref float maximumT, float t, in Vector3 hitNormal, CollidableReference collidable, int childIndex)
    {
        if (Vector3.Dot(hitNormal, new Vector3(0, 1, 0)) > 0.7f)
        {
            toucheSol = true;
            normaleDuSol = hitNormal;
            if (t < maximumT) maximumT = t;
        }
    }
}

