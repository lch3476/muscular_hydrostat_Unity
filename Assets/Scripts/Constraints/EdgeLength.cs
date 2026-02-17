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

        float[] edgeLengths = CalcEdgeLengths(positions);
        List<int> constrainedEdgeIndices = GetConstrainedEdgedIndices(edgeLengths);
        int numConstrained = constrainedEdgeIndices.Count;

        if (numConstrained == 0)
        {
            return (new float[0], new float[0, 0, 0], new float[0, 0, 0]);
        }

        float[] constraints = new float[numConstrained];
        float[,,] jacobians = new float[numConstrained, positions.Length, 3];
        float[,,] jacobiansDerivative = new float[numConstrained, positions.Length, 3];

        for (int constrainedIdx = 0; constrainedIdx < constrainedEdgeIndices.Count; constrainedIdx++)
        {
            int edgeIdx = constrainedEdgeIndices[constrainedIdx];
            float edgeLength = edgeLengths[edgeIdx];
            bool isBelowMin = edgeLength < minLength;

            // Use length - bound so the constraint is negative below min, positive above max.
            constraints[constrainedIdx] = isBelowMin ? (edgeLength - minLength) : (edgeLength - maxLength);

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
                Utility.VectorDivision((edgeVelocityDiff * edgeLengths[edgeIdx]) - (edgeReletiveVector * Vector3.Dot(edgeReletiveVector, edgeVelocityDiff)),
                    (float)Math.Pow(edgeLengths[edgeIdx], 2));

            jacobiansDerivative[constrainedIdx, edgePoint1Index, 0] = unitRelativeEdgeDerivative.x;
            jacobiansDerivative[constrainedIdx, edgePoint1Index, 1] = unitRelativeEdgeDerivative.y;
            jacobiansDerivative[constrainedIdx, edgePoint1Index, 2] = unitRelativeEdgeDerivative.z;

            jacobiansDerivative[constrainedIdx, edgePoint2Index, 0] = -unitRelativeEdgeDerivative.x;
            jacobiansDerivative[constrainedIdx, edgePoint2Index, 1] = -unitRelativeEdgeDerivative.y;
            jacobiansDerivative[constrainedIdx, edgePoint2Index, 2] = -unitRelativeEdgeDerivative.z;
        }

        return (constraints, jacobians, jacobiansDerivative);
    }

    float[] CalcEdgeLengths(Vector3[] positions)
    {
        List<Tuple<GameObject, GameObject>> edges = ModelBuilderObject.Edges;
        float[] edgeLengths = new float[edges.Count];
        for (int i = 0; i < edges.Count; i++)
        {
            int idx0 = ModelBuilderObject.Vertices.IndexOf(edges[i].Item1);
            int idx1 = ModelBuilderObject.Vertices.IndexOf(edges[i].Item2);
            Vector3 edgeVec = positions[idx0] - positions[idx1];
            edgeLengths[i] = edgeVec.magnitude;
        }
        return edgeLengths;
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
