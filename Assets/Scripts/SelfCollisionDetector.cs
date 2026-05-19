using System;
using System.Collections.Generic;
using UnityEngine;

public class SelfCollisionDetector : MonoBehaviour
{
    [SerializeField] ModelBuilder modelBuilder;
    [SerializeField] float insideTolerance = 1e-4f;
    [SerializeField] bool ignoreCellsContainingVertex = true;
    [SerializeField] bool logOnCollisionStateChange = true;
    [SerializeField] Color collisionColor = Color.red;

    private readonly List<HashSet<int>> cellVertexIndices = new List<HashSet<int>>();
    private readonly HashSet<int> penetratingVertexIndices = new HashSet<int>();
    private readonly Dictionary<GameObject, int> vertexIndexLookup = new Dictionary<GameObject, int>();
    private bool hadCollisionLastFrame;
    // Track original colors for all vertex renderers
    private readonly Dictionary<Renderer, Color> originalVertexColors = new Dictionary<Renderer, Color>();

    public bool HasSelfCollision => penetratingVertexIndices.Count > 0;
    public int PenetratingVertexCount => penetratingVertexIndices.Count;

    void Start()
    {
        RebuildCache();
    }

    void Update()
    {
        if (modelBuilder == null)
        {
            return;
        }

        if (modelBuilder.Cells == null || modelBuilder.Cells.Count == 0)
        {
            RebuildCache();
            if (modelBuilder.Cells == null || modelBuilder.Cells.Count == 0)
            {
                return;
            }
        }

        if (cellVertexIndices.Count != modelBuilder.Cells.Count)
        {
            RebuildCache();
        }

        if (modelBuilder.Vertices == null || vertexIndexLookup.Count != modelBuilder.Vertices.Count)
        {
            RebuildCache();
        }

        DetectSelfCollision();
    }

    public void RebuildCache()
    {
        if (modelBuilder == null)
        {
            modelBuilder = GetComponent<ModelBuilder>();
            if (modelBuilder == null)
            {
                modelBuilder = GetComponentInChildren<ModelBuilder>();
            }
        }

        cellVertexIndices.Clear();
        penetratingVertexIndices.Clear();

        if (modelBuilder == null || modelBuilder.Vertices == null || modelBuilder.Cells == null)
        {
            return;
        }

        BuildVertexIndexLookup();
        BuildCellVertexIndices();
    }

    private void BuildCellVertexIndices()
    {
        for (int cellIdx = 0; cellIdx < modelBuilder.Cells.Count; cellIdx++)
        {
            Cell cell = modelBuilder.Cells[cellIdx];
            HashSet<int> verticesForCell = new HashSet<int>();
            if (cell == null || cell.Faces == null || cell.Faces.Count == 0)
            {
                cellVertexIndices.Add(verticesForCell);
                continue;
            }

            foreach (var face in cell.Faces)
            {
                if (face == null) continue;
                int i0 = TryGetVertexIndex(face.Item1);
                int i1 = TryGetVertexIndex(face.Item2);
                int i2 = TryGetVertexIndex(face.Item3);
                int i3 = TryGetVertexIndex(face.Item4);
                if (i0 < 0 || i1 < 0 || i2 < 0 || i3 < 0) continue;
                verticesForCell.Add(i0);
                verticesForCell.Add(i1);
                verticesForCell.Add(i2);
                verticesForCell.Add(i3);
            }

            cellVertexIndices.Add(verticesForCell);
        }
    }

    private void DetectSelfCollision()
    {
        penetratingVertexIndices.Clear();
        int firstPenetratingVertexIdx = -1;

        Vector3[] positions = modelBuilder.GetPositions();
        if (positions == null || positions.Length == 0)
        {
            return;
        }

        for (int vertexIdx = 0; vertexIdx < positions.Length; vertexIdx++)
        {
            Vector3 point = positions[vertexIdx];
            for (int cellIdx = 0; cellIdx < modelBuilder.Cells.Count; cellIdx++)
            {
                var cell = modelBuilder.Cells[cellIdx];

                if (cell == null || cell.Faces == null || cell.Faces.Count == 0)
                    continue;

                if (ignoreCellsContainingVertex && cellIdx < cellVertexIndices.Count && cellVertexIndices[cellIdx].Contains(vertexIdx))
                    continue;
                    
                if (IsPointInsideCellViaFaces(point, cell.Faces, positions))
                {
                    penetratingVertexIndices.Add(vertexIdx);
                    if (firstPenetratingVertexIdx < 0)
                    {
                        firstPenetratingVertexIdx = vertexIdx;
                    }
                    break;
                }
            }
        }

        if (logOnCollisionStateChange && HasSelfCollision != hadCollisionLastFrame)
        {
            Debug.Log("SelfCollisionDetector: self collision " + (HasSelfCollision ? "detected" : "cleared") + ", penetratingVertices=" + PenetratingVertexCount);
        }

        ApplyCollisionColors();
        hadCollisionLastFrame = HasSelfCollision;
    }


    private void ApplyCollisionColors()
    {
        if (modelBuilder == null || modelBuilder.Vertices == null)
            return;

        // First, restore all vertex renderer colors to their original
        for (int i = 0; i < modelBuilder.Vertices.Count; i++)
        {
            GameObject vertex = modelBuilder.Vertices[i];
            if (vertex == null) continue;
            Renderer r = vertex.GetComponent<Renderer>() ?? vertex.GetComponentInChildren<Renderer>();
            if (r == null) continue;
            if (originalVertexColors.TryGetValue(r, out Color orig))
            {
                r.material.color = orig;
            }
            else
            {
                originalVertexColors[r] = r.material.color;
            }
        }

        // Now, set color for all self-collided vertices
        foreach (int idx in penetratingVertexIndices)
        {
            if (idx < 0 || idx >= modelBuilder.Vertices.Count) continue;
            GameObject vertex = modelBuilder.Vertices[idx];
            if (vertex == null) continue;
            Renderer r = vertex.GetComponent<Renderer>() ?? vertex.GetComponentInChildren<Renderer>();
            if (r == null) continue;
            if (!originalVertexColors.ContainsKey(r))
            {
                originalVertexColors[r] = r.material.color;
            }
            r.material.color = collisionColor;
        }
    }

    private void BuildVertexIndexLookup()
    {
        vertexIndexLookup.Clear();
        if (modelBuilder == null || modelBuilder.Vertices == null)
        {
            return;
        }

        for (int i = 0; i < modelBuilder.Vertices.Count; i++)
        {
            GameObject vertex = modelBuilder.Vertices[i];
            if (vertex != null)
            {
                vertexIndexLookup[vertex] = i;
            }
        }
    }

    // Uses Cell.Faces directly and combines multiple tests to reduce false positives.
    private bool IsPointInsideCellViaFaces(
        Vector3 point,
        List<Tuple<GameObject, GameObject, GameObject, GameObject>> faces,
        Vector3[] positions)
    {
        if (faces == null || faces.Count == 0)
        {
            return false;
        }

        bool hasBounds = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        Vector3 cellCenter = Vector3.zero;
        int centerCount = 0;
        HashSet<int> uniqueIndices = new HashSet<int>();

        foreach (var face in faces)
        {
            int i0 = TryGetVertexIndex(face.Item1);
            int i1 = TryGetVertexIndex(face.Item2);
            int i2 = TryGetVertexIndex(face.Item3);
            int i3 = TryGetVertexIndex(face.Item4);
            int[] idx = { i0, i1, i2, i3 };
            for (int k = 0; k < 4; k++)
            {
                int vi = idx[k];
                if (vi < 0)
                {
                    continue;
                }

                Vector3 p = positions[vi];
                (hasBounds, min, max) = UpdateBounds(hasBounds, min, max, p);

                if (uniqueIndices.Add(vi))
                {
                    cellCenter += p;
                    centerCount++;
                }
            }
        }

        if (!hasBounds || centerCount == 0)
        {
            return false;
        }

        Vector3 pad = Vector3.one * Mathf.Max(insideTolerance * 2f, 1e-5f);
        if (point.x < min.x - pad.x || point.x > max.x + pad.x ||
            point.y < min.y - pad.y || point.y > max.y + pad.y ||
            point.z < min.z - pad.z || point.z > max.z + pad.z)
        {
            return false;
        }

        cellCenter /= centerCount;

        // fixed, non-axis-aligned ray directions for the odd-even ray test
        Vector3[] rayDirs =
        {
            new Vector3(0.754f, 0.569f, 0.326f).normalized,
            new Vector3(-0.421f, 0.812f, 0.403f).normalized,
            new Vector3(0.299f, -0.631f, 0.716f).normalized,
        };

        int insideRays = 0;
        for (int r = 0; r < rayDirs.Length; r++)
        {
            int rayHits = CountRayIntersections(point, rayDirs[r], faces, positions, insideTolerance);
            if ((rayHits % 2) == 1)
            {
                insideRays++;
            }
        }

        bool insideByRays = insideRays >= 2;
        if (!insideByRays)
        {
            return false;
        }

        // 2) Half-space consistency check for convex cells.
        int validFaceCount = 0;
        foreach (var face in faces)
        {
            int i0 = TryGetVertexIndex(face.Item1);
            int i1 = TryGetVertexIndex(face.Item2);
            int i2 = TryGetVertexIndex(face.Item3);
            int i3 = TryGetVertexIndex(face.Item4);
            if (i0 < 0 || i1 < 0 || i2 < 0 || i3 < 0)
            {
                continue;
            }

            Vector3 p0 = positions[i0];
            Vector3 p1 = positions[i1];
            Vector3 p2 = positions[i2];
            Vector3 p3 = positions[i3];
            Vector3 faceCenter = (p0 + p1 + p2 + p3) * 0.25f;

            Vector3 n0 = Vector3.Cross(p1 - p0, p2 - p0);
            Vector3 n1 = Vector3.Cross(p2 - p0, p3 - p0);
            Vector3 normal = n0 + n1;
            if (normal.sqrMagnitude <= Mathf.Epsilon)
            {
                continue;
            }

            validFaceCount++;
            if (Vector3.Dot(normal, faceCenter - cellCenter) < 0f)
            {
                normal = -normal;
            }

            float signedDistance = Vector3.Dot(normal.normalized, point - faceCenter);
            if (signedDistance > insideTolerance)
            {
                return false;
            }
        }

        return validFaceCount > 0;
    }

    private static (bool hasBounds, Vector3 min, Vector3 max) UpdateBounds(bool hasBounds, Vector3 min, Vector3 max, Vector3 point)
    {
        if (!hasBounds)
        {
            return (true, point, point);
        }

        return (true, Vector3.Min(min, point), Vector3.Max(max, point));
    }

    private int CountRayIntersections(
        Vector3 point,
        Vector3 rayDir,
        List<Tuple<GameObject, GameObject, GameObject, GameObject>> faces,
        Vector3[] positions,
        float tolerance)
    {
        Vector3 rayOrigin = point + rayDir * Mathf.Max(tolerance, 1e-5f);
        int intersectionCount = 0;
        List<float> hitDistances = new List<float>(faces.Count);

        foreach (var face in faces)
        {
            int i0 = TryGetVertexIndex(face.Item1);
            int i1 = TryGetVertexIndex(face.Item2);
            int i2 = TryGetVertexIndex(face.Item3);
            int i3 = TryGetVertexIndex(face.Item4);
            if (i0 < 0 || i1 < 0 || i2 < 0 || i3 < 0)
            {
                continue;
            }

            Vector3 p0 = positions[i0];
            Vector3 p1 = positions[i1];
            Vector3 p2 = positions[i2];
            Vector3 p3 = positions[i3];

            CountIntersection(rayOrigin, rayDir, p0, p1, p2, tolerance, hitDistances, ref intersectionCount);
            CountIntersection(rayOrigin, rayDir, p0, p2, p3, tolerance, hitDistances, ref intersectionCount);
        }

        return intersectionCount;
    }

    private static void CountIntersection(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        float tolerance,
        List<float> hitDistances,
        ref int intersectionCount)
    {
        float t;
        if (!RayIntersectsTriangle(rayOrigin, rayDirection, a, b, c, out t, tolerance))
        {
            return;
        }

        float mergeEps = Mathf.Max(1e-4f, tolerance * 10f);
        for (int i = 0; i < hitDistances.Count; i++)
        {
            if (Mathf.Abs(hitDistances[i] - t) <= mergeEps)
            {
                return;
            }
        }

        hitDistances.Add(t);
        intersectionCount++;
    }

    private static bool RayIntersectsTriangle(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        out float hitDistance,
        float tolerance)
    {
        hitDistance = 0f;

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 perpendicularCross = Vector3.Cross(rayDirection, edge2);
        float determinant = Vector3.Dot(edge1, perpendicularCross);
        float determinantEpsilon = Mathf.Max(1e-8f, tolerance * 0.1f);
        if (Mathf.Abs(determinant) < determinantEpsilon)
        {
            return false;
        }

        float inverseDeterminant = 1f / determinant;
        Vector3 originToVertex = rayOrigin - v0;
        float hitTriangleWeightU = Vector3.Dot(originToVertex, perpendicularCross) * inverseDeterminant;
        if (hitTriangleWeightU < -tolerance || hitTriangleWeightU > 1f + tolerance)
        {
            return false;
        }

        Vector3 secondCross = Vector3.Cross(originToVertex, edge1);
        float hitTriangleWeightV = Vector3.Dot(rayDirection, secondCross) * inverseDeterminant;
        if (hitTriangleWeightV < -tolerance || hitTriangleWeightU + hitTriangleWeightV > 1f + tolerance)
        {
            return false;
        }

        hitDistance = Vector3.Dot(edge2, secondCross) * inverseDeterminant;
        return hitDistance > Mathf.Max(tolerance, 1e-6f);
    }

    private int TryGetVertexIndex(GameObject vertex)
    {
        if (vertex == null)
        {
            return -1;
        }

        int index;
        return vertexIndexLookup.TryGetValue(vertex, out index) ? index : -1;
    }
}
