using UnityEngine;
using System.Collections.Generic;

// PlanarFaces: assumes each face has the same number of vertices.
// Computes a per-face normal via PCA (smallest eigenvector of covariance)
// and returns constraints and a simplified Jacobian (ignoring normal derivatives).
public class PlanarFaces : Constraint
{
    private int[,] faceIndices; // F x V
    private int F;
    private int V;

    public override void InitializeConstraint()
    {
        var builder = ModelBuilderObject;
        if (builder == null)
        {
            Debug.LogError("PlanarFaces: ModelBuilderObject is null.");
            return;
        }
        var faces = builder.Faces; // List of Tuple<GameObject, ...>
        if (faces == null || faces.Count == 0)
        {
            Debug.LogError("PlanarFaces: no faces available in model builder.");
            return;
        }

        // Determine vertex count per face and ensure uniformity
        int vertexCount = -1;
        foreach (var face in faces)
        {
            int count = 0;
            if (face.Item1 != null) count++;
            if (face.Item2 != null) count++;
            if (face.Item3 != null) count++;
            if (face.Item4 != null) count++;
            if (vertexCount == -1) vertexCount = count;
            else if (vertexCount != count)
            {
                Debug.LogError("PlanarFaces: faces have different vertex counts. Use ragged variant.");
                return;
            }
        }

        F = faces.Count;
        V = vertexCount;
        faceIndices = new int[F, V];

        // Build global vertex index mapping from ModelBuilder.Vertices
        var globalVerts = builder.Vertices;

        for (int f = 0; f < F; f++)
        {
            var face = faces[f];
            // assuming up to 4 elements; fill in order
            GameObject[] elems = new GameObject[] { face.Item1, face.Item2, face.Item3, face.Item4 };
            for (int v = 0; v < V; v++)
            {
                int idx = globalVerts.IndexOf(elems[v]);
                if (idx < 0)
                {
                    Debug.LogWarning($"PlanarFaces.InitializeConstraint: vertex not found in global verts for face {f}, slot {v}.");
                }
                faceIndices[f, v] = idx;
            }
        }

        Debug.Log($"PlanarFaces.InitializeConstraint: F={F}, V={V}");
    }

    // Helper: compute covariance matrix for a face (3x3)
    private static float[,] Covariance3(Vector3[] rel)
    {
        int V = rel.Length;
        int dof = Mathf.Max(1, V - 1);
        float[,] C = new float[3, 3];
        for (int i = 0; i < V; i++)
        {
            Vector3 r = rel[i];
            C[0, 0] += r.x * r.x;
            C[0, 1] += r.x * r.y;
            C[0, 2] += r.x * r.z;
            C[1, 0] += r.y * r.x;
            C[1, 1] += r.y * r.y;
            C[1, 2] += r.y * r.z;
            C[2, 0] += r.z * r.x;
            C[2, 1] += r.z * r.y;
            C[2, 2] += r.z * r.z;
        }
        float inv = 1f / dof;
        for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) C[a, b] *= inv;
        return C;
    }

    // Jacobi eigenvalue algorithm for symmetric 3x3 matrices to get eigenvectors
    // Returns eigenvectors as columns in a 3x3 matrix; eigenvalues in array (ascending)
    private static void SymmetricEigs3(float[,] A, out float[] evals, out float[,] evecs)
    {
        evals = new float[3];
        evecs = new float[3, 3];
        float[,] m = new float[3, 3];
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) m[i, j] = A[i, j];
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) evecs[i, j] = (i == j) ? 1f : 0f;

        for (int iter = 0; iter < 50; iter++)
        {
            int p = 0, q = 1;
            float max = Mathf.Abs(m[0, 1]);
            if (Mathf.Abs(m[0, 2]) > max) { max = Mathf.Abs(m[0, 2]); p = 0; q = 2; }
            if (Mathf.Abs(m[1, 2]) > max) { max = Mathf.Abs(m[1, 2]); p = 1; q = 2; }
            if (max < 1e-10f) break;
            float app = m[p, p];
            float aqq = m[q, q];
            float apq = m[p, q];
            float phi = 0.5f * Mathf.Atan2(2f * apq, aqq - app);
            float c = Mathf.Cos(phi);
            float s = Mathf.Sin(phi);

            for (int i = 0; i < 3; i++)
            {
                float mip = m[i, p];
                float miq = m[i, q];
                m[i, p] = c * mip - s * miq;
                m[i, q] = s * mip + c * miq;
            }
            for (int j = 0; j < 3; j++)
            {
                float mpj = m[p, j];
                float mqj = m[q, j];
                m[p, j] = c * mpj - s * mqj;
                m[q, j] = s * mpj + c * mqj;
            }
            m[p, p] = c * c * app - 2f * s * c * apq + s * s * aqq;
            m[q, q] = s * s * app + 2f * s * c * apq + c * c * aqq;
            m[p, q] = 0f;
            m[q, p] = 0f;

            for (int i = 0; i < 3; i++)
            {
                float vip = evecs[i, p];
                float viq = evecs[i, q];
                evecs[i, p] = c * vip - s * viq;
                evecs[i, q] = s * vip + c * viq;
            }
        }

        evals[0] = m[0, 0];
        evals[1] = m[1, 1];
        evals[2] = m[2, 2];

        for (int i = 0; i < 2; i++)
        {
            for (int j = i + 1; j < 3; j++)
            {
                if (evals[j] < evals[i])
                {
                    float t = evals[i]; evals[i] = evals[j]; evals[j] = t;
                    for (int r = 0; r < 3; r++)
                    {
                        float tmp = evecs[r, i]; evecs[r, i] = evecs[r, j]; evecs[r, j] = tmp;
                    }
                }
            }
        }
    }

    public override (float[] constraints, float[,,] jacobians, float[,,] jacobianDerivative) CalculateConstraints()
    {
        if (faceIndices == null)
        {
            Debug.LogError("PlanarFaces: not initialized.");
            return (null, null, null);
        }

        Vector3[] pos = ModelBuilderObject != null ? ModelBuilderObject.GetPositions() : new Vector3[0];
        Vector3[] vel = ModelBuilderObject != null ? ModelBuilderObject.Velocities : Utility.CreateInitializedArray<Vector3>(pos.Length, Vector3.zero);
        int N = pos.Length;
        int D = 3;

        Vector3[,] points = new Vector3[F, V];
        Vector3[,] dpoints = new Vector3[F, V];
        for (int f = 0; f < F; f++)
        {
            for (int v = 0; v < V; v++)
            {
                int idx = faceIndices[f, v];
                if (idx < 0 || idx >= pos.Length)
                {
                    Debug.LogWarning($"PlanarFaces.CalculateConstraints: invalid vertex index for face {f}, vertex {v}: {idx}. Using zero vector.");
                    points[f, v] = Vector3.zero;
                    dpoints[f, v] = Vector3.zero;
                }
                else
                {
                    points[f, v] = pos[idx];
                    dpoints[f, v] = vel[idx];
                }
            }
        }

        Vector3[] centroids = new Vector3[F];
        Vector3[] dcentroids = new Vector3[F];
        Vector3[,] rel = new Vector3[F, V];
        Vector3[,] drel = new Vector3[F, V];
        for (int f = 0; f < F; f++)
        {
            Vector3 sum = Vector3.zero;
            Vector3 dsum = Vector3.zero;
            for (int v = 0; v < V; v++) { sum += points[f, v]; dsum += dpoints[f, v]; }
            centroids[f] = sum / V;
            dcentroids[f] = dsum / V;
            for (int v = 0; v < V; v++)
            {
                rel[f, v] = points[f, v] - centroids[f];
                drel[f, v] = dpoints[f, v] - dcentroids[f];
            }
        }

        Vector3[] normals = new Vector3[F];
        for (int f = 0; f < F; f++)
        {
            Vector3[] relVec = new Vector3[V];
            for (int v = 0; v < V; v++) relVec[v] = rel[f, v];
            float[,] C = Covariance3(relVec);
            SymmetricEigs3(C, out float[] evals, out float[,] evecs);
            normals[f] = new Vector3(evecs[0, 0], evecs[1, 0], evecs[2, 0]);
            normals[f].Normalize();
        }

        float[] constraints = new float[F * V];
        for (int f = 0; f < F; f++)
        {
            for (int v = 0; v < V; v++)
            {
                int cidx = f * V + v;
                constraints[cidx] = Vector3.Dot(rel[f, v], normals[f]);
            }
        }

        float[,,] jacobians = new float[F * V, N, D];
        for (int f = 0; f < F; f++)
        {
            for (int v = 0; v < V; v++)
            {
                int cidx = f * V + v;
                for (int p = 0; p < N; p++)
                {
                    float coeff = 0f;
                    for (int vv = 0; vv < V; vv++) if (faceIndices[f, vv] == p) { coeff -= 1f / V; }
                    if (faceIndices[f, v] == p) coeff += 1f;
                    if (Mathf.Approximately(coeff, 0f)) continue;
                    jacobians[cidx, p, 0] = coeff * normals[f].x;
                    jacobians[cidx, p, 1] = coeff * normals[f].y;
                    jacobians[cidx, p, 2] = coeff * normals[f].z;
                }
            }
        }

        float[,,] djac_dts = new float[F * V, N, D];

        return (constraints, jacobians, djac_dts);
    }
}
