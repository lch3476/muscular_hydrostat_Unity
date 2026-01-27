using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using Mono.Cecil.Cil;
using Unity.VisualScripting;

// An abstract class for constrained particle dynamics.

// We let a constrained particle dynamics system be a collection of verticies with mass
// and constraints. Forces can act on these vertices and induce accelerations. These
// accelerations are integrated over time to get velocities and positions. The state
// of this system is a length 6n vector of positions and velocities where `n` is the
// number of vertices.

// Properties:
//     inv_masses: a length n array of the reciprocals of vertex masses
//     constraints: a list of constraints to apply to the system
//     external_forces: a length nxd array of external forces acting on the vertices
//     constraint_damping_rate: the rate of damping for the constraints
//     constraint_spring_rate: the rate of spring force for the constraints
public abstract class ConstrainedDynamic : Dynamic
{

    [SerializeField] float constraintDampingRate = 50f;
    [SerializeField] float constraintSpringRate = 50f;

    [SerializeField] float actuationForceScale = 1f;
    public float ActuationForceScale { get { return actuationForceScale; } }
    [SerializeField] float explicitForceScale = 1f;
    public float ExplicitForceScale { get { return explicitForceScale; } }
    [SerializeField] float reactionForceScale = 1f;
    public float ReactionForceScale { get { return reactionForceScale; } }
    [SerializeField] float passiveForceScale = 1f;
    public float PassiveForceScale { get { return passiveForceScale; } }
    [SerializeField] float passiveEdgeForceScale = 1f;
    public float PassiveEdgeForceScale { get { return passiveEdgeForceScale; } }
    [SerializeField] float externalForceScale = 1f;
    public float ExternalForceScale { get { return externalForceScale; } }

    [SerializeField] List<Constraint> constraints;
    private float[] invMasses;
    public float[] InvMasses { get { return invMasses; } set { invMasses = value; } }
    private Vector3[] externalForces;
    public Vector3[] ExternalForces { get { return externalForces; } set { externalForces = value; } }
    private int numParticles;
    public int NumParticles { get { return numParticles; } set { numParticles = value; } }
    int statesNum;

    // Ensure critical initialization runs in Awake so other components (like Simulator)
    // can safely read arrays during their Start methods. Unity calls Awake on all
    // enabled objects before any Start runs.
    void Awake()
    {
        base.Awake(); // Call parent's Awake to initialize NumControls and NumStates
        InitNumParticles();
        InitExternalForces();
        InitInvMasses();
        Initconstraints();
    }

    // TODO: implement after environment implementation
    // public void SetEnvironment(Environment environment, float[] state, List<Obstacle> obstacles);

    // TODO: Need to be modified depending on the constraint implementation
    private void Initconstraints()
    {
        foreach (Constraint constraint in constraints)
        {
            if (ModelBuilder == null)
            {
                UnityEngine.Debug.LogError("ModelBuilder is null when initializing constraints.");
                continue;
            }
            constraint.Initialize(ModelBuilder);
        }
    }

    // Set the force acting on a particular vertex
    // Args:
    //     vertices: a length l array of vertex indices to apply forces to
    //     forces: an lxd array of forces
    public void ApplyExternalForces(Vector3[] forces)
    {
        for (int i = 0; i < externalForces.Length; ++i)
        {
            // the order of vertices, externalForces, and forces
            // should be the same
            externalForces[i] = forces[i];
        }
    }


    // Calculate and sum all forces that are not calculated via constrained
    //     dynamics.

    //     Typically, forces are notated as PassiveForces=ActiveForces like in the case
    //     of m*ddx + b*dx + k*x = F. In this case, our external forces are any actuations
    //     or external forces not caused by constrainst. This is why there is a negative
    //     sign.

    //     Args:
    //         state: the current state of the system
    //         actuation_forces: an nxd jnp.ndarray where n is the number of vertices and
    //             d is the dimension of the space

    //     Returns:
    //         An nxd array of total explicit force vectors on the vertices.
    private Vector3[] CalcExplicitForces(Vector3[] actuationForces)
    {
        if (actuationForces == null)
        {
            UnityEngine.Debug.LogError("CalcExplicitForces: actuationForces is null.");
            return null;
        }
        // Defensive: handle null external/actuation/passive forces and length mismatches.
        int count = actuationForces.Length;

        // If we still don't know count, return empty list
        if (count == 0)
        {
            UnityEngine.Debug.LogError("CalcExplicitForces: unable to determine number of vertices; returning null.");
            return null;
        }

        Vector3[] explicitForces = new Vector3[count];

        Vector3[] passiveForces = null;
        if (ModelBuilder != null)
        {
            passiveForces = CalcPassiveForces(ModelBuilder.GetPositions(), ModelBuilder.Velocities);
        }
        else
        {
            UnityEngine.Debug.LogError("ModelBuilder is null in CalcExplicitForces; skipping passive forces calculation.");
            return null;
        }
        

        // If CalcPassiveForces returned per-vertex forces (length == count), subtract per-vertex.
        if (passiveForces != null && passiveForces.Length == count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 externalForce = (externalForces != null && i < externalForces.Length) ? externalForces[i] : Vector3.zero;
                Vector3 actuationForce = (i < actuationForces.Length) ? actuationForces[i] : Vector3.zero;
                Vector3 passiveForce = passiveForces[i];
                explicitForces[i] = externalForce + actuationForce - passiveForce;
            }
        }
        else
        {
            UnityEngine.Debug.LogError("CalcExplicitForces: passiveForces length mismatch; returning null.");
            return null;
        }

        return Utility.MatrixMultiply<Vector3>(explicitForces, explicitForceScale);
    }

    private Vector3[] CalcReactionForces(float[] state, Vector3[] explicitForces)
    {
        if (constraints == null || constraints.Count == 0)
        {
            Vector3[] zeros = Utility.CreateInitializedArray<Vector3>(externalForces.Length, Vector3.zero);
            return zeros;
        }

        // Calculate constraints, jacobian, and jacobian derivative
        (float[] constraintsVec, float[,] jacobian, float[,] jacobianDerivative) = CalcConstraints();

        // invMasses: float[] of length n
        // jacobian: (m, n*3)
        // jacobian.T: (n*3, m)
        // invMasses[:, None] * jacobian.T: (n, m) * (n*3, m) -- need to expand invMasses to match shape

        // Compute front_matrix = jacobian @ (inv_masses[:, None] * jacobian.T)
        float[,] invMassesMatrix = Utility.Diagonalize(invMasses);
        float[,] jacobianTranspose = Utility.Transpose(jacobian);   // (n*3, m)
        float[,] invMassesJacobianTranspose = Utility.MatrixMultiply(jacobianTranspose, invMassesMatrix); // (n*3, m)
        float[,] frontMatrix = Utility.MatrixMultiply(jacobian, invMassesJacobianTranspose); // (m, m)
        // Regularization: front_matrix + eye * 1e-6
        Utility.AddIdentityInPlace(frontMatrix, 1e-6f);

        // dependent_array = -(
        //     djacobian_dt @ vel.ravel()
        //     + jacobian @ (inv_masses * explicit_forces.ravel())
        //     + constraint_damping_rate * jacobian @ vel.ravel()
        //     + constraint_spring_rate * constraints
        // )

        float[] velocitiesFlat = Utility.Flatten(ModelBuilder.Velocities); // (n*3,)
        float[] explicitForcesFlat = Utility.Flatten(explicitForces); // (n*3,)

        float[] invMassesTimesExplicit = Utility.MatrixMultiply(invMasses, explicitForcesFlat); // (n*3,)
        float[] djacobianDtVel = Utility.MatrixMultiply(jacobianDerivative, velocitiesFlat); // (m,)
        float[] jacobianInvMassesExplicit = Utility.MatrixMultiply(jacobian, invMassesTimesExplicit); // (m,)
        float[] jacobianVel = Utility.MatrixMultiply(jacobian, velocitiesFlat); // (m,)

        float[] dependentArray = new float[djacobianDtVel.Length];
        for (int i = 0; i < dependentArray.Length; i++)
        {
            dependentArray[i] = -(
                djacobianDtVel[i]
                + jacobianInvMassesExplicit[i]
                + constraintDampingRate * jacobianVel[i]
                + constraintSpringRate * constraintsVec[i]
            );
        }

        // Solve for lagrange_multipliers: front_matrix x = dependent_array
        float[] lagrangeMultipliers = Utility.SolveLinearSystem(frontMatrix, dependentArray);

        // reaction_forces = jacobian.T @ lagrange_multipliers
        float[] reactionForcesFlat = Utility.MatrixMultiply(jacobianTranspose, lagrangeMultipliers);
        // Convert flat array back to Vector3[]
        Vector3[] reactionForces = Utility.UnflattenToVector3Array(reactionForcesFlat);

        return Utility.MatrixMultiply<Vector3>(reactionForces, reactionForceScale);
    }

    // Translated from Python `continuous_dynamics`
    // Returns the state derivative as a flattened float array: [velocities_flat, accelerations_flat]
    public override float[] ContinuousDynamics(float[] state, float[] control, float t)
    {
        var sw = Stopwatch.StartNew();

        // Policy and other callers may pass controls as float[]; convert to List<float>
        Vector3[] actuationForces = CalcActuationForces(control);
        //Utility.PrintVectors(actuationForces, "Actuation Forces");
        sw.Restart();

        Vector3[] explicitForces = CalcExplicitForces(actuationForces);
        Utility.PrintArray(explicitForces, "Explicit Forces");
        //Utility.PrintVectors(explicitForces, "Explicit Forces");
        sw.Restart();

        // Vector3[] explicitForces = explicitForcesList.ToArray();
        // Vector3[] reactionForces = CalcReactionForces(state, explicitForces);
        // UnityEngine.Debug.Log("Reaction forces " + sw.Elapsed.TotalSeconds);
        // sw.Restart();

        // TODO: later comment back in reaction forces
        // Vector3[] forces = explicitForces + reactionForces;
        // float[] forcesFlat = Utility.Flatten(forces);
        float[] forcesFlat = Utility.Flatten(explicitForces);
        float[] scaledForcesFlat = Utility.MatrixMultiply(invMasses, forcesFlat);
        float[] velocitiesFlat = Utility.Flatten(ModelBuilder.Velocities);
        //dstate
        return  Utility.HorizontalStack(velocitiesFlat, scaledForcesFlat);
    }

    // TODO replace with autograd calculation of constraints
    private (float[] constraints, float[,] jacobians, float[,] jacobianDerivatives) CalcConstraints()
    {
        List<float> constraints = new List<float>();
        List<float[,]> jacobians = new List<float[,]>();
        List<float[,]> jacobianDerivatives = new List<float[,]>();

        foreach (var constraint in this.constraints)
        {
            (float[] _constraints, float[,,] _jacobians, float[,,] _jacobianDerivatives) calculatedConstraints =
                constraint.CalculateConstraints();
            float[] constraintVector = calculatedConstraints._constraints;
            float[,,] jacobian = calculatedConstraints._jacobians;
            float[,,] jacobianDerivative = calculatedConstraints._jacobianDerivatives;

            if (constraintVector == null || constraintVector.Length == 0)
            {
                continue;
            }

            float[,] jacobianReshaped =
                Utility.Reshape(jacobian, constraintVector.Length, -1);
            float[,] jacobianDerivativeReshaped =
                Utility.Reshape(jacobianDerivative, constraintVector.Length, -1);
            
            // Handle error if reshaping failed
            if (jacobianReshaped == null || jacobianDerivativeReshaped == null)
            {
                UnityEngine.Debug.LogError("Reshaping failed for one of the constraints. Check dimension matching.");
                continue;
            }

            constraints.AddRange(constraintVector);
            
            jacobians.Add(jacobianReshaped);
            jacobianDerivatives.Add(jacobianDerivativeReshaped);
        }

        float[] constraintsArray = constraints.ToArray();

        float[,] jacobiansStacked = Utility.VerticalStack(jacobians);
        float[,] jacobianDerivativesStacked = Utility.VerticalStack(jacobianDerivatives);

        return (constraintsArray, jacobiansStacked, jacobianDerivativesStacked);
    }

    // TODO: need to change the function header depending on the state implementation
    public abstract Vector3[] CalcActuationForces(float[] control);

    // Calculate forces that are not caused by constraints or actuation.
    public abstract Vector3[] CalcPassiveForces(Vector3[] pos, Vector3[] vel);

    // Factory abstract methods
    protected abstract void InitNumParticles();
    protected abstract void InitExternalForces();
    protected abstract void InitInvMasses();
}
