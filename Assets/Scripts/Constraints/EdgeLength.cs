using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class EdgeLength : Constraint
{
    [SerializeField] float minLength = 0.0f;
    [SerializeField] float maxLength = float.PositiveInfinity;

    private float[] limits;

    public override void InitializeConstraint()
    {
        limits = new float[] { minLength, maxLength };
    }

    public override (float[] constraints, float[,,] jacobians, float[,,] jacobianDerivative) CalculateConstraints()
    {
        if (ModelBuilderObject == null)
        {
            Debug.LogError("ModelBuilder is null in EdgeLength constraint");
            return (new float[0], new float[0, 0, 0], new float[0, 0, 0]);
        }

        Vector3[] positions = ModelBuilderObject.GetPositions();
        Vector3[] velocities = ModelBuilderObject.Velocities;
        List<Tuple<GameObject, GameObject>> edges = ModelBuilderObject.Edges;

        if (velocities == null || velocities.Length != positions.Length)
        {
            Debug.LogWarning("EdgeLength: Velocities array is null or mismatched length; using zeros.");
            velocities = new Vector3[positions.Length];
        }

        float[] edgeLengths = CalcEdgeLengths();
        List<int> constrainedEdgeIndices = GetConstrainedEdgedIndices(edgeLengths);
        int numConstrained = constrainedEdgeIndices.Count;

        if (numConstrained == 0)
        {
            return (new float[0], new float[0, 0, 0], new float[0, 0, 0]);
        }

        float[] constraints = new float[numConstrained];

        // Calculate length differences from limits
        float[,] lengthDiffs = new float[numConstrained, 2];
        for (int i = 0; i < numConstrained; i++)
        {
            int edgeIdx = constrainedEdgeIndices[i];
            lengthDiffs[i, 0] = edgeLengths[edgeIdx] - limits[0];
            lengthDiffs[i, 1] = edgeLengths[edgeIdx] - limits[1];
        }

        // Find which limit each edge is closest to
        int[] minOrMax = new int[numConstrained];
        for (int i = 0; i < numConstrained; i++)
        {
            float absDiffMin = Mathf.Abs(lengthDiffs[i, 0]);
            float absDiffMax = Mathf.Abs(lengthDiffs[i, 1]);
            minOrMax[i] = (absDiffMin < absDiffMax) ? 0 : 1;
        }

        // Select constraint values: constraints = length_diffs[np.arange(num_constrained), min_or_max]
        for (int i = 0; i < numConstrained; i++)
        {
            constraints[i] = lengthDiffs[i, minOrMax[i]];
        }

        float[,,] jacobians = new float[numConstrained, positions.Length, 3];
        float[,,] jacobiansDerivative = new float[numConstrained, positions.Length, 3];

        for (int constrainedIdx = 0; constrainedIdx < constrainedEdgeIndices.Count; constrainedIdx++)
        {
            int edgeIdx = constrainedEdgeIndices[constrainedIdx];

            // Jacobain calculation for edge length constraint
            GameObject edgePoint1 = ModelBuilderObject.Edges[edgeIdx].Item1;
            GameObject edgePoint2 = ModelBuilderObject.Edges[edgeIdx].Item2;
            int edgePoint1Index = ModelBuilderObject.Vertices.IndexOf(edgePoint1);
            int edgePoint2Index = ModelBuilderObject.Vertices.IndexOf(edgePoint2);

            Vector3 edgeReletiveVector = positions[edgePoint1Index] - positions[edgePoint2Index];
            Vector3 edgeDir = edgeReletiveVector.normalized;

            jacobians[constrainedIdx, edgePoint1Index, 0] = edgeDir.x;
            jacobians[constrainedIdx, edgePoint1Index, 1] = edgeDir.y;
            jacobians[constrainedIdx, edgePoint1Index, 2] = edgeDir.z;

            jacobians[constrainedIdx, edgePoint2Index, 0] = -edgeDir.x;
            jacobians[constrainedIdx, edgePoint2Index, 1] = -edgeDir.y;
            jacobians[constrainedIdx, edgePoint2Index, 2] = -edgeDir.z;

            Vector3 edgePoint1Vel = velocities[edgePoint1Index];
            Vector3 edgePoint2Vel = velocities[edgePoint2Index];
            Vector3 edgeVelocityDiff = edgePoint1Vel - edgePoint2Vel;

            var unitRelativeEdgeDerivative = 
                ((edgeVelocityDiff * edgeLengths[edgeIdx]) - (edgeReletiveVector * Vector3.Dot(edgeReletiveVector, edgeVelocityDiff))) /
                (float)Math.Pow(edgeLengths[edgeIdx], 2);

            jacobiansDerivative[constrainedIdx, edgePoint1Index, 0] = unitRelativeEdgeDerivative.x;
            jacobiansDerivative[constrainedIdx, edgePoint1Index, 1] = unitRelativeEdgeDerivative.y;
            jacobiansDerivative[constrainedIdx, edgePoint1Index, 2] = unitRelativeEdgeDerivative.z;

            jacobiansDerivative[constrainedIdx, edgePoint2Index, 0] = -unitRelativeEdgeDerivative.x;
            jacobiansDerivative[constrainedIdx, edgePoint2Index, 1] = -unitRelativeEdgeDerivative.y;
            jacobiansDerivative[constrainedIdx, edgePoint2Index, 2] = -unitRelativeEdgeDerivative.z;
        }

        return (constraints, jacobians, jacobiansDerivative);
    }

    float[] CalcEdgeLengths()
    {
        List<float> edgeLengths = new List<float>(ModelBuilderObject.Edges.Count);
        foreach (var edge in ModelBuilderObject.Edges)
        {

            Vector3 edgeVec = edge.Item1.transform.position - edge.Item2.transform.position;
            edgeLengths.Add(edgeVec.magnitude);
        }
        return edgeLengths.ToArray();
    }

    List<int> GetConstrainedEdgedIndices(float[] edgeLengths)
    {
        if (edgeLengths.Length == 0)
        {
            Debug.LogWarning("EdgeLength.GetConstrainedEdgeIndices: edgeLengths array is empty");
            return new List<int>();
        }

        List<int> constrainedEdgeIndices = new List<int>();
        for (int i = 0; i < edgeLengths.Length; i++)
        {
            if (edgeLengths[i] > maxLength || edgeLengths[i] < minLength)
            {
                constrainedEdgeIndices.Add(i);
            }
        }

        return constrainedEdgeIndices;
    }
}
