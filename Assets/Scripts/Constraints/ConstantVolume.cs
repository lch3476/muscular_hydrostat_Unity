using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

// Constant volume constraint for 3D cells.
// Converted from Python implementation: computes per-cell volumes from tetrahedra
// formed by an apex and triangulated faces, and the Jacobian and its time derivative.
public class ConstantVolume : Constraint
{
    private float[] initialVolumes;
    private float initialTotalVolume;

    public override void InitializeConstraint()
    {
        var cells = ModelBuilderObject.Cells;
        int cellCount = cells.Count;
        initialVolumes = new float[cellCount];

        for (int i = 0; i < cellCount; i++)
        {
            Cell cell = cells[i];
            initialVolumes[i] = cell.CalcVolume();
        }

        initialTotalVolume = initialVolumes.Sum();
    }

    public override (float[] constraints, float[,,] jacobians, float[,,] jacobianDerivative)
    CalculateConstraints()
    {
        if (initialVolumes == null)
        {
            Debug.LogError("ConstantVolume: initial volumes not initialized.");
            return (null, null, null);
        }

        Vector3[] position = ModelBuilderObject.GetPositions();
        Vector3[] velocity = ModelBuilderObject.Velocities ?? new Vector3[position.Length];

        var cells = ModelBuilderObject.Cells;
        int cellCount = cells.Count;
        int n = position.Length;

        float[] constraints = new float[cellCount];
        float[,,] jacobians = new float[cellCount, n, 3];
        float[,,] jacobianDerivative = new float[cellCount, n, 3];

        for (int i = 0; i < cellCount; i++)
        {
            Cell cell = cells[i];

            int apexIdx = -1;
            Vector3 apexPosition = Vector3.zero;
            Vector3 apexVelocity = Vector3.zero;
            if (cell.Vertices != null && cell.Vertices.Count > 0)
            {
                var apexVertex = cell.Vertices[0];
                apexIdx = ModelBuilderObject.Vertices.IndexOf(apexVertex);
                if (apexIdx >= 0 && apexIdx < n)
                {
                    apexPosition = position[apexIdx];
                    apexVelocity = velocity.Length > apexIdx ? velocity[apexIdx] : Vector3.zero;
                }
            }

            float volumeSum = 0f;

            Vector3[] localJacobian = new Vector3[n];
            Vector3[] localJacobianDerivative = new Vector3[n];

            // Iterate triangles via cell.Triangles to get GameObject -> global indices
            var triangles = cell.Triangles;
            foreach (var triangle in triangles)
            {
                if (triangle == null) continue;
                var triangleVertex1 = triangle.Item1;
                var triangleVertex2 = triangle.Item2;
                var triangleVertex3 = triangle.Item3;

                int triangleVertex1Index = ModelBuilderObject.Vertices.IndexOf(triangleVertex1);
                int triangleVertex2Index = ModelBuilderObject.Vertices.IndexOf(triangleVertex2);
                int triangleVertex3Index = ModelBuilderObject.Vertices.IndexOf(triangleVertex3);

                // validate triangle indices
                if (triangleVertex1Index < 0 ||
                triangleVertex2Index < 0 ||
                triangleVertex3Index < 0 ||
                triangleVertex1Index >= n ||
                triangleVertex2Index >= n ||
                triangleVertex3Index >= n)
                {
                    Debug.LogWarning($"ConstantVolume.CalculateConstraints: skipping invalid triangle indices in cell {i}, triangle {triangle}.");
                    continue;
                }

                Vector3 relativePosition0 = position[triangleVertex1Index] - apexPosition;
                Vector3 relativePosition1 = position[triangleVertex2Index] - apexPosition;
                Vector3 relativePosition2 = position[triangleVertex3Index] - apexPosition;

                Vector3 relativeVelocity0 = velocity[triangleVertex1Index] - apexVelocity;
                Vector3 relativeVelocity1 = velocity[triangleVertex2Index] - apexVelocity;
                Vector3 relativeVelocity2 = velocity[triangleVertex3Index] - apexVelocity;

                float tetrahedronVolume = Utility.Determinant3x3(relativePosition0, relativePosition1, relativePosition2);
                volumeSum += tetrahedronVolume;

                Vector3 cofactor0 = Vector3.Cross(relativePosition1, relativePosition2);
                Vector3 cofactor1 = Vector3.Cross(relativePosition2, relativePosition0);
                Vector3 cofactor2 = Vector3.Cross(relativePosition0, relativePosition1);

                Vector3 cofactorDerivative0 = Vector3.Cross(relativeVelocity1, relativePosition2) + Vector3.Cross(relativePosition1, relativeVelocity2);
                Vector3 cofactorDerivative1 = Vector3.Cross(relativeVelocity2, relativePosition0) + Vector3.Cross(relativePosition2, relativeVelocity0);
                Vector3 cofactorDerivative2 = Vector3.Cross(relativeVelocity0, relativePosition1) + Vector3.Cross(relativePosition0, relativeVelocity1);

                localJacobian[triangleVertex1Index] += cofactor0;
                localJacobian[triangleVertex2Index] += cofactor1;
                localJacobian[triangleVertex3Index] += cofactor2;

                localJacobianDerivative[triangleVertex1Index] += cofactorDerivative0;
                localJacobianDerivative[triangleVertex2Index] += cofactorDerivative1;
                localJacobianDerivative[triangleVertex3Index] += cofactorDerivative2;
            }

            constraints[i] = volumeSum - initialVolumes[i];

            Vector3 sumCofactor = Vector3.zero;
            Vector3 sumCofactorDerivative = Vector3.zero;
            for (int j = 0; j < n; j++)
            {
                if (localJacobian[j] != Vector3.zero)
                    sumCofactor += localJacobian[j];
                if (localJacobianDerivative[j] != Vector3.zero)
                    sumCofactorDerivative += localJacobianDerivative[j];
            }

            if (apexIdx >= 0)
            {
                localJacobian[apexIdx] = -sumCofactor;
                localJacobianDerivative[apexIdx] = -sumCofactorDerivative;
            }

            for (int j = 0; j < n; j++)
            {
                if (localJacobian[j] != Vector3.zero)
                {
                    jacobians[i, j, 0] = localJacobian[j].x;
                    jacobians[i, j, 1] = localJacobian[j].y;
                    jacobians[i, j, 2] = localJacobian[j].z;
                }
                if (localJacobianDerivative[j] != Vector3.zero)
                {
                    jacobianDerivative[i, j, 0] = localJacobianDerivative[j].x;
                    jacobianDerivative[i, j, 1] = localJacobianDerivative[j].y;
                    jacobianDerivative[i, j, 2] = localJacobianDerivative[j].z;
                }
            }
        }

        return (constraints, jacobians, jacobianDerivative);
    }

    public override string GenerateDataText()
    {
        return $"Initial Total Volume: {Math.Abs(initialTotalVolume)}\n" +
        $"Current Total Volume: {Math.Abs(ModelBuilderObject.CalcTotalVolume())}\n" +
        $"Volume Difference: {Math.Abs(ModelBuilderObject.CalcTotalVolume() - initialTotalVolume)}";
    }
}
