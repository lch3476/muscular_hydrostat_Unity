using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using System;

[DefaultExecutionOrder(-100)]
public abstract class ModelBuilder : MonoBehaviour
{
    // Class variables
    [SerializeField] int cellCount;
    public int CellCount { get { return cellCount; } }
    [SerializeField] Cell cellPrefab;
    public Cell CellPrefab { get { return cellPrefab; } }
    List<Cell> cells = new List<Cell>();
    public List<Cell> Cells { get { return cells; } }

    List<GameObject> vertices = new List<GameObject>();
    public List<GameObject> Vertices { get { return vertices; } }
    Vector3[] velocities;
    public Vector3[] Velocities { get { return velocities; } set { velocities = value;} }
    List<Tuple<GameObject, GameObject>> edges = new List<Tuple<GameObject, GameObject>>();
    public List<Tuple<GameObject, GameObject>> Edges { get { return edges; } }
    List<Tuple<GameObject, GameObject, GameObject, GameObject>> faces =
        new List<Tuple<GameObject, GameObject, GameObject, GameObject>>();
    public List<Tuple<GameObject, GameObject, GameObject, GameObject>> Faces { get { return faces; } }
    List<float> masses;
    public List<float> Masses { get { return masses; } set { masses = value; } }
    List<float> vertexDamping;
    public List<float> VertexDamping { get { return vertexDamping; } set { vertexDamping = value; } }
    List<float> edgeDamping;
    public List<float> EdgeDamping { get { return edgeDamping; } set { edgeDamping = value; } }

    void Awake() {
        Initialize();
    }

    void Start()
    {
        PrintDebugInfo();
    }

    // void Update()
    // {
    //     PrintDebugInfo();   
    // }

    // Methods
    public void Initialize()
    {
        CreateModel();
        InitVertices();
        InitVelocities();
        InitMasses();
        InitVertexDamping();
        InitEdges();
        InitFaces();
        InitEdgeDamping();
    }

    public void PrintDebugInfo()
    {
        Debug.Log("=== ModelBuilder Debug Info ===");
        Debug.Log($"Total Vertices: {vertices.Count}");
        Debug.Log($"Total Edges: {edges.Count}");
        
        Debug.Log("\n--- Vertices List ---");
        for (int i = 0; i < vertices.Count; i++)
        {
            GameObject v = vertices[i];
            Debug.Log($"Vertex[{i}]: {v?.name ?? "NULL"} (InstanceID: {v?.GetInstanceID().ToString() ?? "N/A"})");
        }
        
        Debug.Log("\n--- Edges List ---");
        for (int e = 0; e < edges.Count; e++)
        {
            var edge = edges[e];
            GameObject p1 = edge.Item1;
            GameObject p2 = edge.Item2;
            int i = vertices.IndexOf(p1);
            int j = vertices.IndexOf(p2);
            
            Debug.Log($"Edge[{e}]: {p1?.name ?? "NULL"} (ID:{p1?.GetInstanceID().ToString() ?? "N/A"}) -> " +
                     $"{p2?.name ?? "NULL"} (ID:{p2?.GetInstanceID().ToString() ?? "N/A"}) | " +
                     $"Indices: [{i}, {j}]");
        }
        Debug.Log("=== End Debug Info ===\n");
    }

    // Abstract
    protected abstract void CreateModel();
    protected abstract void InitVertices();
    protected abstract void InitVelocities();
    protected abstract void InitEdges();
    protected abstract void InitFaces();
    protected abstract void InitMasses();
    protected abstract void InitVertexDamping();
    protected abstract void InitEdgeDamping();

    public Vector3[] GetPositions()
    {
        Vector3[] positions = new Vector3[vertices.Count];
        for (int i = 0; i < positions.Length; ++i)
        {
            positions[i] = vertices[i].transform.position;
        }
        return positions;
    }

    // Convert current vertex positions and velocities into a flattened state array.
    public float[] GetState()
    {
        Vector3[] positions = GetPositions();
        int n = positions.Length;

        if (velocities == null || velocities.Length != n)
        {
            velocities = new Vector3[n];
        }

        float[] posFlat = Utility.Flatten(positions);
        float[] velFlat = Utility.Flatten(velocities);

        float[] state = new float[posFlat.Length + velFlat.Length];
        Array.Copy(posFlat, 0, state, 0, posFlat.Length);
        Array.Copy(velFlat, 0, state, posFlat.Length, velFlat.Length);

        return state;
    }

    // Set vertex positions from a flattened state array.
    // Expects layout produced by GetState().
    // TODO: create State class to represent this data more cleanly.
    private void SetPositionFromState(float[] state)
    {
        if (state == null) return;
        int n = vertices.Count;
        int comp = 3;
        int expected = n * comp * 2;
        if (state.Length != expected)
        {
            throw new ArgumentException($"State length mismatch. Expected {expected}, got {state.Length}.");
        }

        for (int i = 0; i < n; ++i)
        {
            int baseIdx = i * comp;
            Vector3 pos = new Vector3(state[baseIdx + 0], state[baseIdx + 1], state[baseIdx + 2]);
            vertices[i].transform.position = pos;
        }
    }

    // Set vertex velocities from a flattened state array.
    // Expects layout produced by GetState().
    // TODO: create State class to represent this data more cleanly.
    private void SetVelocityFromState(float[] state)
    {
        if (state == null) return;
        int n = vertices.Count;
        int comp = 3;
        int expected = n * comp * 2;
        if (state.Length != expected)
        {
            throw new ArgumentException($"State length mismatch. Expected {expected}, got {state.Length}.");
        }

        int velOffset = n * comp;
        if (velocities == null || velocities.Length != n)
        {
            velocities = new Vector3[n];
        }
        for (int i = 0; i < n; ++i)
        {
            int baseIdx = velOffset + i * comp;
            velocities[i] = new Vector3(state[baseIdx + 0], state[baseIdx + 1], state[baseIdx + 2]);
        }
    }

    /// <summary>
    /// Convenience: apply both positions and velocities from state.
    /// </summary>
    public void ParseState(float[] state)
    {
        SetPositionFromState(state);
        SetVelocityFromState(state);
    }
}
