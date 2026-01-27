using UnityEngine;
using System.Collections.Generic;

// Constraint that fixes specific vertices to their initial positions.
// Each fixed vertex produces `dim` scalar constraints (for x,y,z).
public class FixedVertex : Constraint
{
    [SerializeField] int[] fixedVertices = new int[0];

    private Vector3[] initialPositions;

    // Initialize with the initial state (flattened float list expected as [pos_flat, vel_flat]).
    public override void InitializeConstraint()
    {
        Vector3[] pos;
        if (ModelBuilderObject != null)
        {
            float[] stateArr = ModelBuilderObject.GetState();
            if (stateArr != null && stateArr.Length > 0)
            {
                var posvel = Utility.StateToPosVel(stateArr);
                float[,] posMat = posvel.Item1;
                int rows = posMat.GetLength(0);
                pos = new Vector3[rows];
                for (int i = 0; i < rows; i++) pos[i] = new Vector3(posMat[i, 0], posMat[i, 1], posMat[i, 2]);
            }
            else
            {
                pos = ModelBuilderObject.GetPositions();
            }
        }
        else
        {
            pos = new Vector3[0];
        }

        initialPositions = new Vector3[fixedVertices.Length];
        for (int i = 0; i < fixedVertices.Length; i++)
        {
            int idx = fixedVertices[i];
            if (idx >= 0 && idx < pos.Length) initialPositions[i] = pos[idx];
            else initialPositions[i] = Vector3.zero;
        }

        Debug.Log($"FixedVertex.InitializeConstraint: fixed count={fixedVertices.Length}");
    }

    // Calculate constraints, jacobian and jacobian time derivative.
    public override (float[] constraints, float[,,] jacobians, float[,,] jacobianDerivative) CalculateConstraints()
    {
        Vector3[] pos = ModelBuilderObject != null ? ModelBuilderObject.GetPositions() : new Vector3[0];
        Vector3[] vel = ModelBuilderObject != null ? ModelBuilderObject.Velocities : Utility.CreateInitializedArray<Vector3>(pos.Length, Vector3.zero);

        int dim = 3;
        int numFixed = fixedVertices.Length;
        int numConstraints = numFixed * dim;
        int n = pos.Length;

        float[] constraints = new float[numConstraints];
        float[,,] jacobians = new float[numConstraints, n, dim];
        float[,,] djac_dts = new float[numConstraints, n, dim]; // zero by default

        for (int f = 0; f < numFixed; f++)
        {
            int vertexIndex = fixedVertices[f];
            Vector3 rel = Vector3.zero;
            if (vertexIndex >= 0 && vertexIndex < pos.Length && initialPositions != null && f < initialPositions.Length)
            {
                rel = pos[vertexIndex] - initialPositions[f];
            }

            // flatten into constraints: [v0.x, v0.y, v0.z, v1.x, v1.y, v1.z, ...]
            for (int d = 0; d < dim; d++)
            {
                int cidx = f * dim + d;
                constraints[cidx] = (d == 0) ? rel.x : (d == 1) ? rel.y : rel.z;
                if (vertexIndex >= 0 && vertexIndex < n)
                {
                    jacobians[cidx, vertexIndex, d] = 1f;
                }
            }
        }

        return (constraints, jacobians, djac_dts);
    }
}
