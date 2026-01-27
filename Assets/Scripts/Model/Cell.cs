using UnityEngine;
using Unity.VisualScripting;

using System.Collections.Generic;
using System.Linq;
using System;

// equivalent to Cell3D class in structure.py
// also this class takes some variables of CubicArmBuilder class
// including cube_vertices, cube_edges, and cube_faces

public class Cell : MonoBehaviour
{
    // Class variables
    // [SerializeField] float width = 0.5f;
    // [SerializeField] float length = 0.5f;
    // [SerializeField] float height = 0.5f;
    // [SerializeField] float scale = 0.05f;

    // Spheres that work as vertices
    GameObject sphere1;
    public GameObject Sphere1 { get { return sphere1; } set { sphere1 = value; } }
    GameObject sphere2;
    public GameObject Sphere2 { get { return sphere2; } set { sphere2 = value; } }
    GameObject sphere3;
    public GameObject Sphere3 { get { return sphere3; } set { sphere3 = value; } }
    GameObject sphere4;
    public GameObject Sphere4 { get { return sphere4; } set { sphere4 = value; } }
    GameObject sphere5;
    public GameObject Sphere5 { get { return sphere5; } set { sphere5 = value; } }
    GameObject sphere6;
    public GameObject Sphere6 { get { return sphere6; } set { sphere6 = value; } }
    GameObject sphere7;
    public GameObject Sphere7 { get { return sphere7; } set { sphere7 = value; } }
    GameObject sphere8;
    public GameObject Sphere8 { get { return sphere8; } set { sphere8 = value; } }
    private List<LineRenderer> lines = new List<LineRenderer>();

    // Geometric properties
    private List<GameObject> vertices = new List<GameObject>();
    public List<GameObject> Vertices { get { return vertices; } }
    private List<Tuple<GameObject, GameObject>> edges = new List<Tuple<GameObject, GameObject>>();
    public List<Tuple<GameObject, GameObject>> Edges { get { return edges; } }
    private List<Tuple<GameObject, GameObject, GameObject, GameObject>> faces =
        new List<Tuple<GameObject, GameObject, GameObject, GameObject>>();
    public List<Tuple<GameObject, GameObject, GameObject, GameObject>> Faces { get { return faces; } }
    private float[] masses;
    public float[] Masses { get { return masses; } }
    private float[] vertexDamping;
    public float[] VertexDamping { get { return vertexDamping; } }
    private float[] edgeDamping;
    public float[] EdgeDamping { get { return edgeDamping; } }
    private List<Tuple<GameObject, GameObject, GameObject>> triangles =
        new List<Tuple<GameObject, GameObject, GameObject>>();
    public List<Tuple<GameObject, GameObject, GameObject>> Triangles { get { return triangles; } }

    // List<int[]> cubeEdgesIndices = new List<int[]>
    // {
    //     new int[] {0, 1},
    //     new int[] {1, 2},
    //     new int[] {2, 3},
    //     new int[] {3, 0},
    //     new int[] {0, 4},
    //     new int[] {1, 5},
    //     new int[] {2, 6},
    //     new int[] {3, 7},
    //     new int[] {0, 6},
    //     new int[] {1, 7},
    //     new int[] {2, 4},
    //     new int[] {3, 5},
    //     new int[] {4, 5},
    //     new int[] {5, 6},
    //     new int[] {6, 7},
    //     new int[] {7, 4}
    // };

    // List<int[]> cubeFacesIndices = new List<int[]>
    // {
    //     new int[] {0, 3, 2, 1},
    //     new int[] {0, 1, 5, 4},
    //     new int[] {1, 2, 6, 5},
    //     new int[] {2, 3, 7, 6},
    //     new int[] {3, 0, 4, 7},
    //     new int[] {4, 5, 6, 7}
    // };

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GetSpheres();
        SetUpLineRenderers();
    }

    // Awake runs immediately when the object is instantiated (before Start).
    // Ensure arrays that other classes may read immediately after Instantiate are initialized here.
    void Awake()
    {
        // populate sphere references early so other builders can read them immediately after Instantiate
        GetSpheres();

        Initialize();
    }

    // Update is called once per frame
    void Update()
    {
        DrawLineBetweenSpheres();
    }

    // Set the top vertices (Sphere1-4) to reference specific GameObjects
    // Used to share vertices between adjacent cells
    public void SetTopVertices(GameObject s1, GameObject s2, GameObject s3, GameObject s4)
    {
        sphere1 = s1;
        sphere2 = s2;
        sphere3 = s3;
        sphere4 = s4;
        
        // Rebuild vertices, edges, and faces lists with new references
        RebuildGeometry();
    }

    // Rebuild vertices, edges, and faces after vertex references change
    private void RebuildGeometry()
    {
        vertices.Clear();
        edges.Clear();
        faces.Clear();
        
        InitVertices();
        InitEdges();
        InitFaces();
        InitTriangulatedFaces();
    }

    void GetSpheres()
    {
        Transform sphere1Transform = this.transform.Find("Sphere1");
        if (sphere1Transform != null) sphere1 = sphere1Transform.gameObject;

        Transform sphere2Transform = this.transform.Find("Sphere2");
        if (sphere2Transform != null) sphere2 = sphere2Transform.gameObject;

        Transform sphere3Transform = this.transform.Find("Sphere3");
        if (sphere3Transform != null) sphere3 = sphere3Transform.gameObject;

        Transform sphere4Transform = this.transform.Find("Sphere4");
        if (sphere4Transform != null) sphere4 = sphere4Transform.gameObject;

        Transform sphere5Transform = this.transform.Find("Sphere5");
        if (sphere5Transform != null) sphere5 = sphere5Transform.gameObject;

        Transform sphere6Transform = this.transform.Find("Sphere6");
        if (sphere6Transform != null) sphere6 = sphere6Transform.gameObject;

        Transform sphere7Transform = this.transform.Find("Sphere7");
        if (sphere7Transform != null) sphere7 = sphere7Transform.gameObject;

        Transform sphere8Transform = this.transform.Find("Sphere8");
        if (sphere8Transform != null) sphere8 = sphere8Transform.gameObject;
    }

    // void CreateSpheres()
    // {
    //     // GameObject cell = GameObject.Find("");
    //     sphere1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    //     if (sphere1 != null)
    //     {
    //         sphere1.name = "sphere1";
    //         sphere1.transform.localPosition = new Vector3(0, 0, 0);
    //         sphere1.transform.localScale = new Vector3(scale, scale, scale);
    //         sphere1.transform.parent = this.transform;
    //     }

    //     sphere2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    //     if (sphere2 != null)
    //     {
    //         sphere2.name = "sphere2";
    //         sphere2.transform.localPosition = new Vector3(width, 0, 0);
    //         sphere2.transform.localScale = new Vector3(scale, scale, scale);
    //         sphere2.transform.parent = this.transform;
    //     }

    //     sphere3 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    //     if (sphere3 != null)
    //     {
    //         sphere3.name = "sphere3";
    //         sphere3.transform.localPosition = new Vector3(0, length, 0);
    //         sphere3.transform.localScale = new Vector3(scale, scale, scale);
    //         sphere3.transform.parent = this.transform;
    //     }

    //     sphere4 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    //     if (sphere4 != null)
    //     {
    //         sphere4.name = "sphere4";
    //         sphere4.transform.localPosition = new Vector3(width, length, 0);
    //         sphere4.transform.localScale = new Vector3(scale, scale, scale);
    //         sphere4.transform.parent = this.transform;
    //     }

    //     sphere5 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    //     if (sphere5 != null)
    //     {
    //         sphere5.name = "sphere5";
    //         sphere5.transform.localPosition = new Vector3(0, 0, height);
    //         sphere5.transform.localScale = new Vector3(scale, scale, scale);
    //         sphere5.transform.parent = this.transform;
    //     }

    //     sphere6 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    //     if (sphere6 != null)
    //     {
    //         sphere6.name = "sphere6";
    //         sphere6.transform.localPosition = new Vector3(width, 0, height);
    //         sphere6.transform.localScale = new Vector3(scale, scale, scale);
    //         sphere6.transform.parent = this.transform;
    //     }

    //     sphere7 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    //     if (sphere7 != null)
    //     {
    //         sphere7.name = "sphere7";
    //         sphere7.transform.localPosition = new Vector3(0, length, height);
    //         sphere7.transform.localScale = new Vector3(scale, scale, scale);
    //         sphere7.transform.parent = this.transform;
    //     }

    //     sphere8 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    //     if (sphere8 != null)
    //     {
    //         sphere8.name = "sphere8";
    //         sphere8.transform.localPosition = new Vector3(width, length, height);
    //         sphere8.transform.localScale = new Vector3(scale, scale, scale);
    //         sphere8.transform.parent = this.transform;
    //     }
    // }

    void SetUpLineRenderers()
    {
        for (int i = 0; i < 12; i++)
        {
            GameObject lineObj = new GameObject("Line" + i);
            lineObj.transform.parent = this.transform;

            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.positionCount = 2; // two points
            line.widthMultiplier = 0.01f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = Color.white;
            line.endColor = Color.white;

            lines.Add(line);
        }
    }

    void DrawLineBetweenSpheres()
    {
        // Check if spheres are active, but allow shared spheres from other cells
        if (sphere1 != null && sphere2 != null)
        {
            lines[0].SetPosition(0, sphere1.transform.position);
            lines[0].SetPosition(1, sphere2.transform.position);
            lines[0].enabled = true;
        }
        else
        {
            lines[0].enabled = false;
            if (sphere1 == null)
            {
                Debug.LogError($"{gameObject.name}: sphere1 is null");
            }
            else if (sphere2 == null)
            {
                Debug.LogError($"{gameObject.name}: sphere2 is null");
            }
        }

        if (sphere1 != null && sphere4 != null)
        {
            lines[1].SetPosition(0, sphere1.transform.position);
            lines[1].SetPosition(1, sphere4.transform.position);
            lines[1].enabled = true;
        }
        else
        {
            lines[1].enabled = false;
            if (sphere1 == null)
            {
                Debug.LogError($"{gameObject.name}: sphere1 is null");
            }
            else if (sphere4 == null)
            {
                Debug.LogError($"{gameObject.name}: sphere4 is null");
            }
        }

        if (sphere1 != null && sphere5 != null)
        {
            lines[2].SetPosition(0, sphere1.transform.position);
            lines[2].SetPosition(1, sphere5.transform.position);
            lines[2].enabled = true;
        }
        else
        {
            lines[2].enabled = false;
            if (sphere1 == null)
            {
                Debug.LogError($"{gameObject.name}: sphere1 is null");
            }
            else if (sphere5 == null)
            {
                Debug.LogError($"{gameObject.name}: sphere5 is null");
            }
        }

        if (sphere2 != null && sphere3 != null)
        {
            lines[3].SetPosition(0, sphere2.transform.position);
            lines[3].SetPosition(1, sphere3.transform.position);
            lines[3].enabled = true;
        }
        else
        {
            lines[3].enabled = false;
            if (sphere2 == null)
            {
                Debug.LogError($"{gameObject.name}: sphere2 is null");
            }
            else if (sphere3 == null)
            {
                Debug.LogError($"{gameObject.name}: sphere3 is null");
            }
        }

        if (sphere2 != null && sphere6 != null)
        {
            lines[4].SetPosition(0, sphere2.transform.position);
            lines[4].SetPosition(1, sphere6.transform.position);
            lines[4].enabled = true;
        }
        else
        {
            lines[4].enabled = false;
            if (sphere2 == null)
            {
                Debug.LogError($"{gameObject.name}: sphere2 is null");
            }
            else if (sphere6 == null)
            {
                Debug.LogError($"{gameObject.name}: sphere6 is null");
            }
        }

        if (sphere3 != null && sphere4 != null)
        {
            lines[5].SetPosition(0, sphere3.transform.position);
            lines[5].SetPosition(1, sphere4.transform.position);
            lines[5].enabled = true;
        }
        else
        {
            lines[5].enabled = false;
        }

        if (sphere3 != null && sphere7 != null)
        {
            lines[6].SetPosition(0, sphere3.transform.position);
            lines[6].SetPosition(1, sphere7.transform.position);
            lines[6].enabled = true;
        }
        else
        {
            lines[6].enabled = false;
        }

        if (sphere4 != null && sphere8 != null)
        {
            lines[7].SetPosition(0, sphere4.transform.position);
            lines[7].SetPosition(1, sphere8.transform.position);
            lines[7].enabled = true;
        }
        else
        {
            lines[7].enabled = false;
        }

        if (sphere5 != null && sphere6 != null)
        {
            lines[8].SetPosition(0, sphere5.transform.position);
            lines[8].SetPosition(1, sphere6.transform.position);
            lines[8].enabled = true;
        }
        else
        {
            lines[8].enabled = false;
        }

        if (sphere5 != null && sphere8 != null)
        {
            lines[9].SetPosition(0, sphere5.transform.position);
            lines[9].SetPosition(1, sphere8.transform.position);
            lines[9].enabled = true;
        }
        else
        {
            lines[9].enabled = false;
        }

        if (sphere6 != null && sphere7 != null)
        {
            lines[10].SetPosition(0, sphere6.transform.position);
            lines[10].SetPosition(1, sphere7.transform.position);
            lines[10].enabled = true;
        }
        else
        {
            lines[10].enabled = false;
        }

        if (sphere7 != null && sphere8 != null)
        {
            lines[11].SetPosition(0, sphere7.transform.position);
            lines[11].SetPosition(1, sphere8.transform.position);
            lines[11].enabled = true;
        }
        else
        {
            lines[11].enabled = false;
        }
    }

    void Initialize()
    {

        InitVertices();
        InitEdges();
        if (masses == null)
        {
            masses = Utility.CreateInitializedArray<float>(vertices.Count, 1f / vertices.Count);
        }
        if (vertexDamping == null)
        {
            vertexDamping = Utility.CreateInitializedArray<float>(vertices.Count, 1f / vertices.Count);
        }
        if (edgeDamping == null)
        {
            edgeDamping = Utility.CreateInitializedArray<float>(edges.Count, 1f);
        }
        InitFaces();
        InitTriangulatedFaces();
    }

    void InitVertices()
    {
        vertices.Add(sphere1);
        vertices.Add(sphere2);
        vertices.Add(sphere3);
        vertices.Add(sphere4);
        vertices.Add(sphere5);
        vertices.Add(sphere6);
        vertices.Add(sphere7);
        vertices.Add(sphere8);
    }

    void InitEdges()
    {
        edges.Add(Tuple.Create(Sphere1, Sphere2));
        edges.Add(Tuple.Create(Sphere2, Sphere3));
        edges.Add(Tuple.Create(Sphere3, Sphere4));
        edges.Add(Tuple.Create(Sphere4, Sphere1));
        edges.Add(Tuple.Create(Sphere1, Sphere5));
        edges.Add(Tuple.Create(Sphere2, Sphere6));
        edges.Add(Tuple.Create(Sphere3, Sphere7));
        edges.Add(Tuple.Create(Sphere4, Sphere8));
        edges.Add(Tuple.Create(Sphere1, Sphere7));
        edges.Add(Tuple.Create(Sphere2, Sphere8));
        edges.Add(Tuple.Create(Sphere3, Sphere5));
        edges.Add(Tuple.Create(Sphere4, Sphere6));
        edges.Add(Tuple.Create(Sphere5, Sphere6));
        edges.Add(Tuple.Create(Sphere6, Sphere7));
        edges.Add(Tuple.Create(Sphere7, Sphere8));
        edges.Add(Tuple.Create(Sphere8, Sphere5));
    }

    void InitFaces()
    {
        faces.Add(Tuple.Create(Sphere1, Sphere4, Sphere3, Sphere2));
        faces.Add(Tuple.Create(Sphere1, Sphere2, Sphere6, Sphere5));
        faces.Add(Tuple.Create(Sphere2, Sphere3, Sphere7, Sphere6));
        faces.Add(Tuple.Create(Sphere3, Sphere4, Sphere8, Sphere7));
        faces.Add(Tuple.Create(Sphere4, Sphere1, Sphere5, Sphere8));
        faces.Add(Tuple.Create(Sphere5, Sphere6, Sphere7, Sphere8));
    }
    
    // Decompose each face into triangles for the purposes of volume calculation.
    // Each face is assumed to be arranged counter clockwise from the outside.
    //
    // Returns:
    //     a tx3 jnp.ndarray of vertex indices for all t triangles
    void InitTriangulatedFaces()
    {
        if (faces != null)
        {
            foreach (var face in faces)
            {
                // Skip the top face by checking if the face contains any of the top vertices (sphere1-4)
                if (face.Item1 == sphere1 || face.Item2 == sphere1 || face.Item3 == sphere1 || face.Item4 == sphere1 ||
                    face.Item1 == sphere2 || face.Item2 == sphere2 || face.Item3 == sphere2 || face.Item4 == sphere2 ||
                    face.Item1 == sphere3 || face.Item2 == sphere3 || face.Item3 == sphere3 || face.Item4 == sphere3 ||
                    face.Item1 == sphere4 || face.Item2 == sphere4 || face.Item3 == sphere4 || face.Item4 == sphere4)
                {
                    continue;
                }
                triangles.Add(Tuple.Create(face.Item1, face.Item2, face.Item3));
                triangles.Add(Tuple.Create(face.Item1, face.Item3, face.Item4));
            }
        }
        else
        {
            Debug.LogError("Cell: faces is null");
        }
    }

    // Return the current world-space positions of each triangle's vertices as a jagged array.
    // The returned array has shape [t][3] where t is number of triangles.
    public Vector3[][] GetTrianglePositions()
    {
        if (triangles == null) return new Vector3[0][];
        int count = triangles.Count;
        Vector3[][] result = new Vector3[count][];
        for (int i = 0; i < count; i++)
        {
            var triangle = triangles[i];
            Vector3 p0 = triangle.Item1 != null ? triangle.Item1.transform.position : Vector3.zero;
            Vector3 p1 = triangle.Item2 != null ? triangle.Item2.transform.position : Vector3.zero;
            Vector3 p2 = triangle.Item3 != null ? triangle.Item3.transform.position : Vector3.zero;
            result[i] = new Vector3[3] { p0, p1, p2 };
        }
        return result;
    }

    // Compute the cell volume by summing tetrahedron volumes formed by the apex (first vertex)
    // and each triangulated face of the cell.
    public float CalcVolume()
    {
        Vector3 apexPos = (Vertices != null && Vertices.Count > 0) ? Vertices[0].transform.position : Vector3.zero;
        Vector3[][] trianglePositions = GetTrianglePositions();
        float cellVolume = 0f;
        if (trianglePositions != null)
        {
            foreach (Vector3[] triangle in trianglePositions)
            {
                if (triangle == null)
                {
                    Debug.LogError("ConstantVolume: triangle is null.");
                    continue;
                }

                if (triangle.Length != 3)
                {
                    Debug.LogError("ConstantVolume: triangle does not have 3 vertices.");
                    continue;
                }
                
                Vector3 p0 = triangle[0] - apexPos;
                Vector3 p1 = triangle[1] - apexPos;
                Vector3 p2 = triangle[2] - apexPos;
                float tetrahedronVolume = Utility.Determinant3x3(p0, p1, p2);
                cellVolume += tetrahedronVolume;
            }
        }
        return cellVolume;
    }
}
