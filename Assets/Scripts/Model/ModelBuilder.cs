using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using System;

public abstract class ModelBuilder : MonoBehaviour
{
    // Class variables
    [SerializeField] protected int cellCount;
    [SerializeField] protected Cell cellPrefab;
    protected List<Cell> cells = new List<Cell>();

    protected List<GameObject> vertices = new List<GameObject>();
    public List<GameObject> Vertices { get { return vertices; } }
    protected Vector3[] velocities;
    public Vector3[] Velocities { get { return velocities; } set { velocities = value;} }
    protected List<Tuple<GameObject, GameObject>> edges = new List<Tuple<GameObject, GameObject>>();
    public List<Tuple<GameObject, GameObject>> Edges { get { return edges; } }
    protected List<Tuple<GameObject, GameObject, GameObject, GameObject>> faces =
        new List<Tuple<GameObject, GameObject, GameObject, GameObject>>();
    public List<Tuple<GameObject, GameObject, GameObject, GameObject>> Faces { get { return faces; } }
    protected List<float> masses;
    public List<float> Masses { get { return masses; } }
    protected List<float> vertexDamping;
    public List<float> VertexDamping { get { return vertexDamping; } }
    protected List<float> edgeDamping;
    public List<float> EdgeDamping { get { return edgeDamping; } }

    // Methods
    public Vector3[] GetPositions()
    {
        Vector3[] positions = new Vector3[vertices.Count];
        for (int i = 0; i < positions.Length; ++i)
        {
            positions[i] = vertices[i].transform.position;
        }
        return positions;
    }

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

    // Abstract
    public abstract void CreateModel();
    protected abstract void InitVertices();
    protected abstract void InitVelocities();
    protected abstract void InitEdges();
    protected abstract void InitFaces();
    protected abstract void InitMasses();
    protected abstract void InitVertexDamping();
    protected abstract void InitEdgeDamping();
}
