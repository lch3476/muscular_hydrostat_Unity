using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;

public class Arm : ConstrainedDynamic
{
    // Not sure if states is necessary
    // states is the array that contains vertically stacked positions and velocities
    // List<Vector3> states;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Factory methods implementation
    protected override void InitNumParticles()
    {
        NumParticles = ModelBuilder.Vertices.Count;
    }

    protected override void InitNumControls()
    {
        if (ModelBuilder == null)
        {
            Debug.LogError("InitNumControls: ModelBuilder is null!");
            NumControls = 0;
            return;
        }
        
        if (ModelBuilder.Edges == null)
        {
            Debug.LogError("InitNumControls: ModelBuilder.Edges is null!");
            NumControls = 0;
            return;
        }
        
        NumControls = ModelBuilder.Edges.Count;
        Debug.Log($"InitNumControls: Set NumControls to {NumControls} (Edges count: {ModelBuilder.Edges.Count})");
    }

    protected override void InitExternalForces()
    {
        ExternalForces = new Vector3[ModelBuilder.Vertices.Count];
    }

    protected override void InitInvMasses()
    {
        int length = ModelBuilder.Masses.Count;
        InvMasses = new float[length * 3];
        for (int i = 0; i < length; i++)
        {
            float invMass = 1 / ModelBuilder.Masses[i];
            for (int j = 0; j < 3; j++)
            {
                InvMasses[i * 3 + j] = invMass;
            }
        }
    }
    
    protected override void InitNumStates()
    {
        NumStates = ModelBuilder.Vertices.Count * 6;
    }

    public override Vector3[] CalcPassiveForces(Vector3[] pos, Vector3[] vel)
    {
        int n = pos.Length;
        // passive edge forces per vertex
        Vector3[] passiveEdgeForces = CalcPassiveEdgeForces(pos, vel);
        // vertex damping: per-vertex scalar * vel
        List<float> dampingList = ModelBuilder.VertexDamping;
        Vector3[] vertexDampingForces = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float d = (dampingList != null && i < dampingList.Count) ? dampingList[i] : 0f;
            Vector3 df = d * vel[i];
            vertexDampingForces[i] = df;
        }

        // Sum contributions per vertex and return list of per-vertex passive forces
        Vector3[] totalPassive = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 edgeF = (i < passiveEdgeForces.Length) ? passiveEdgeForces[i] : Vector3.zero;
            Vector3 dampF = (i < vertexDampingForces.Length) ? vertexDampingForces[i] : Vector3.zero;
            totalPassive[i] = edgeF + dampF;
        }

        return Utility.MatrixMultiply<Vector3>(totalPassive, PassiveForceScale);
    }

    private Vector3[] CalcPassiveEdgeForces(Vector3[] pos, Vector3[] vel)
    {
        int n = pos.Length;
        Vector3[] edgeForces = new Vector3[n];

        var edges = ModelBuilder.Edges;
        var edgeDamping = ModelBuilder.EdgeDamping;

        int m = edges.Count;
        for (int e = 0; e < m; e++)
        {
            var edge = edges[e];
            // edge is Tuple<GameObject, GameObject>
            GameObject ga = edge.Item1;
            GameObject gb = edge.Item2;
            int i = ModelBuilder.Vertices.IndexOf(ga);
            int j = ModelBuilder.Vertices.IndexOf(gb);
            if (i < 0 || j < 0) continue;

            Vector3 edgeVector = pos[j] - pos[i];
            float len = edgeVector.magnitude;
            if (len <= Mathf.Epsilon) continue;
            Vector3 edgeUnit = edgeVector / len;

            Vector3 relativeVelocity = vel[j] - vel[i];
            Vector3 edgeVelocity = Vector3.Dot(edgeUnit, relativeVelocity) * edgeUnit;

            float dampingRate = (edgeDamping != null && e < edgeDamping.Count) ? edgeDamping[e] : 0f;
            Vector3 edgeDampForce = dampingRate * edgeVelocity;

            edgeForces[i] += -edgeDampForce;
            edgeForces[j] += edgeDampForce;
        }

        Vector3[] result = new Vector3[n];
        for (int ii = 0; ii < n; ii++) result[ii] = edgeForces[ii];

        return Utility.MatrixMultiply<Vector3>(result, PassiveEdgeForceScale);
    }

    // Converted from Python `_calc_actuation_forces`.
    // Computes per-vertex actuation forces from per-edge muscle forces (`control`).
    public override Vector3[] CalcActuationForces(float[] control)
    {
        Vector3[] pos = ModelBuilder.GetPositions();
        int n = pos.Length;
        Vector3[] edgeForces = new Vector3[n];

        var edges = ModelBuilder.Edges;
        int m = edges.Count;


        for (int e = 0; e < m; e++)
        {
            var edge = edges[e];
            GameObject p1 = edge.Item1;
            GameObject p2 = edge.Item2;

            Vector3 edgeVector = p2.transform.position - p1.transform.position;
            float vectorLength = edgeVector.magnitude;
            if (vectorLength <= Mathf.Epsilon) continue;
            edgeVector = edgeVector / vectorLength;

            float muscle = (control != null && e < control.Length) ? control[e] : 0f;
            Vector3 edgeForce = edgeVector * muscle;

            int i = ModelBuilder.Vertices.IndexOf(p1);
            int j = ModelBuilder.Vertices.IndexOf(p2);

            if (i < 0 || j < 0) 
            {
                Debug.LogWarning($"Edge {e}: vertices not found. p1='{p1?.name}' (index={i}), p2='{p2?.name}' (index={j}). " +
                    $"p1==null? {p1 == null}, p2==null? {p2 == null}. " +
                    $"Total vertices in list: {ModelBuilder.Vertices.Count}");
                continue;
            }
            // Apply equal and opposite forces to the two vertices
            edgeForces[i] += edgeForce;
            edgeForces[j] -= edgeForce;
        }

        return Utility.MatrixMultiply<Vector3>(edgeForces, ActuationForceScale);
    }
}
