using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

// Constant volume constraint for 3D cells.
// Converted from Python implementation: computes per-cell volumes from tetrahedra
// formed by an apex and triangulated faces, and the Jacobian and its time derivative.
public class ConstantVolume : Constraint
{
    // Per-cell storage
    private float[] initialVolumes;

    public override void InitializeConstraint()
    {
        var cells = ModelBuilderObject.Cells;
        int cellCount = cells.Count;
        initialVolumes = new float[cellCount];

        for (int i = 0; i < cellCount; i++)
        {
            Cell cell = cells[i];
            initialVolumes[i] = cell.CalcVolume();
        }
    }

    // (Removed local Determinant3 helper - using Utility.Determinant3x3 instead)

    public override (float[] constraints, float[,,] jacobians, float[,,] jacobianDerivative)
    CalculateConstraints()
    {
        if (initialVolumes == null)
        {
            Debug.LogError("ConstantVolume: initial volumes not initialized.");
            return (null, null, null);
        }

        // Current positions and velocities (global)
        Vector3[] pos = ModelBuilderObject.GetPositions();
        Vector3[] vel = ModelBuilderObject.Velocities ?? new Vector3[pos.Length];

        var cells = ModelBuilderObject.Cells;
        int cellCount = cells.Count;
        int n = pos.Length; // total vertices

        float[] constraints = new float[cellCount];
        // 3 is dimensions
        float[,,] jacobians = new float[cellCount, n, 3];
        float[,,] djac_dts = new float[cellCount, n, 3];

        for (int i = 0; i < cellCount; i++)
        {
            Cell cell = cells[i];

            // Determine apex global index and apex state
            int apexIdx = -1;
            Vector3 apexPos = Vector3.zero;
            Vector3 apexVel = Vector3.zero;
            if (cell.Vertices != null && cell.Vertices.Count > 0)
            {
                var apexGO = cell.Vertices[0];
                apexIdx = ModelBuilderObject.Vertices.IndexOf(apexGO);
                if (apexIdx >= 0 && apexIdx < n)
                {
                    apexPos = pos[apexIdx];
                    apexVel = vel.Length > apexIdx ? vel[apexIdx] : Vector3.zero;
                }
            }

            float sumVol = 0f;

            // accumulate cofactors per vertex locally, then write into jacobians
            Vector3[] localJac = new Vector3[n]; // sparse usage: only some indices used
            Vector3[] localDJac = new Vector3[n];

            // Iterate triangles via cell.Triangles to get GameObject -> global indices
            var tris = cell.Triangles; // List<Tuple<GameObject, GameObject, GameObject>>
            for (int t = 0; t < tris.Count; t++)
            {
                var tri = tris[t];
                if (tri == null) continue;
                var g0 = tri.Item1;
                var g1 = tri.Item2;
                var g2 = tri.Item3;

                int i0 = ModelBuilderObject.Vertices.IndexOf(g0);
                int i1 = ModelBuilderObject.Vertices.IndexOf(g1);
                int i2 = ModelBuilderObject.Vertices.IndexOf(g2);

                // validate triangle indices
                if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= n || i1 >= n || i2 >= n)
                {
                    Debug.LogWarning($"ConstantVolume.CalculateConstraints: skipping invalid triangle indices in cell {i}, triangle {t}.");
                    continue;
                }

                Vector3 r0 = pos[i0] - apexPos;
                Vector3 r1 = pos[i1] - apexPos;
                Vector3 r2 = pos[i2] - apexPos;

                Vector3 v0 = vel[i0] - apexVel;
                Vector3 v1 = vel[i1] - apexVel;
                Vector3 v2 = vel[i2] - apexVel;

                float tetVol = Utility.Determinant3x3(r0, r1, r2);
                sumVol += tetVol;

                // Cofactors: cross products
                Vector3 co0 = Vector3.Cross(r1, r2);
                Vector3 co1 = Vector3.Cross(r2, r0);
                Vector3 co2 = Vector3.Cross(r0, r1);

                // Time derivatives of cofactors
                Vector3 dco0 = Vector3.Cross(v1, r2) + Vector3.Cross(r1, v2);
                Vector3 dco1 = Vector3.Cross(v2, r0) + Vector3.Cross(r2, v0);
                Vector3 dco2 = Vector3.Cross(v0, r1) + Vector3.Cross(r0, v1);

                // Accumulate at triangle vertex indices
                localJac[i0] += co0;
                localJac[i1] += co1;
                localJac[i2] += co2;

                localDJac[i0] += dco0;
                localDJac[i1] += dco1;
                localDJac[i2] += dco2;
            }

            // Constraint value
            constraints[i] = sumVol - initialVolumes[i];

            // Apex contribution is negative sum of cofactors over triangle vertices
            Vector3 sumCof = Vector3.zero;
            Vector3 sumDCof = Vector3.zero;
            for (int vi = 0; vi < n; vi++)
            {
                if (localJac[vi] != Vector3.zero)
                {
                    sumCof += localJac[vi];
                }
                if (localDJac[vi] != Vector3.zero)
                {
                    sumDCof += localDJac[vi];
                }
            }

            if (apexIdx >= 0)
            {
                localJac[apexIdx] = -sumCof;
                localDJac[apexIdx] = -sumDCof;
            }

            // Write localJac into jacobians[c, :, :]
            for (int vi = 0; vi < n; vi++)
            {
                if (localJac[vi] != Vector3.zero)
                {
                    jacobians[i, vi, 0] = localJac[vi].x;
                    jacobians[i, vi, 1] = localJac[vi].y;
                    jacobians[i, vi, 2] = localJac[vi].z;
                }
                if (localDJac[vi] != Vector3.zero)
                {
                    djac_dts[i, vi, 0] = localDJac[vi].x;
                    djac_dts[i, vi, 1] = localDJac[vi].y;
                    djac_dts[i, vi, 2] = localDJac[vi].z;
                }
            }
        }

        return (constraints, jacobians, djac_dts);
    }
}
