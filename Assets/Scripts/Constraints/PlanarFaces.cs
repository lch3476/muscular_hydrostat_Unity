using UnityEngine;
using System.Collections.Generic;
using UnityEngine.TestTools;

// PlanarFaces: assumes each face has the same number of vertices.
// Computes a per-face normal via PCA (smallest eigenvector of covariance)
// and returns constraints and a simplified Jacobian (ignoring normal derivatives).
public class PlanarFaces : Constraint
{
    int[,] faceIndices; // F x V
    int facesNum; // F
    int verticesPerFace; // V
    int positionNum; // N
    int dim; // D


    public override void InitializeConstraint()
    {   
        if (ModelBuilderObject == null)
        {
            Debug.LogError("PlanarFaces: ModelBuilderObject is null during initialization.");
            return;
        }

        var faces = ModelBuilderObject.Faces;
        if (faces == null || faces.Count == 0)
        {
            Debug.LogError("PlanarFaces: no faces available in model builder.");
            return;
        }

        verticesPerFace = faces.Count > 0 ? Cell.NUM_PLANE_VERTICES : 0;  
        facesNum = faces.Count;
        positionNum = ModelBuilderObject.Vertices.Count;
        dim = 3;

        faceIndices = new int[facesNum, verticesPerFace];
    }

    // Jacobi eigenvalue algorithm for symmetric 3x3 matrices to get eigenvectors.
    // Returns eigenvectors as columns in a 3x3 matrix; eigenvalues in array (ascending).
    private static void SymmetricEigs3(float[,] A, out float[] evals, out float[,] evecs)
    {
        evals = new float[3];
        evecs = new float[3, 3];
        float[,] m = new float[3, 3];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                m[i, j] = A[i, j];
                evecs[i, j] = (i == j) ? 1f : 0f;
            }
        }

        for (int iter = 0; iter < 50; iter++)
        {
            int p = 0;
            int q = 1;
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
                    float t = evals[i];
                    evals[i] = evals[j];
                    evals[j] = t;
                    for (int r = 0; r < 3; r++)
                    {
                        float tmp = evecs[r, i];
                        evecs[r, i] = evecs[r, j];
                        evecs[r, j] = tmp;
                    }
                }
            }
        }
    }

    private (float[][][] cov, float[][][][][] dcdp, float[][][] dcdt, float[][][][][] ddcdpdt) CalcCovarianceVariables(
        Vector3[,] relativePositions,
        Vector3[,] relativeVelocities,
        float[][][][][] drelative_dP)
    {
        int faces = relativePositions.GetLength(0);
        int verts = relativePositions.GetLength(1);
        int dof = Mathf.Max(1, verts - 1);
        float invDof = 1f / dof;

        float[][][] cov = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        float[][][][][] dcdp = (float[][][][][])Utility.CreateJaggedArray<float>(faces, dim, dim, positionNum, dim);
        float[][][] dcdt = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        float[][][][][] ddcdpdt = (float[][][][][])Utility.CreateJaggedArray<float>(faces, dim, dim, positionNum, dim);

        // cov = relative_positions^T * relative_positions / dof
        for (int f = 0; f < faces; f++)
        {
            for (int v = 0; v < verts; v++)
            {
                Vector3 rp = relativePositions[f, v];
                cov[f][0][0] += rp.x * rp.x;
                cov[f][0][1] += rp.x * rp.y;
                cov[f][0][2] += rp.x * rp.z;
                cov[f][1][0] += rp.y * rp.x;
                cov[f][1][1] += rp.y * rp.y;
                cov[f][1][2] += rp.y * rp.z;
                cov[f][2][0] += rp.z * rp.x;
                cov[f][2][1] += rp.z * rp.y;
                cov[f][2][2] += rp.z * rp.z;
            }

            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    cov[f][i][j] *= invDof;
                }
            }
        }

        // dcdp and ddcdpdt (symmetric over the two D axes)
        for (int f = 0; f < faces; f++)
        {
            for (int v = 0; v < verts; v++)
            {
                Vector3 rp = relativePositions[f, v];
                Vector3 rv = relativeVelocities[f, v];
                for (int d = 0; d < dim; d++)
                {
                    float rpComp = (d == 0) ? rp.x : (d == 1) ? rp.y : rp.z;
                    float rvComp = (d == 0) ? rv.x : (d == 1) ? rv.y : rv.z;
                    for (int j = 0; j < dim; j++)
                    {
                        for (int n = 0; n < positionNum; n++)
                        {
                            for (int dd = 0; dd < dim; dd++)
                            {
                                dcdp[f][d][j][n][dd] += rpComp * drelative_dP[f][v][j][n][dd];
                                ddcdpdt[f][d][j][n][dd] += rvComp * drelative_dP[f][v][j][n][dd];
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < dim; i++)
            {
                for (int j = i; j < dim; j++)
                {
                    for (int n = 0; n < positionNum; n++)
                    {
                        for (int d = 0; d < dim; d++)
                        {
                            float symDcdp = (dcdp[f][i][j][n][d] + dcdp[f][j][i][n][d]) * invDof;
                            float symDdcdpdt = (ddcdpdt[f][i][j][n][d] + ddcdpdt[f][j][i][n][d]) * invDof;
                            dcdp[f][i][j][n][d] = symDcdp;
                            dcdp[f][j][i][n][d] = symDcdp;
                            ddcdpdt[f][i][j][n][d] = symDdcdpdt;
                            ddcdpdt[f][j][i][n][d] = symDdcdpdt;
                        }
                    }
                }
            }
        }

        // dcdt = (relative_velocities^T * relative_positions + transpose) / dof
        for (int f = 0; f < faces; f++)
        {
            float[,] dcdtSingle = new float[dim, dim];
            for (int v = 0; v < verts; v++)
            {
                Vector3 rv = relativeVelocities[f, v];
                Vector3 rp = relativePositions[f, v];
                dcdtSingle[0, 0] += rv.x * rp.x;
                dcdtSingle[0, 1] += rv.x * rp.y;
                dcdtSingle[0, 2] += rv.x * rp.z;
                dcdtSingle[1, 0] += rv.y * rp.x;
                dcdtSingle[1, 1] += rv.y * rp.y;
                dcdtSingle[1, 2] += rv.y * rp.z;
                dcdtSingle[2, 0] += rv.z * rp.x;
                dcdtSingle[2, 1] += rv.z * rp.y;
                dcdtSingle[2, 2] += rv.z * rp.z;
            }

            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    dcdt[f][i][j] = (dcdtSingle[i, j] + dcdtSingle[j, i]) * invDof;
                }
            }
        }

        return (cov, dcdp, dcdt, ddcdpdt);
    }

    private (float[][] normals, float[][][][] dndp, float[][] dndt, float[][][][] ddndpdt) CalcNormalVariables(
        float[][][] cov,
        float[][][][][] dcdp,
        float[][][] dcdt,
        float[][][][][] ddcdpdt)
    {
        int faces = cov.Length;
        float[][] eigvals = new float[faces][];
        float[][][] eigvecs = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        float[][][] eigvecsT = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);

        for (int f = 0; f < faces; f++)
        {
            float[,] covMat = new float[dim, dim];
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    covMat[i, j] = cov[f][i][j];
                }
            }

            SymmetricEigs3(covMat, out float[] evals, out float[,] evecs);
            eigvals[f] = new float[dim];
            for (int i = 0; i < dim; i++)
            {
                eigvals[f][i] = evals[i];
            }
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    eigvecs[f][i][j] = evecs[i, j];
                    eigvecsT[f][i][j] = evecs[j, i];
                }
            }
        }

        float[][] normals = (float[][])Utility.CreateJaggedArray<float>(faces, dim);
        for (int f = 0; f < faces; f++)
        {
            for (int d = 0; d < dim; d++)
            {
                normals[f][d] = eigvecs[f][d][0];
            }
        }

        float[][][] eigvalDif = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        for (int f = 0; f < faces; f++)
        {
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    float diff = eigvals[f][i] - eigvals[f][j];
                    eigvalDif[f][i][j] = Mathf.Abs(diff) > 0f ? 1f / diff : 0f;
                }
            }
        }

        float[][][] minEigvalDifs = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        for (int f = 0; f < faces; f++)
        {
            for (int a = 0; a < dim; a++)
            {
                float val = eigvalDif[f][0][a];
                for (int b = 0; b < dim; b++)
                {
                    minEigvalDifs[f][a][b] = val;
                }
            }
        }

        float[][][][] dndp = BespokeContraction(minEigvalDifs, eigvecsT, dcdp, normals, eigvecs);

        float[][][] dvdts = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        for (int f = 0; f < faces; f++)
        {
            float[,] temp = new float[dim, dim];
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < dim; k++)
                    {
                        sum += dcdt[f][i][k] * eigvecs[f][k][j];
                    }
                    temp[i, j] = sum;
                }
            }

            float[,] m = new float[dim, dim];
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < dim; k++)
                    {
                        sum += eigvecsT[f][i][k] * temp[k, j];
                    }
                    m[i, j] = sum;
                }
            }

            for (int j = 0; j < dim; j++)
            {
                for (int k = 0; k < dim; k++)
                {
                    float sum = 0f;
                    for (int i = 0; i < dim; i++)
                    {
                        float val = -eigvalDif[f][i][j] * m[i, j];
                        sum += val * eigvecsT[f][i][k];
                    }
                    dvdts[f][k][j] = sum;
                }
            }
        }

        float[][] dndt = (float[][])Utility.CreateJaggedArray<float>(faces, dim);
        for (int f = 0; f < faces; f++)
        {
            for (int d = 0; d < dim; d++)
            {
                dndt[f][d] = dvdts[f][d][0];
            }
        }

        float[][] dldts = (float[][])Utility.CreateJaggedArray<float>(faces, dim);
        for (int f = 0; f < faces; f++)
        {
            float[,] temp = new float[dim, dim];
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < dim; k++)
                    {
                        sum += dcdt[f][i][k] * eigvecs[f][k][j];
                    }
                    temp[i, j] = sum;
                }
            }

            float[,] m = new float[dim, dim];
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < dim; k++)
                    {
                        sum += eigvecsT[f][i][k] * temp[k, j];
                    }
                    m[i, j] = sum;
                }
            }

            for (int i = 0; i < dim; i++)
            {
                dldts[f][i] = m[i, i];
            }
        }

        float[][][] deigvalDifdt = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        for (int f = 0; f < faces; f++)
        {
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    float diff = dldts[f][i] - dldts[f][j];
                    float denom = eigvals[f][i] - eigvals[f][j];
                    if (Mathf.Abs(denom) > 0f && Mathf.Abs(diff) > 0f)
                    {
                        deigvalDifdt[f][i][j] = -diff / (denom * denom);
                    }
                    else
                    {
                        deigvalDifdt[f][i][j] = 0f;
                    }
                }
            }
        }

        float[][][] minDeigvalDifdt = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        for (int f = 0; f < faces; f++)
        {
            for (int a = 0; a < dim; a++)
            {
                float val = deigvalDifdt[f][0][a];
                for (int b = 0; b < dim; b++)
                {
                    minDeigvalDifdt[f][a][b] = val;
                }
            }
        }

        float[][][] dvdtsT = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        for (int f = 0; f < faces; f++)
        {
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    dvdtsT[f][i][j] = dvdts[f][j][i];
                }
            }
        }

        float[][][][] term1 = BespokeContraction(minDeigvalDifdt, eigvecsT, dcdp, normals, eigvecs);
        float[][][][] term2 = BespokeContraction(minEigvalDifs, dvdtsT, dcdp, normals, eigvecs);
        float[][][][] term3 = BespokeContraction(minEigvalDifs, eigvecsT, ddcdpdt, normals, eigvecs);
        float[][][][] term4 = BespokeContraction(minEigvalDifs, eigvecsT, dcdp, dndt, eigvecs);
        float[][][][] term5 = BespokeContraction(minEigvalDifs, eigvecsT, dcdp, normals, dvdts);

        float[][][][] ddndpdt = (float[][][][])Utility.CreateJaggedArray<float>(faces, dim, positionNum, dim);
        for (int f = 0; f < faces; f++)
        {
            for (int d = 0; d < dim; d++)
            {
                for (int n = 0; n < positionNum; n++)
                {
                    for (int dd = 0; dd < dim; dd++)
                    {
                        ddndpdt[f][d][n][dd] = term1[f][d][n][dd]
                            + term2[f][d][n][dd]
                            + term3[f][d][n][dd]
                            + term4[f][d][n][dd]
                            + term5[f][d][n][dd];
                    }
                }
            }
        }

        return (normals, dndp, dndt, ddndpdt);
    }

    private float[][][][] BespokeContraction(
        float[][][] eigvalDifs,
        float[][][] eigvecsT,
        float[][][][][] dcdp,
        float[][] normals,
        float[][][] eigvecs)
    {
        int faces = eigvalDifs.Length;
        float[][][][] result = (float[][][][])Utility.CreateJaggedArray<float>(faces, dim, positionNum, dim);

        for (int f = 0; f < faces; f++)
        {
            for (int p = 0; p < dim; p++)
            {
                for (int n = 0; n < positionNum; n++)
                {
                    for (int d = 0; d < dim; d++)
                    {
                        float sum = 0f;
                        for (int a = 0; a < dim; a++)
                        {
                            for (int b = 0; b < dim; b++)
                            {
                                float eigTerm = eigvalDifs[f][a][b] * eigvecsT[f][a][b];
                                float eigvecTerm = eigvecs[f][p][a];
                                for (int j = 0; j < dim; j++)
                                {
                                    sum += eigTerm
                                        * dcdp[f][b][j][n][d]
                                        * normals[f][j]
                                        * eigvecTerm;
                                }
                            }
                        }
                        result[f][p][n][d] = sum;
                    }
                }
            }
        }

        return result;
    }

    public override (float[] constraints, float[,,] jacobians, float[,,] jacobianDerivative) CalculateConstraints()
    {
        if (faceIndices == null)
        {
            Debug.LogError("PlanarFaces: not initialized.");
            return (null, null, null);
        }

        Vector3[] pos = ModelBuilderObject.GetPositions();
        Vector3[] vel = ModelBuilderObject.Velocities;

        Vector3[,] points = new Vector3[facesNum, verticesPerFace];
        Vector3[,] velocities = new Vector3[facesNum, verticesPerFace];

        // get vertex positions/velocities for each face
        for (int f = 0; f < facesNum; f++)
        {
            for (int v = 0; v < verticesPerFace; v++)
            {
                int idx = faceIndices[f, v];
                if (idx < 0 || idx >= pos.Length)
                {
                    Debug.LogWarning($"PlanarFaces.CalculateConstraints: invalid vertex index for face {f}, vertex {v}: {idx}. Using zero vector.");
                    points[f, v] = Vector3.zero;
                    velocities[f, v] = Vector3.zero;
                }
                else
                {
                    points[f, v] = pos[idx];
                    velocities[f, v] = vel[idx];
                }
            }
        }

        Vector3[] positionCentroids = new Vector3[facesNum];
        Vector3[] velocityCentroids = new Vector3[facesNum];
        Vector3[,] relativePositions = new Vector3[facesNum, verticesPerFace];
        Vector3[,] relativeVelocities = new Vector3[facesNum, verticesPerFace];
        for (int i = 0; i < facesNum; i++)
        {
            Vector3 positionSum = Vector3.zero;
            Vector3 velocitySum = Vector3.zero;
            for (int j = 0; j < verticesPerFace; j++) 
            { 
                positionSum += points[i, j];
                velocitySum += velocities[i, j];
            }
            positionCentroids[i] = positionSum / verticesPerFace;
            velocityCentroids[i] = velocitySum / verticesPerFace;
            for (int j = 0; j < verticesPerFace; j++)
            {
                relativePositions[i, j] = points[i, j] - positionCentroids[i];
                relativeVelocities[i, j] = velocities[i, j] - velocityCentroids[i];
            }
        }

        // Create meshgrid indices
        int[] vIndices = new int[verticesPerFace];
        for (int i = 0; i < verticesPerFace; i++) vIndices[i] = i;
        int[] fIndices = new int[facesNum];
        for (int i = 0; i < facesNum; i++) fIndices[i] = i;
        (int[,] VV, int[,] FF) = Utility.Meshgrid(vIndices, fIndices);

        // Initialize 5D array dFdP[F, V, N, D, D]
        float[][][][][] dFdP = (float[][][][][])Utility.CreateJaggedArray<float>(facesNum, verticesPerFace, positionNum, dim, dim);

        // Set identity matrix blocks: dFdP[FF, VV, faceIndices, :, :] = eye(D)
        for (int f = 0; f < facesNum; f++)
        {
            for (int v = 0; v < verticesPerFace; v++)
            {
                for (int d1 = 0; d1 < dim; d1++)
                {
                    for (int d2 = 0; d2 < dim; d2++)
                    {
                        dFdP[f][v][faceIndices[f, v]][d1][d2] = (d1 == d2) ? 1f : 0f;
                    }
                }
            }
        }

        // Initialize 4D array dcentroiddP[F, N, D, D]
        float[][][][] dcentroiddP = (float[][][][])Utility.CreateJaggedArray<float>(facesNum, positionNum, dim, dim);

        // Set identity matrix blocks divided by V: dcentroiddP[FF, faceIndices, :, :] = eye(D) / V
        float invV = 1f / verticesPerFace;
        for (int f = 0; f < facesNum; f++)
        {
            for (int v = 0; v < verticesPerFace; v++)
            {
                int faceIdx = faceIndices[f, v];
                for (int d1 = 0; d1 < dim; d1++)
                {
                    for (int d2 = 0; d2 < dim; d2++)
                    {
                        dcentroiddP[f][faceIdx][d1][d2] = (d1 == d2) ? invV : 0f;
                    }
                }
            }
        }

        // drelative_dP[F x V x D x N x D] = dFdP - dcentroiddP
        float[][][][][] drelativedP = (float[][][][][])Utility.CreateJaggedArray<float>(facesNum, verticesPerFace, dim, positionNum, dim);
        for (int f = 0; f < facesNum; f++)
        {
            for (int v = 0; v < verticesPerFace; v++)
            {
                for (int n = 0; n < positionNum; n++)
                {
                    for (int d1 = 0; d1 < dim; d1++)
                    {
                        for (int d2 = 0; d2 < dim; d2++)
                        {
                            float value = dFdP[f][v][n][d1][d2] - dcentroiddP[f][n][d1][d2];
                            drelativedP[f][v][d1][n][d2] = value;
                        }
                    }
                }
            }
        }

        var (cov, dcdp, dcdt, ddcdpdt) = CalcCovarianceVariables(
            relativePositions,
            relativeVelocities,
            drelativedP);

        var (normals, dndp, dndt, ddndpdt) = CalcNormalVariables(
            cov,
            dcdp,
            dcdt,
            ddcdpdt);

        // constraints = relative_positions @ normal[:, :, None] -> F x V
        float[] constraints = new float[facesNum * verticesPerFace];
        for (int f = 0; f < facesNum; f++)
        {
            float nx = normals[f][0];
            float ny = normals[f][1];
            float nz = normals[f][2];
            for (int v = 0; v < verticesPerFace; v++)
            {
                Vector3 rp = relativePositions[f, v];
                int cidx = f * verticesPerFace + v;
                constraints[cidx] = rp.x * nx + rp.y * ny + rp.z * nz;
            }
        }

        // jacobians = sum(drelative_dP * normal[:, None, :, None, None], axis=2)
        //           + sum(relative_positions[:, :, :, None, None] * dndp[:, None, :], axis=2)
        float[,,] jacobians = new float[facesNum * verticesPerFace, positionNum, dim];
        for (int f = 0; f < facesNum; f++)
        {
            for (int v = 0; v < verticesPerFace; v++)
            {
                Vector3 rp = relativePositions[f, v];
                int cidx = f * verticesPerFace + v;
                for (int n = 0; n < positionNum; n++)
                {
                    for (int d = 0; d < dim; d++)
                    {
                        float sum1 = 0f;
                        float sum2 = 0f;
                        for (int k = 0; k < dim; k++)
                        {
                            float rpComp = (k == 0) ? rp.x : (k == 1) ? rp.y : rp.z;
                            sum1 += drelativedP[f][v][k][n][d] * normals[f][k];
                            sum2 += rpComp * dndp[f][k][n][d];
                        }
                        jacobians[cidx, n, d] = sum1 + sum2;
                    }
                }
            }
        }

        // djac_dts = sum(drelative_dP * dndt[:, None, :, None, None], axis=2)
        //         + sum(relative_velocities[:, :, :, None, None] * dndp[:, None, :, :, :], axis=2)
        //         + sum(relative_positions[:, :, :, None, None] * ddndpdt[:, None, :, :, :], axis=2)
        float[,,] djac_dts = new float[facesNum * verticesPerFace, positionNum, dim];
        for (int f = 0; f < facesNum; f++)
        {
            for (int v = 0; v < verticesPerFace; v++)
            {
                Vector3 rp = relativePositions[f, v];
                Vector3 rv = relativeVelocities[f, v];
                int cidx = f * verticesPerFace + v;
                for (int n = 0; n < positionNum; n++)
                {
                    for (int d = 0; d < dim; d++)
                    {
                        float sum1 = 0f;
                        float sum2 = 0f;
                        float sum3 = 0f;
                        for (int k = 0; k < dim; k++)
                        {
                            float rpComp = (k == 0) ? rp.x : (k == 1) ? rp.y : rp.z;
                            float rvComp = (k == 0) ? rv.x : (k == 1) ? rv.y : rv.z;
                            sum1 += drelativedP[f][v][k][n][d] * dndt[f][k];
                            sum2 += rvComp * dndp[f][k][n][d];
                            sum3 += rpComp * ddndpdt[f][k][n][d];
                        }
                        djac_dts[cidx, n, d] = sum1 + sum2 + sum3;
                    }
                }
            }
        }

        // Already flattened to match: constraints = constraints.reshape(-1), jacobians/djac_dts = reshape(-1, N, D)

        return (constraints, jacobians, djac_dts);
    }
}
