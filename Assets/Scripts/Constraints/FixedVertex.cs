using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Constraint that fixes specific vertices to their initial positions.
// Each fixed vertex produces `dim` scalar constraints (for x,y,z).
public class FixedVertex : Constraint
{
    [SerializeField] int dim = 3;
    GameObject[] fixedVertices;
    public GameObject[] FixedVertices { get { return fixedVertices; } }
    private Vector3[] initialPositions;

    // Initialize with the initial state (flattened float list expected as [pos_flat, vel_flat]).
    public override void InitializeConstraint()
    {
        // Initialize fixedVertices array first
        fixedVertices = new GameObject[4];

        // TODO: flexible system utilizing serialized fields
        fixedVertices[0] = ModelBuilderObject.Vertices[0];
        fixedVertices[1] = ModelBuilderObject.Vertices[1];
        fixedVertices[2] = ModelBuilderObject.Vertices[2];
        fixedVertices[3] = ModelBuilderObject.Vertices[3];
        
        // Now get the initial positions
        initialPositions = GetFixedVertexPositions();
    }

    // Calculate constraints, jacobian and jacobian time derivative.
    public override (float[] constraints, float[,,] jacobians, float[,,] jacobianDerivative) CalculateConstraints()
    {
        Vector3[] positions = ModelBuilderObject.GetPositions();
        int numConstraints = fixedVertices.Length * dim;
        Vector3[] relativeVectors = new Vector3[fixedVertices.Length];
        for (int i = 0; i < fixedVertices.Length; i++)
        {
            relativeVectors[i] = positions[i] - initialPositions[i];
        }
        float[] constraints = Utility.Flatten(relativeVectors);

        // Create dimension indices array (equivalent to np.arange(dim))
        int[] dimIdx = Utility.Arange(dim);

        // Get fixed vertex indices from the vertices list
        List<GameObject> allVertices = ModelBuilderObject.Vertices;
        int[] fixedVertexIndices = new int[fixedVertices.Length];
        for (int i = 0; i < fixedVertices.Length; i++)
        {
            fixedVertexIndices[i] = allVertices.IndexOf(fixedVertices[i]);
        }

        // Create meshgrid (equivalent to np.meshgrid(dim_idx, self.fixed_vertices))
        (int[,] dimIDX, int[,] fixedIDX) = Utility.Meshgrid(dimIdx, fixedVertexIndices);

        // Initialize jacobians and jacobian time derivatives with zeros
        // Shape: (num_constraints, num_vertices, dim)
        int numVertices = positions.Length;
        float[,,] jacobians = new float[numConstraints, numVertices, dim];
        float[,,] djacDts = new float[numConstraints, numVertices, dim];

        // Set jacobian entries to 1 at specific indices
        // Equivalent to: jacobians[np.arange(num_constraints), fixed_IDX.flatten(), dim_IDX.flatten()] = 1
        int constraintIdx = 0;
        for (int i = 0; i < fixedVertexIndices.Length; i++)
        {
            for (int d = 0; d < dim; d++)
            {
                int vertexIdx = fixedIDX[i, d];
                int dimIndex = dimIDX[i, d];
                jacobians[constraintIdx, vertexIdx, dimIndex] = 1;
                constraintIdx++;
            }
        }

        return (constraints, jacobians, djacDts);
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
