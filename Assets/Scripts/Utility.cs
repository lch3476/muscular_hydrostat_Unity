using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;

public static class Utility
{

    public static (float[,], float[,]) StateToPosVel(float[] state)
    {
        int totalLength = state.Length;
        if (totalLength % 6 != 0)
        {
            throw new ArgumentException("State length must be divisible by 6.");
        }

        int n = totalLength / 6; // number of vertices

        float[] posFlat = new float[n * 3];
        float[] velFlat = new float[n * 3];

        // Split the array
        Array.Copy(state, 0, posFlat, 0, n * 3);
        Array.Copy(state, n * 3, velFlat, 0, n * 3);

        // Reshape into n x 3 matrices
        float[,] posMatrix = new float[n, 3];
        float[,] velMatrix = new float[n, 3];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                posMatrix[i, j] = posFlat[i * 3 + j];
                velMatrix[i, j] = velFlat[i * 3 + j];
            }
        }

        return (posMatrix, velMatrix);
    }

    public static T[] CreateInitializedArray<T>(int length, T initialValue)
    {
        T[] array = new T[length];
        for (int i = 0; i < length; ++i)
        {
            array[i] = initialValue;
        }
        return array;
    }

    public static float[] Flatten(Vector3[] vectors)
    {
        float[] flattened = new float[vectors.Length * 3];
        for (int i = 0; i < vectors.Length; ++i)
        {
            flattened[3*i] = vectors[i].x;
            flattened[3*i + 1] = vectors[i].y;
            flattened[3*i + 2] = vectors[i].z;
        }

        return flattened;
    }

    // Flatten a 2D array into 1D
    public static T[] Flatten<T>(T[,] array)
    {
        int rows = array.GetLength(0);
        int cols = array.GetLength(1);
        T[] flattened = new T[rows * cols];
        
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                flattened[i * cols + j] = array[i, j];
            }
        }
        
        return flattened;
    }

    public static T[] HorizontalStack<T>(params T[][] arrays)
    {
        if (arrays == null || arrays.Length == 0)
        {
            return new T[0];
        }

        // Calculate total length
        int totalLength = 0;
        foreach (var arr in arrays)
        {
            if (arr != null)
            {
                totalLength += arr.Length;
            }
        }

        T[] result = new T[totalLength];
        int currentIndex = 0;

        foreach (var arr in arrays)
        {
            if (arr != null)
            {
                System.Array.Copy(arr, 0, result, currentIndex, arr.Length);
                currentIndex += arr.Length;
            }
        }

        return result;
    }

    public static T[,] VerticalStack<T>(List<T[,]> arrays)
    {
        if (arrays == null || arrays.Count == 0)
            return new T[0, 0];

        int totalRows = 0;
        int cols = arrays[0].GetLength(1);

        // Check all arrays have the same number of columns
        foreach (var arr in arrays)
        {
            if (arr.GetLength(1) != cols)
                throw new ArgumentException("All arrays must have the same number of columns.");
            totalRows += arr.GetLength(0);
        }

        T[,] result = new T[totalRows, cols];
        int currentRow = 0;

        foreach (var arr in arrays)
        {
            int rows = arr.GetLength(0);
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[currentRow + i, j] = arr[i, j];
                }
            }
            currentRow += rows;
        }

        return result;
    }

    public static T[,] VerticalStack<T>(T[,] arr1, T[,] arr2)
    {
        int rows1 = arr1.GetLength(0);
        int cols1 = arr1.GetLength(1);
        int rows2 = arr2.GetLength(0);
        int cols2 = arr2.GetLength(1);

        if (cols1 != cols2)
        {
            Debug.LogError("The number of columns must be the same for vertical stacking.");
            return new T[0, 0];
        }

        T[,] result = new T[rows1 + rows2, cols1];

        // copy the first array
        for (int i = 0; i < rows1; i++)
        {
            for (int j = 0; j < cols1; j++)
            {
                result[i, j] = arr1[i, j];
            }
        }

        // copy the second array
        for (int i = 0; i < rows2; i++)
        {
            for (int j = 0; j < cols2; j++)
            {
                result[rows1 + i, j] = arr2[i, j];
            }
        }

        return result;
    }

    public static T[,] Diagonalize<T>(T[] diagElements)
    {
        if (diagElements == null)
        {
            return new T[0, 0];
        }

        int n = diagElements.Length;
        T[,] result = new T[n, n];
        for (int i = 0; i < n; i++)
        {
            result[i, i] = diagElements[i];
        }

        return result;
    }

    public static T[,] Transpose<T>(T[,] matrix)
    {
        if (matrix == null)
        {
            return null;
        }

        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        T[,] result = new T[cols, rows];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                result[j, i] = matrix[i, j];
            }
        }

        return result;
    }

    // Add `scalar` to the diagonal entries of a square float matrix in-place.
    public static void AddIdentityInPlace(float[,] matrix, float scalar)
    {
        if (matrix == null)
        {
            Debug.LogError("AddIdentityInPlace: matrix is null.");
            return;
        }

        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        if (rows != cols)
        {
            Debug.LogError($"AddIdentityInPlace: matrix must be square (rows={rows}, cols={cols}).");
            return;
        }

        for (int i = 0; i < rows; i++)
        {
            matrix[i, i] += scalar;
        }
    }

    public static float[,] MatrixMultiply(float[,] lhs, float[,] rhs)
    {
        if (lhs == null || rhs == null) return null;

        int lhsRows = lhs.GetLength(0);
        int lhsCols = lhs.GetLength(1);
        int rhsRows = rhs.GetLength(0);
        int rhsCols = rhs.GetLength(1);

        if (lhsCols != rhsRows)
        {
            Debug.LogError($"MatrixMultiply: inner dimensions must match (lhsCols={lhsCols}, rhsRows={rhsRows}).");
            return null;
        }

        float[,] result = new float[lhsRows, rhsCols];

        // Transpose rhs to improve cache locality: access rhsT[row, k] == rhs[k, row]
        float[,] rhsT = new float[rhsCols, rhsRows];
        for (int r = 0; r < rhsRows; r++)
        {
            for (int c = 0; c < rhsCols; c++)
            {
                rhsT[c, r] = rhs[r, c];
            }
        }

        // Parallelize over rows of lhs. Each row writes to distinct output rows, so safe.
        Parallel.For(0, lhsRows, i =>
        {
            for (int j = 0; j < rhsCols; j++)
            {
                float sum = 0f;
                // dot product of row i of lhs and row j of rhsT (which is column j of rhs)
                for (int k = 0; k < lhsCols; k++)
                {
                    sum += lhs[i, k] * rhsT[j, k];
                }
                result[i, j] = sum;
            }
        });

        return result;
    }

    public static float[] MatrixMultiply(float[,] mat, float[] vec)
    {
        if (mat == null || vec == null) return null;

        int rows = mat.GetLength(0);
        int cols = mat.GetLength(1);
        if (vec.Length != cols)
        {
            Debug.LogError($"MatrixMultiply (mat-vec): vector length {vec.Length} does not match matrix cols {cols}.");
            return null;
        }

        float[] result = new float[rows];
        for (int i = 0; i < rows; i++)
        {
            float s = 0f;
            for (int j = 0; j < cols; j++) s += mat[i, j] * vec[j];
            result[i] = s;
        }

        return result;
    }

    public static float[] MatrixMultiply(float[] vec, float[,] mat)
    {
        if (vec == null || mat == null) return null;
        
        int rows = mat.GetLength(0);
        int cols = mat.GetLength(1);

        if (vec.Length != rows)
        {
            Debug.LogError($"MatrixMultiply (vec-mat): length {vec.Length} does not match mat rows {rows}.");
            return null;
        }

        float[] result = new float[cols];
        for (int j = 0; j < cols; j++)
        {
            float s = 0f;
            for (int i = 0; i < rows; i++) s += vec[i] * mat[i, j];
            result[j] = s;
        }

        return result;
    }

    // Element-wise multiplication of two 1D arrays: result[i] = lhs[i] * rhs[i]
    public static T[] MatrixMultiply<T>(T[] lhs, T[] rhs)
    {
        if (lhs == null || rhs == null)
        {
            Debug.LogError("MatrixMultiply (element-wise): one or both arrays are null.");
            return null;
        }

        if (lhs.Length != rhs.Length)
        {
            Debug.LogError($"MatrixMultiply (element-wise): array lengths must match (lhs={lhs.Length}, rhs={rhs.Length}).");
            return null;
        }

        T[] result = new T[lhs.Length];
        for (int i = 0; i < lhs.Length; i++)
        {
            result[i] = (dynamic)lhs[i] * (dynamic)rhs[i];
        }

        return result;
    }

        // Multiply each Vector3 in the array by a scalar value
    public static T[] MatrixMultiply<T>(T[] mat, float scalar)
    {
        if (mat == null)
        {
            Debug.LogError("ScalarMultiply: input vectors array is null.");
            return null;
        }

        T[] result = new T[mat.Length];
        for (int i = 0; i < mat.Length; i++)
        {
            result[i] = (dynamic)mat[i] * scalar;
        }
        return result;
    }

    public static T[] MatrixMultiply<T>(float scalar, T[] mat)
    {
        if (mat == null)
        {
            Debug.LogError("ScalarMultiply: input vectors array is null.");
            return null;
        }

        return MatrixMultiply(mat, scalar);
    }

    public static T[,] MatrixAdd<T>(T[,] lhs, T[,] rhs)
    {
        if (lhs == null || rhs == null)
        {
            Debug.LogError("MatrixAdd: one or both matrices are null.");
            return null;
        }

        int lhsRows = lhs.GetLength(0);
        int lhsCols = lhs.GetLength(1);
        int rhsRows = rhs.GetLength(0);
        int rhsCols = rhs.GetLength(1);

        if (lhsRows != rhsRows || lhsCols != rhsCols)
        {
            Debug.LogError($"MatrixAdd: matrix dimensions must match (lhs={lhsRows}x{lhsCols}, rhs={rhsRows}x{rhsCols}).");
            return null;
        }

        T[,] result = new T[lhsRows, lhsCols];
        for (int i = 0; i < lhsRows; i++)
        {
            for (int j = 0; j < lhsCols; j++)
            {
                result[i, j] = (dynamic)lhs[i, j] + (dynamic)rhs[i, j];
            }
        }

        return result;
    }


    // Convert flat float array (n*3) to Vector3[]
    public static Vector3[] UnflattenToVector3Array(float[] flat)
    {
        if (flat == null) return null;
        if (flat.Length % 3 != 0)
        {
            Debug.LogError($"UnflattenToVector3Array: flat length {flat.Length} is not a multiple of 3.");
            return null;
        }
        int n = flat.Length / 3;
        Vector3[] outArr = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            outArr[i] = new Vector3(flat[3*i], flat[3*i + 1], flat[3*i + 2]);
        }
        return outArr;
    }

    // Solve linear system A x = b using Gaussian elimination with partial pivoting.
    // Returns null on failure.
    public static float[] SolveLinearSystem(float[,] A, float[] b)
    {
        if (A == null || b == null) return null;
        int n = A.GetLength(0);
        if (A.GetLength(1) != n || b.Length != n)
        {
            Debug.LogError("SolveLinearSystem: A must be square and b must have matching length.");
            return null;
        }

        // Make copies because we'll modify
        float[,] mat = new float[n, n];
        float[] rhs = new float[n];
        for (int i = 0; i < n; i++)
        {
            rhs[i] = b[i];
            for (int j = 0; j < n; j++) mat[i, j] = A[i, j];
        }

        int[] pivot = new int[n];
        for (int i = 0; i < n; i++) pivot[i] = i;

        for (int k = 0; k < n; k++)
        {
            // Partial pivot
            int maxRow = k;
            float maxVal = Mathf.Abs(mat[k, k]);
            for (int i = k + 1; i < n; i++)
            {
                float v = Mathf.Abs(mat[i, k]);
                if (v > maxVal) { maxVal = v; maxRow = i; }
            }
            if (maxVal < 1e-12f)
            {
                Debug.LogError("SolveLinearSystem: matrix is singular or near-singular.");
                return null;
            }
            if (maxRow != k)
            {
                // swap rows k and maxRow in mat and rhs
                for (int j = k; j < n; j++)
                {
                    float tmp = mat[k, j]; mat[k, j] = mat[maxRow, j]; mat[maxRow, j] = tmp;
                }
                float tmpb = rhs[k]; rhs[k] = rhs[maxRow]; rhs[maxRow] = tmpb;
            }

            // Eliminate
            float pivotVal = mat[k, k];
            for (int i = k + 1; i < n; i++)
            {
                float factor = mat[i, k] / pivotVal;
                mat[i, k] = 0f;
                for (int j = k + 1; j < n; j++) mat[i, j] -= factor * mat[k, j];
                rhs[i] -= factor * rhs[k];
            }
        }

        // Back substitution
        float[] x = new float[n];
        for (int i = n - 1; i >= 0; i--)
        {
            float s = rhs[i];
            for (int j = i + 1; j < n; j++) s -= mat[i, j] * x[j];
            x[i] = s / mat[i, i];
        }
        return x;
    }

    // Overloaded Reshape for 3D array input
    public static T[,] Reshape<T>(T[,,] flatArray, int newRows, int newCols)
    {
        if (flatArray == null)
        {
            return null;
        }

        int dim0 = flatArray.GetLength(0);
        int dim1 = flatArray.GetLength(1);
        int dim2 = flatArray.GetLength(2);
        int totalElements = dim0 * dim1 * dim2;
        int rows = newRows;
        int cols = newCols;

        if (rows == -1 && cols == -1)
        {
            Debug.LogError("Cannot infer both rows and columns. One must be specified.");
            return null;
        }
        if (rows == -1)
        {
            if (cols <= 0 || totalElements % cols != 0)
            {
                Debug.LogError($"Cannot infer rows: array size {totalElements} is not divisible by cols {cols}.");
                return null;
            }
            rows = totalElements / cols;
        }
        if (cols == -1)
        {
            if (rows <= 0 || totalElements % rows != 0)
            {
                Debug.LogError($"Cannot infer cols: array size {totalElements} is not divisible by rows {rows}.");
                return null;
            }
            cols = totalElements / rows;
        }
        if (rows * cols != totalElements)
        {
            Debug.LogError($"Cannot reshape 3D array of size {dim0}x{dim1}x{dim2} (total {totalElements}) into shape ({rows}, {cols}).");
            return null;
        }

        T[,] reshapedArray = new T[rows, cols];
        int index = 0;

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                int flatIdx = index;
                int d0 = flatIdx / (dim1 * dim2);
                int d1 = (flatIdx / dim2) % dim1;
                int d2 = flatIdx % dim2;
                reshapedArray[i, j] = flatArray[d0, d1, d2];
                index++;
            }
        }

        return reshapedArray;
    }

    public static float Determinant3x3(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        return Vector3.Dot(v1, Vector3.Cross(v2, v3));
    }

    // Extract a column from a 2D array (equivalent to arr[:, colIndex] in NumPy)
    public static T[] GetColumn<T>(T[,] array, int columnIndex)
    {
        if (array == null)
        {
            Debug.LogError("GetColumn: array is null.");
            return null;
        }

        int rows = array.GetLength(0);
        int cols = array.GetLength(1);

        if (columnIndex < 0 || columnIndex >= cols)
        {
            Debug.LogError($"GetColumn: columnIndex {columnIndex} out of range [0, {cols}).");
            return null;
        }

        T[] result = new T[rows];
        for (int i = 0; i < rows; i++)
        {
            result[i] = array[i, columnIndex];
        }
        return result;
    }

    public static void PrintVectors(List<Vector3> vectors, string label = "Vectors")
    {
        if (vectors == null)
        {
            Debug.Log($"{label}: null");
            return;
        }
        PrintVectors(vectors.ToArray(), label);
    }

    public static void PrintVectors(Vector3[] vectors, string label = "Vectors")
    {
        if (vectors == null)
        {
            Debug.Log($"{label}: null");
            return;
        }

        Debug.Log($"{label} (Length: {vectors.Length}):");
        for (int i = 0; i < vectors.Length; i++)
        {
            Debug.Log($"  [{i}]: ({vectors[i].x:F3}, {vectors[i].y:F3}, {vectors[i].z:F3})");
        }
    }

    public static void PrintArray<T>(T[] array, string label = "Array")
    {
        if (array == null)
        {
            Debug.Log($"{label}: null");
            return;
        }

        Debug.Log($"{label} (Length: {array.Length}):");
        for (int i = 0; i < array.Length; i++)
        {
            Debug.Log($"  [{i}]: {array[i]}");
        }
    }

    public static void PrintArray<T>(T[,] array, string label = "Array")
    {
        if (array == null)
        {
            Debug.Log($"{label}: null");
            return;
        }

        int rows = array.GetLength(0);
        int cols = array.GetLength(1);
        
        Debug.Log($"{label} (Shape: {rows}x{cols}):");
        for (int i = 0; i < rows; i++)
        {
            string rowStr = $"  [{i}]: ";
            for (int j = 0; j < cols; j++)
            {
                rowStr += $"{array[i, j],8} ";
            }
            Debug.Log(rowStr);
        }
    }

}

