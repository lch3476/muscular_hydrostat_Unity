using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Constraint that fixes specific vertices to their initial positions.
public class FixedVertex : Constraint
{
    GameObject[] fixedVertices;
    public GameObject[] FixedVertices { get { return fixedVertices; } }
    private Vector3[] initialPositions;

    public override void InitializeConstraint()
    {
        // TODO: flexible vertice selection (currently hardcoded to first 4 vertices)
        fixedVertices = new GameObject[4];
        fixedVertices[0] = ModelBuilderObject.Vertices[0];
        fixedVertices[1] = ModelBuilderObject.Vertices[1];
        fixedVertices[2] = ModelBuilderObject.Vertices[2];
        fixedVertices[3] = ModelBuilderObject.Vertices[3];
        
        initialPositions = GetFixedVertexPositions();
    }

    public override (float[] constraints, float[,,] jacobians, float[,,] jacobianDerivative) CalculateConstraints()
    {
        Vector3[] positions = ModelBuilderObject.GetPositions();
        int numConstraints = fixedVertices.Length * Simulator.DIM;
        Vector3[] relativeVectors = new Vector3[fixedVertices.Length];

        for (int i = 0; i < fixedVertices.Length; i++)
            relativeVectors[i] = positions[i] - initialPositions[i];

        float[] constraints = Utility.Flatten(relativeVectors);

        int[] dimIdx = Utility.Arange(Simulator.DIM);

        List<GameObject> Vertices = ModelBuilderObject.Vertices;
        int[] fixedVertexIndices = new int[fixedVertices.Length];
        for (int i = 0; i < fixedVertices.Length; i++)
            fixedVertexIndices[i] = Vertices.IndexOf(fixedVertices[i]);

        (int[,] MeshgridDimIdx, int[,] MeshgridDimfixedIdX) = Utility.Meshgrid(dimIdx, fixedVertexIndices);

        // Shape: (num_constraints, num_vertices, dim)
        float[,,] jacobians = new float[numConstraints, positions.Length, Simulator.DIM];
        float[,,] jacobiansDerivatives = new float[numConstraints, positions.Length, Simulator.DIM];

        // Set jacobian entries to 1 at specific indices
        int constraintIdx = 0;
        for (int i = 0; i < fixedVertexIndices.Length; i++)
        {
            for (int d = 0; d < Simulator.DIM; d++)
            {
                int vertexIdx = MeshgridDimfixedIdX[i, d];
                int dimIndex = MeshgridDimIdx[i, d];
                jacobians[constraintIdx, vertexIdx, dimIndex] = 1;
                constraintIdx++;
            }
        }

        return (constraints, jacobians, jacobiansDerivatives);
    }

    private Vector3[] GetFixedVertexPositions()
    {
        Vector3[] positions = new Vector3[fixedVertices.Length];
        for (int i = 0; i < fixedVertices.Length; i++)
        {
            if (fixedVertices[i] != null)
            {
                positions[i] = fixedVertices[i].transform.position;
            }
            else
            {
                positions[i] = Vector3.zero;
                Debug.LogWarning("Fixed vertex at index " + i + " is null.");
            }
        }

        return positions;
    }   
}
