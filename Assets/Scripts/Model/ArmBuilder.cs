using UnityEngine;
using Unity.VisualScripting;
using System;
using System.Collections.Generic;

public class ArmBuilder : ModelBuilder
{

    protected override void CreateModel()
    {
        if (CellPrefab != null)
        {
            for (int i = 0; i < CellCount; ++i)
            {
                Cell cell = Instantiate(CellPrefab, this.transform.position, Quaternion.identity);
                if (cell != null)
                {
                    cell.name = "cell" + i;
                    cell.transform.position = cell.transform.position + new Vector3(0.0f, i * 0.5f, 0.0f);
                    cell.transform.parent = this.transform;
                    Cells.Add(cell);
                }
            }
            
            // Link adjacent cells to share vertices
            // Cell[i+1]'s top (Sphere1-4) = Cell[i]'s bottom (Sphere5-8)
            for (int i = 0; i < Cells.Count - 1; ++i)
            {
                if (Cells[i] != null && Cells[i + 1] != null)
                {
                    // Use SetTopVertices first to update references
                    Cells[i + 1].SetTopVertices(
                        Cells[i].Sphere5,
                        Cells[i].Sphere6,
                        Cells[i].Sphere7,
                        Cells[i].Sphere8
                    );
                    // Then destroy the redundant sphere GameObjects that are no longer referenced
                    // Note: These spheres are now orphaned since SetTopVertices updated the references
                    Transform sphere1Transform = Cells[i + 1].transform.Find("Sphere1");
                    Transform sphere2Transform = Cells[i + 1].transform.Find("Sphere2");
                    Transform sphere3Transform = Cells[i + 1].transform.Find("Sphere3");
                    Transform sphere4Transform = Cells[i + 1].transform.Find("Sphere4");
                    if (sphere1Transform != null) GameObject.DestroyImmediate(sphere1Transform.gameObject);
                    if (sphere2Transform != null) GameObject.DestroyImmediate(sphere2Transform.gameObject);
                    if (sphere3Transform != null) GameObject.DestroyImmediate(sphere3Transform.gameObject);
                    if (sphere4Transform != null) GameObject.DestroyImmediate(sphere4Transform.gameObject);
                }
            }
        }

    }

    // Initialization methods
    protected override void InitVertices()
    {
        if (Cells[0] != null)
        {
            Vertices.Add(Cells[0].Sphere1);
            Vertices.Add(Cells[0].Sphere2);
            Vertices.Add(Cells[0].Sphere3);
            Vertices.Add(Cells[0].Sphere4);
        }
        for (int i = 0; i < Cells.Count; ++i)
        {
            if (Cells[i] != null)
            {
                Vertices.Add(Cells[i].Sphere5);
                Vertices.Add(Cells[i].Sphere6);
                Vertices.Add(Cells[i].Sphere7);
                Vertices.Add(Cells[i].Sphere8);
            }
        }
    }

    protected override void InitVelocities()
    {
        Velocities = Utility.CreateInitializedArray(Vertices.Count, new Vector3(0.0f, 0.0f, 0.0f));
    }

    protected override void InitEdges()
    {
        if (Cells[0] != null)
        {
            Edges.Add(Cells[0].Edges[0]);
            Edges.Add(Cells[0].Edges[1]);
            Edges.Add(Cells[0].Edges[2]);
            Edges.Add(Cells[0].Edges[3]);
        }
        for (int i = 0; i < Cells.Count; ++i)
        {
            if (Cells[i] != null)
            {
                for (int j = 4; j < Cells[i].Edges.Count; ++j)
                {
                    Edges.Add(Cells[i].Edges[j]);
                }
            }
        }
    }

    protected override void InitFaces()
    {
        if (Cells[0] != null)
        {
            Faces.Add(Cells[0].Faces[0]);
        }
        for (int i = 0; i < Cells.Count; ++i)
        {
            if (Cells[i] != null)
            {
                for (int j = 1; j < Cells[i].Faces.Count; ++j)
                {
                    Faces.Add(Cells[i].Faces[j]);
                }
            }
        }
    }

    protected override void InitMasses()
    {
        Masses = new List<float>(Vertices.Count);
        if (Cells.Count > 0 && Cells[0] != null && Cells[0].Masses != null && Cells[0].Masses.Length >= 4)
        {
            Masses.Add(Cells[0].Masses[0]);
            Masses.Add(Cells[0].Masses[1]);
            Masses.Add(Cells[0].Masses[2]);
            Masses.Add(Cells[0].Masses[3]);
        }
        for (int i = 0; i < Cells.Count; ++i)
        {
            if (Cells[i] != null && Cells[i].Masses != null && Cells[i].Masses.Length >= 8)
            {
                Masses.Add(Cells[i].Masses[4]);
                Masses.Add(Cells[i].Masses[5]);
                Masses.Add(Cells[i].Masses[6]);
                Masses.Add(Cells[i].Masses[7]);
            }
        }
    }

    protected override void InitVertexDamping()
    {
        VertexDamping = new List<float>(Vertices.Count);
        if (Cells[0] != null)
        {
            VertexDamping.Add(Cells[0].VertexDamping[0]);
            VertexDamping.Add(Cells[0].VertexDamping[1]);
            VertexDamping.Add(Cells[0].VertexDamping[2]);
            VertexDamping.Add(Cells[0].VertexDamping[3]);
        }
        for (int i = 0; i < Cells.Count; ++i)
        {
            if (Cells[i] != null)
            {
                VertexDamping.Add(Cells[i].VertexDamping[4]);
                VertexDamping.Add(Cells[i].VertexDamping[5]);
                VertexDamping.Add(Cells[i].VertexDamping[6]);
                VertexDamping.Add(Cells[i].VertexDamping[7]);
            }
        }
    }

    protected override void InitEdgeDamping()
    {
        EdgeDamping = new List<float>(Edges.Count);
        if (Cells[0] != null)
        {
            EdgeDamping.Add(Cells[0].EdgeDamping[0]);
            EdgeDamping.Add(Cells[0].EdgeDamping[1]);
            EdgeDamping.Add(Cells[0].EdgeDamping[2]);
            EdgeDamping.Add(Cells[0].EdgeDamping[3]);
        }
        for (int i = 0; i < Cells.Count; ++i)
        {
            if (Cells[i] != null)
            {
                for (int j = 4; j < Cells[i].Edges.Count; ++j)
                {
                    EdgeDamping.Add(Cells[i].EdgeDamping[j]);
                }
            }
        }
    }
}
