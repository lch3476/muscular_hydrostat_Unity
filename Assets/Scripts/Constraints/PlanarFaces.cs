using UnityEngine;
using System.Collections.Generic;

public class PlanarFaces : Constraint
{
    int[,] faceIndices; // F x V
    float[][][][][] cachedRelativeDerivative; // F x V x D x N x D
    int facesNum; // F
    int verticesPerFace; // V
    int positionNum; // N
    int dim; // D

    // Global planarity score across all faces.
    // Computed as: 1 - mean(lambda0 / (lambda0 + lambda1 + lambda2 + eps)),
    // where lambda0 is the smallest covariance eigenvalue of each face.
    // Higher value means more planar overall (closer to 1 is flatter).
    float cachedGlobalPlanarityScore;


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
        
        var vertexIndexMap = new Dictionary<GameObject, int>(positionNum);
        for (int i = 0; i < positionNum; i++)
            vertexIndexMap[ModelBuilderObject.Vertices[i]] = i;
        
        for (int f = 0; f < facesNum; f++)
        {
            var face = faces[f];
            if (face == null) continue;
            
            faceIndices[f, 0] = vertexIndexMap.TryGetValue(face.Item1, out int idx1) ? idx1 : -1;
            faceIndices[f, 1] = vertexIndexMap.TryGetValue(face.Item2, out int idx2) ? idx2 : -1;
            faceIndices[f, 2] = vertexIndexMap.TryGetValue(face.Item3, out int idx3) ? idx3 : -1;
            faceIndices[f, 3] = vertexIndexMap.TryGetValue(face.Item4, out int idx4) ? idx4 : -1;
            
            for (int v = 0; v < verticesPerFace; v++)
            {
                if (faceIndices[f, v] < 0 || faceIndices[f, v] >= positionNum)
                    Debug.LogError($"PlanarFaces: invalid vertex index {faceIndices[f, v]} for face {f}, vertex {v}");
            }
        }

        cachedRelativeDerivative = BuildRelativeDerivative();
    }

    private float[][][][][] BuildRelativeDerivative()
    {
        // relativePositionDerivative[F x V x D x N x D]
        float[][][][][] relativePositionDerivative = (float[][][][][])Utility.CreateJaggedArray<float>(facesNum, verticesPerFace, dim, positionNum, dim);
        float inverseVertexCount = 1f / verticesPerFace;

        for (int f = 0; f < facesNum; f++)
        {
            for (int v = 0; v < verticesPerFace; v++)
            {
                int vertexIdx = faceIndices[f, v];
                if (vertexIdx < 0 || vertexIdx >= positionNum)
                    continue;

                for (int d = 0; d < dim; d++)
                {
                    // identity at vertex position
                    relativePositionDerivative[f][v][d][vertexIdx][d] += 1f;
                    
                    // -1/V for all vertices in centroid
                    for (int vc = 0; vc < verticesPerFace; vc++)
                    {
                        int centroidVertexIdx = faceIndices[f, vc];
                        if (centroidVertexIdx >= 0 && centroidVertexIdx < positionNum)
                            relativePositionDerivative[f][v][d][centroidVertexIdx][d] -= inverseVertexCount;
                    }
                }
            }
        }

        return relativePositionDerivative;
    }

    private static void SymmetricEigs3(float[,] inputMatrix, out float[] eigenValues, out float[,] eigenVectors)
    {
        eigenValues = new float[3];
        eigenVectors = new float[3, 3];
        float[,] workingMatrix = new float[3, 3];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                workingMatrix[i, j] = inputMatrix[i, j];
                eigenVectors[i, j] = (i == j) ? 1f : 0f;
            }
        }

        for (int iter = 0; iter < 50; iter++)
        {
            int p = 0;
            int q = 1;
            float max = Mathf.Abs(workingMatrix[0, 1]);
            if (Mathf.Abs(workingMatrix[0, 2]) > max) 
            { 
                max = Mathf.Abs(workingMatrix[0, 2]);
                p = 0;
                q = 2;
            }
            if (Mathf.Abs(workingMatrix[1, 2]) > max)
            { 
                max = Mathf.Abs(workingMatrix[1, 2]);
                p = 1;
                q = 2;
            }
            if (max < 1e-10f) break;

            float matrixPP = workingMatrix[p, p];
            float matrixQQ = workingMatrix[q, q];
            float matrixPQ = workingMatrix[p, q];
            float rotationAngle = 0.5f * Mathf.Atan2(2f * matrixPQ, matrixQQ - matrixPP);
            float cosineValue = Mathf.Cos(rotationAngle);
            float sineValue = Mathf.Sin(rotationAngle);

            for (int i = 0; i < 3; i++)
            {
                float matrixIP = workingMatrix[i, p];
                float matrixIQ = workingMatrix[i, q];
                workingMatrix[i, p] = cosineValue * matrixIP - sineValue * matrixIQ;
                workingMatrix[i, q] = sineValue * matrixIP + cosineValue * matrixIQ;
            }
            for (int j = 0; j < 3; j++)
            {
                float matrixPJ = workingMatrix[p, j];
                float matrixQJ = workingMatrix[q, j];
                workingMatrix[p, j] = cosineValue * matrixPJ - sineValue * matrixQJ;
                workingMatrix[q, j] = sineValue * matrixPJ + cosineValue * matrixQJ;
            }
            workingMatrix[p, p] = cosineValue * cosineValue * matrixPP - 2f * sineValue * cosineValue * matrixPQ + sineValue * sineValue * matrixQQ;
            workingMatrix[q, q] = sineValue * sineValue * matrixPP + 2f * sineValue * cosineValue * matrixPQ + cosineValue * cosineValue * matrixQQ;
            workingMatrix[p, q] = 0f;
            workingMatrix[q, p] = 0f;

            for (int i = 0; i < 3; i++)
            {
                float eigenVectorIP = eigenVectors[i, p];
                float eigenVectorIQ = eigenVectors[i, q];
                eigenVectors[i, p] = cosineValue * eigenVectorIP - sineValue * eigenVectorIQ;
                eigenVectors[i, q] = sineValue * eigenVectorIP + cosineValue * eigenVectorIQ;
            }
        }

        eigenValues[0] = workingMatrix[0, 0];
        eigenValues[1] = workingMatrix[1, 1];
        eigenValues[2] = workingMatrix[2, 2];

        for (int i = 0; i < 2; i++)
        {
            for (int j = i + 1; j < 3; j++)
            {
                if (eigenValues[j] < eigenValues[i])
                {
                    float tempEigenValue = eigenValues[i];
                    eigenValues[i] = eigenValues[j];
                    eigenValues[j] = tempEigenValue;
                    for (int r = 0; r < 3; r++)
                    {
                        float tempEigenVector = eigenVectors[r, i];
                        eigenVectors[r, i] = eigenVectors[r, j];
                        eigenVectors[r, j] = tempEigenVector;
                    }
                }
            }
        }
    }

    private (float[][][] covarianceMatrices, float[][][][][] covariancePositionDerivative, float[][][] covarianceTimeDerivative, float[][][][][] covarianceSecondPositionTimeDerivative)
    CalcCovarianceVariables(
        Vector3[,] relativePositions,
        Vector3[,] relativeVelocities,
        float[][][][][] relativePositionDerivative)
    {
        int faces = relativePositions.GetLength(0);
        int verts = relativePositions.GetLength(1);
        int degreesOfFreedom = Mathf.Max(1, verts - 1);
        float inverseDegreesOfFreedom = 1f / degreesOfFreedom;

        float[][][] covarianceMatrices = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        float[][][][][] covariancePositionDerivative = (float[][][][][])Utility.CreateJaggedArray<float>(faces, dim, dim, positionNum, dim);
        float[][][] covarianceTimeDerivative = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        float[][][][][] covarianceSecondPositionTimeDerivative = (float[][][][][])Utility.CreateJaggedArray<float>(faces, dim, dim, positionNum, dim);

        // covarianceMatrices = relative_positions^T * relative_positions / degreesOfFreedom
        for (int f = 0; f < faces; f++)
        {
            for (int v = 0; v < verts; v++)
            {
                Vector3 relativePosition = relativePositions[f, v];
                covarianceMatrices[f][0][0] += relativePosition.x * relativePosition.x;
                covarianceMatrices[f][0][1] += relativePosition.x * relativePosition.y;
                covarianceMatrices[f][0][2] += relativePosition.x * relativePosition.z;
                covarianceMatrices[f][1][0] += relativePosition.y * relativePosition.x;
                covarianceMatrices[f][1][1] += relativePosition.y * relativePosition.y;
                covarianceMatrices[f][1][2] += relativePosition.y * relativePosition.z;
                covarianceMatrices[f][2][0] += relativePosition.z * relativePosition.x;
                covarianceMatrices[f][2][1] += relativePosition.z * relativePosition.y;
                covarianceMatrices[f][2][2] += relativePosition.z * relativePosition.z;
            }

            for (int i = 0; i < dim; i++)
                for (int j = 0; j < dim; j++)
                    covarianceMatrices[f][i][j] *= inverseDegreesOfFreedom;
        }

        for (int f = 0; f < faces; f++)
        {
            for (int v = 0; v < verts; v++)
            {
                Vector3 relativePosition = relativePositions[f, v];
                Vector3 relativeVelocity = relativeVelocities[f, v];
                for (int d = 0; d < dim; d++)
                {
                    float relativePositionComponent = (d == 0) ? relativePosition.x : (d == 1) ? relativePosition.y : relativePosition.z;
                    float relativeVelocityComponent = (d == 0) ? relativeVelocity.x : (d == 1) ? relativeVelocity.y : relativeVelocity.z;
                    for (int j = 0; j < dim; j++)
                    {
                        for (int n = 0; n < positionNum; n++)
                        {
                            for (int dd = 0; dd < dim; dd++)
                            {
                                covariancePositionDerivative[f][d][j][n][dd] += relativePositionComponent * relativePositionDerivative[f][v][j][n][dd];
                                covarianceSecondPositionTimeDerivative[f][d][j][n][dd] += relativeVelocityComponent * relativePositionDerivative[f][v][j][n][dd];
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
                            float symmetricCovariancePositionDerivative = (covariancePositionDerivative[f][i][j][n][d] + covariancePositionDerivative[f][j][i][n][d]) * inverseDegreesOfFreedom;
                            float symmetricCovarianceSecondPositionTimeDerivative = (covarianceSecondPositionTimeDerivative[f][i][j][n][d] + covarianceSecondPositionTimeDerivative[f][j][i][n][d]) * inverseDegreesOfFreedom;
                            covariancePositionDerivative[f][i][j][n][d] = symmetricCovariancePositionDerivative;
                            covariancePositionDerivative[f][j][i][n][d] = symmetricCovariancePositionDerivative;
                            covarianceSecondPositionTimeDerivative[f][i][j][n][d] = symmetricCovarianceSecondPositionTimeDerivative;
                            covarianceSecondPositionTimeDerivative[f][j][i][n][d] = symmetricCovarianceSecondPositionTimeDerivative;
                        }
                    }
                }
            }
        }

        // covarianceTimeDerivative = (relative_velocities^T * relative_positions + transpose) / degreesOfFreedom
        for (int f = 0; f < faces; f++)
        {
            float[,] covarianceTimeSingleFaceDerivative = new float[dim, dim];
            for (int v = 0; v < verts; v++)
            {
                Vector3 relativeVelocity = relativeVelocities[f, v];
                Vector3 relativePosition = relativePositions[f, v];
                covarianceTimeSingleFaceDerivative[0, 0] += relativeVelocity.x * relativePosition.x;
                covarianceTimeSingleFaceDerivative[0, 1] += relativeVelocity.x * relativePosition.y;
                covarianceTimeSingleFaceDerivative[0, 2] += relativeVelocity.x * relativePosition.z;
                covarianceTimeSingleFaceDerivative[1, 0] += relativeVelocity.y * relativePosition.x;
                covarianceTimeSingleFaceDerivative[1, 1] += relativeVelocity.y * relativePosition.y;
                covarianceTimeSingleFaceDerivative[1, 2] += relativeVelocity.y * relativePosition.z;
                covarianceTimeSingleFaceDerivative[2, 0] += relativeVelocity.z * relativePosition.x;
                covarianceTimeSingleFaceDerivative[2, 1] += relativeVelocity.z * relativePosition.y;
                covarianceTimeSingleFaceDerivative[2, 2] += relativeVelocity.z * relativePosition.z;
            }

            for (int i = 0; i < dim; i++)
                for (int j = 0; j < dim; j++)
                    covarianceTimeDerivative[f][i][j] = (covarianceTimeSingleFaceDerivative[i, j] + covarianceTimeSingleFaceDerivative[j, i]) * inverseDegreesOfFreedom;
        }

        return (covarianceMatrices, covariancePositionDerivative, covarianceTimeDerivative, covarianceSecondPositionTimeDerivative);
    }

    private (float[][] normals, float[][][][] normalPositionDerivative, float[][] normalTimeDerivative, float[][][][] normalSecondPositionTimeDerivative)
    CalcNormalVariables(
        float[][][] covarianceMatrices,
        float[][][][][] covariancePositionDerivative,
        float[][][] covarianceTimeDerivative,
        float[][][][][] covarianceSecondPositionTimeDerivative)
    {
        int faces = covarianceMatrices.Length;
        float[][] eigenvalues = new float[faces][];
        float[][][] eigenvectors = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        float[][][] eigenVectorsTranspose = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);

        for (int f = 0; f < faces; f++)
        {
            float[,] covarianceMatrix = new float[dim, dim];
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    covarianceMatrix[i, j] = covarianceMatrices[f][i][j];
                }
            }

            SymmetricEigs3(covarianceMatrix, out float[] faceEigenValues, out float[,] faceEigenVectors);
            eigenvalues[f] = new float[dim];
            for (int i = 0; i < dim; i++)
                eigenvalues[f][i] = faceEigenValues[i];
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    eigenvectors[f][i][j] = faceEigenVectors[i, j];
                    eigenVectorsTranspose[f][i][j] = faceEigenVectors[j, i];
                }
            }
        }

        float scoreSum = 0f;
        const float eps = 1e-8f;
        for (int f = 0; f < faces; f++)
        {
            float l0 = eigenvalues[f][0];
            float l1 = eigenvalues[f][1];
            float l2 = eigenvalues[f][2];
            float denom = l0 + l1 + l2;
            scoreSum += l0 / (denom + eps);
        }
        float meanNonPlanarity = faces > 0 ? scoreSum / faces : 0f;
        cachedGlobalPlanarityScore = 1f - meanNonPlanarity;

        float[][] normals = (float[][])Utility.CreateJaggedArray<float>(faces, dim);
        for (int f = 0; f < faces; f++)
            for (int d = 0; d < dim; d++)
                normals[f][d] = eigenvectors[f][d][0];

        float[][][] inverseEigenvalueDifferences = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        for (int f = 0; f < faces; f++)
        {
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    float diff = eigenvalues[f][i] - eigenvalues[f][j];
                    inverseEigenvalueDifferences[f][i][j] = Mathf.Abs(diff) > 0f ? 1f / diff : 0f;
                }
            }
        }

        float[][][] minimumInverseEigenvalueDifferences = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        for (int f = 0; f < faces; f++)
        {
            for (int a = 0; a < dim; a++)
            {
                float val = inverseEigenvalueDifferences[f][0][a];
                for (int b = 0; b < dim; b++)
                    minimumInverseEigenvalueDifferences[f][a][b] = val;
            }
        }

        float[][][][] normalPositionDerivative = BespokeContraction(minimumInverseEigenvalueDifferences, eigenVectorsTranspose, covariancePositionDerivative, normals, eigenvectors);

        float[][][] eigenvectorTimeDerivative = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        for (int f = 0; f < faces; f++)
        {
            float[,] intermediateProduct = new float[dim, dim];
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < dim; k++)
                    {
                        sum += covarianceTimeDerivative[f][i][k] * eigenvectors[f][k][j];
                    }
                    intermediateProduct[i, j] = sum;
                }
            }

            float[,] projectedTimeDerivative = new float[dim, dim];
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < dim; k++)
                    {
                        sum += eigenVectorsTranspose[f][i][k] * intermediateProduct[k, j];
                    }
                    projectedTimeDerivative[i, j] = sum;
                }
            }

            for (int j = 0; j < dim; j++)
            {
                for (int k = 0; k < dim; k++)
                {
                    float sum = 0f;
                    for (int i = 0; i < dim; i++)
                    {
                        float val = -inverseEigenvalueDifferences[f][i][j] * projectedTimeDerivative[i, j];
                        sum += val * eigenVectorsTranspose[f][i][k];
                    }
                    eigenvectorTimeDerivative[f][k][j] = sum;
                }
            }
        }

        float[][] normalTimeDerivative = (float[][])Utility.CreateJaggedArray<float>(faces, dim);
        for (int f = 0; f < faces; f++)
            for (int d = 0; d < dim; d++)
                normalTimeDerivative[f][d] = eigenvectorTimeDerivative[f][d][0];

        float[][] eigenvalueTimeDerivative = (float[][])Utility.CreateJaggedArray<float>(faces, dim);
        for (int f = 0; f < faces; f++)
        {
            float[,] intermediateProduct = new float[dim, dim];
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < dim; k++)
                    {
                        sum += covarianceTimeDerivative[f][i][k] * eigenvectors[f][k][j];
                    }
                    intermediateProduct[i, j] = sum;
                }
            }

            float[,] projectedTimeDerivative = new float[dim, dim];
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < dim; k++)
                    {
                        sum += eigenVectorsTranspose[f][i][k] * intermediateProduct[k, j];
                    }
                    projectedTimeDerivative[i, j] = sum;
                }
            }

            for (int i = 0; i < dim; i++)
                eigenvalueTimeDerivative[f][i] = projectedTimeDerivative[i, i];
        }

        float[][][] inverseEigenvalueDifferenceTimeDerivative = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        for (int f = 0; f < faces; f++)
        {
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    float diff = eigenvalueTimeDerivative[f][i] - eigenvalueTimeDerivative[f][j];
                    float denom = eigenvalues[f][i] - eigenvalues[f][j];
                    if (Mathf.Abs(denom) > 0f && Mathf.Abs(diff) > 0f)
                        inverseEigenvalueDifferenceTimeDerivative[f][i][j] = -diff / (denom * denom);
                    else
                        inverseEigenvalueDifferenceTimeDerivative[f][i][j] = 0f;
                }
            }
        }

        float[][][] minimumInverseEigenvalueDifferenceTimeDerivative = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        for (int f = 0; f < faces; f++)
        {
            for (int a = 0; a < dim; a++)
            {
                float val = inverseEigenvalueDifferenceTimeDerivative[f][0][a];
                for (int b = 0; b < dim; b++)
                    minimumInverseEigenvalueDifferenceTimeDerivative[f][a][b] = val;
            }
        }

        float[][][] eigenvectorTimeDerivativeTranspose = (float[][][])Utility.CreateJaggedArray<float>(faces, dim, dim);
        for (int f = 0; f < faces; f++)
            for (int i = 0; i < dim; i++)
                for (int j = 0; j < dim; j++)
                    eigenvectorTimeDerivativeTranspose[f][i][j] = eigenvectorTimeDerivative[f][j][i];

        float[][][][] firstTerm = BespokeContraction(minimumInverseEigenvalueDifferenceTimeDerivative, eigenVectorsTranspose, covariancePositionDerivative, normals, eigenvectors);
        float[][][][] secondTerm = BespokeContraction(minimumInverseEigenvalueDifferences, eigenvectorTimeDerivativeTranspose, covariancePositionDerivative, normals, eigenvectors);
        float[][][][] thirdTerm = BespokeContraction(minimumInverseEigenvalueDifferences, eigenVectorsTranspose, covarianceSecondPositionTimeDerivative, normals, eigenvectors);
        float[][][][] fourthTerm = BespokeContraction(minimumInverseEigenvalueDifferences, eigenVectorsTranspose, covariancePositionDerivative, normalTimeDerivative, eigenvectors);
        float[][][][] fifthTerm = BespokeContraction(minimumInverseEigenvalueDifferences, eigenVectorsTranspose, covariancePositionDerivative, normals, eigenvectorTimeDerivative);

        float[][][][] normalSecondPositionTimeDerivative = (float[][][][])Utility.CreateJaggedArray<float>(faces, dim, positionNum, dim);
        for (int f = 0; f < faces; f++)
        {
            for (int d = 0; d < dim; d++)
            {
                for (int n = 0; n < positionNum; n++)
                {
                    for (int dd = 0; dd < dim; dd++)
                    {
                        normalSecondPositionTimeDerivative[f][d][n][dd] = firstTerm[f][d][n][dd]
                            + secondTerm[f][d][n][dd]
                            + thirdTerm[f][d][n][dd]
                            + fourthTerm[f][d][n][dd]
                            + fifthTerm[f][d][n][dd];
                    }
                }
            }
        }

        return (normals, normalPositionDerivative, normalTimeDerivative, normalSecondPositionTimeDerivative);
    }

    private float[][][][] BespokeContraction(
        float[][][] inverseEigenvalueDifferences,
        float[][][] eigenVectorsTranspose,
        float[][][][][] covariancePositionDerivative,
        float[][] normals,
        float[][][] eigenVectors)
    {
        int faces = inverseEigenvalueDifferences.Length;
        float[][][][] contractionResult = (float[][][][])Utility.CreateJaggedArray<float>(faces, dim, positionNum, dim);

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
                                float eigenvalueTerm = inverseEigenvalueDifferences[f][a][b] * eigenVectorsTranspose[f][a][b];
                                float eigenvectorTerm = eigenVectors[f][p][a];
                                for (int j = 0; j < dim; j++)
                                {
                                    sum += eigenvalueTerm
                                        * covariancePositionDerivative[f][b][j][n][d]
                                        * normals[f][j]
                                        * eigenvectorTerm;
                                }
                            }
                        }
                        contractionResult[f][p][n][d] = sum;
                    }
                }
            }
        }

        return contractionResult;
    }

    public override string GenerateDataText()
    {
        return $"Global Planarity: {cachedGlobalPlanarityScore}";
    }

    public override (float[] constraints, float[,,] jacobians, float[,,] jacobianDerivative) CalculateConstraints()
    {
        if (faceIndices == null || cachedRelativeDerivative == null)
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

        var (covarianceMatrices, covariancePositionDerivative, covarianceTimeDerivative, covarianceSecondPositionTimeDerivative) = CalcCovarianceVariables(
            relativePositions,
            relativeVelocities,
            cachedRelativeDerivative);

        var (normals, normalPositionDerivative, normalTimeDerivative, normalSecondPositionTimeDerivative) = CalcNormalVariables(
            covarianceMatrices,
            covariancePositionDerivative,
            covarianceTimeDerivative,
            covarianceSecondPositionTimeDerivative);

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
                            sum1 += cachedRelativeDerivative[f][v][k][n][d] * normals[f][k];
                            sum2 += rpComp * normalPositionDerivative[f][k][n][d];
                        }
                        jacobians[cidx, n, d] = sum1 + sum2;
                    }
                }
            }
        }

        float[,,] jacobianDerivative = new float[facesNum * verticesPerFace, positionNum, dim];
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
                            sum1 += cachedRelativeDerivative[f][v][k][n][d] * normalTimeDerivative[f][k];
                            sum2 += rvComp * normalPositionDerivative[f][k][n][d];
                            sum3 += rpComp * normalSecondPositionTimeDerivative[f][k][n][d];
                        }
                        jacobianDerivative[cidx, n, d] = sum1 + sum2 + sum3;
                    }
                }
            }
        }

        return (constraints, jacobians, jacobianDerivative);
    }
}
