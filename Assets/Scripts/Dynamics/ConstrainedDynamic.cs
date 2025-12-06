using UnityEngine;
using System.Collections.Generic;
using Mono.Cecil.Cil;

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
    ModelBuilder modelBuilder;
    public ModelBuilder ModelBuilder { get { return modelBuilder; } }

    [SerializeField] float constraintDampingRate = 50f;
    [SerializeField] float constraintSpringRate = 50f;

    [SerializeField] List<Constraint> constraints;
    protected float[] invMasses;
    protected Vector3[] externalForces;
    protected int numParticles;
    int statesNum;

    // TODO: implement after environment implementation
    // public void SetEnvironment(Environment environment, float[] state, List<Obstacle> obstacles);

    // TODO: Need to be mified depending on the constraint implementation
    private void Initconstraints(List<float> initialState)
    {
        foreach (Constraint constraint in constraints)
        {
            constraint.InitializeConstraint(this, initialState);
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
    private List<Vector3> CalcExplicitForces(Vector3[] actuationForces)
    {
        int count = externalForces.Length;
        List<Vector3> explicitForces = new List<Vector3>(new Vector3[count]);

        List<Vector3> passiveForces = CalcPassiveForces();
        Vector3 passiveForcesSum = Vector3.zero;
        foreach (Vector3 passiveForce in passiveForces)
        {
            passiveForcesSum += passiveForce;
        }
        
        for (int i = 0; i < count; i++)
        {
            explicitForces[i] = externalForces[i] + actuationForces[i] - passiveForcesSum;
        }
        return explicitForces;
    }

    // private Vector3[] CalcReactionForces(float[] state, Vector3[] explicitForces)
    // {
    //     if (constraints == null || constraints.Count == 0)
    //     {
    //         Vector3[] zeros = Utility.CreateInitializedArray<Vector3>(externalForces.Length, Vector3.zero);
    //         return zeros;
    //     }

    //     // Calculate constraints, jacobian, and jacobian derivative
    //     (float[] constraintsVec, float[,] jacobian, float[,] jacobianDerivative) = CalcConstraints();

    //     // invMasses: float[] of length n
    //     // jacobian: (m, n*3)
    //     // jacobian.T: (n*3, m)
    //     // invMasses[:, None] * jacobian.T: (n, m) * (n*3, m) -- need to expand invMasses to match shape

    //     // Compute front_matrix = jacobian @ (inv_masses[:, None] * jacobian.T)
    //     float[,] invMassesMatrix = Utility.Diagonalize(invMasses);
    //     float[,] jacobianT = Utility.Transpose(jacobian);   // (n*3, m)
    //     float[,] invMassesJacobianT = Utility.MatrixMultiply(jacobianT, invMasses); // (n*3, m)
    //     float[,] frontMatrix = Utility.MatrixMultiply(jacobian, invMassesJacobianT); // (m, m)

    //     // Regularization: front_matrix + eye * 1e-6
    //     Utility.AddIdentityInPlace(frontMatrix, 1e-6f);

    //     // dependent_array = -(
    //     //     djacobian_dt @ vel.ravel()
    //     //     + jacobian @ (inv_masses * explicit_forces.ravel())
    //     //     + constraint_damping_rate * jacobian @ vel.ravel()
    //     //     + constraint_spring_rate * constraints
    //     // )

    //     float[] velocitiesFlat = Utility.Flatten(modelBuilder.Velocities); // (n*3,)
    //     float[] explicitForcesFlat = Utility.Flatten(explicitForces); // (n*3,)

    //     float[] invMassesTimesExplicit = Utility.ElementwiseMultiply(invMasses, explicitForcesFlat); // (n*3,)
    //     float[] djacobianDtVel = Utility.MatrixMultiply(jacobianDerivative, velocitiesFlat); // (m,)
    //     float[] jacobianInvMassesExplicit = Utility.MatrixMultiply(jacobian, invMassesTimesExplicit); // (m,)
    //     float[] jacobianVel = Utility.MatrixMultiply(jacobian, velocitiesFlat); // (m,)

    //     float[] dependentArray = new float[djacobianDtVel.Length];
    //     for (int i = 0; i < dependentArray.Length; i++)
    //     {
    //         dependentArray[i] = -(
    //             djacobianDtVel[i]
    //             + jacobianInvMassesExplicit[i]
    //             + constraintDampingRate * jacobianVel[i]
    //             + constraintSpringRate * constraintsVec[i]
    //         );
    //     }

    //     // Solve for lagrange_multipliers: front_matrix x = dependent_array
    //     float[] lagrangeMultipliers = Utility.SolveLinearSystem(frontMatrix, dependentArray);

    //     // reaction_forces = jacobian.T @ lagrange_multipliers
    //     float[] reactionForcesFlat = Utility.MatrixMultiply(jacobianT, lagrangeMultipliers);

    //     // Convert flat array back to Vector3[]
    //     Vector3[] reactionForces = Utility.UnflattenToVector3Array(reactionForcesFlat);

    //     return reactionForces;
    // }

    // TODO replace with autograd calculation of constraints
    private (float[] constraints, float[,] jacobians, float[,] jacobianDerivatives) CalcConstraints()
    {
        List<float> constraints = new List<float>();
        List<float[,]> jacobians = new List<float[,]>();
        List<float[,]> jacobianDerivatives = new List<float[,]>();

        foreach (var constraint in this.constraints)
        {
            (float[] _constraints, float[,,] _jacobians, float[,,] _jacobianDerivatives) calculatedConstraints =
                constraint.CalculateConstraints(modelBuilder);
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
                Debug.LogError("Reshaping failed for one of the constraints. Check dimension matching.");
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
    public abstract List<Vector3> CalcActuationForces(List<float> control);

    // Calculate forces that are not caused by constraints or actuation.
    public abstract List<Vector3> CalcPassiveForces();

    // Factory abstract methods
    public abstract void InitNumParticles();
    public abstract void InitExternalForces();
    public abstract void InitInvMasses();
}
