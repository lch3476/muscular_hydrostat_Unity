using UnityEngine;
using Unity.VisualScripting;
using System;
using System.Collections.Generic;

public class ArmBuilder : ModelBuilder
{
    void Start()
    {
        Initialize();
    }

    public override void CreateModel()
    {
        if (cellPrefab != null)
        {
            for (int i = 0; i < cellCount; ++i)
            {
                Cell cell = Instantiate(cellPrefab, this.transform.position, Quaternion.identity);
                if (cell != null)
                {
                    cell.name = "cell" + i;
                    cell.transform.position = cell.transform.position + new Vector3(0.0f, i * 0.5f, 0.0f);
                    cell.transform.parent = this.transform;
                }
                cells.Add(cell);
            }
        }

    }

    // Initialization methods
    protected override void InitVertices()
    {
        if (cells[0] != null)
        {
            vertices.Add(cells[0].Sphere1);
            vertices.Add(cells[0].Sphere2);
            vertices.Add(cells[0].Sphere3);
            vertices.Add(cells[0].Sphere4);
        }
        for (int i = 0; i < cells.Count; ++i)
        {
            if (cells[i] != null)
            {
                vertices.Add(cells[i].Sphere5);
                vertices.Add(cells[i].Sphere6);
                vertices.Add(cells[i].Sphere7);
                vertices.Add(cells[i].Sphere8);
            }
        }
    }

    protected override void InitVelocities()
    {
        velocities = Utility.CreateInitializedArray(vertices.Count, new Vector3(0.0f, 0.0f, 0.0f));
    }

    protected override void InitEdges()
    {
        if (cells[0] != null)
        {
            edges.Add(cells[0].Edges[0]);
            edges.Add(cells[0].Edges[1]);
            edges.Add(cells[0].Edges[2]);
            edges.Add(cells[0].Edges[3]);
        }
        for (int i = 0; i < cells.Count; ++i)
        {
            if (cells[i] != null)
            {
                for (int j = 4; j < cells[i].Edges.Count; ++j)
                {
                    edges.Add(cells[i].Edges[j]);
                }
            }
        }
    }

    protected override void InitFaces()
    {
        if (cells[0] != null)
        {
            faces.Add(cells[0].Faces[0]);
        }
        for (int i = 0; i < cells.Count; ++i)
        {
            if (cells[i] != null)
            {
                for (int j = 1; j < cells[i].Faces.Count; ++j)
                {
                    faces.Add(cells[i].Faces[j]);
                }
            }
        }
    }

    protected override void InitMasses()
    {
        masses = new List<float>(vertices.Count);
        if (cells[0] != null)
        {
            masses.Add(cells[0].Masses[0]);
            masses.Add(cells[0].Masses[1]);
            masses.Add(cells[0].Masses[2]);
            masses.Add(cells[0].Masses[3]);
        }
        for (int i = 0; i < cells.Count; ++i)
        {
            if (cells[i] != null)
            {
                masses.Add(cells[i].Masses[4]);
                masses.Add(cells[i].Masses[5]);
                masses.Add(cells[i].Masses[6]);
                masses.Add(cells[i].Masses[7]);
            }
        }
    }

    protected override void InitVertexDamping()
    {
        vertexDamping = new List<float>(vertices.Count);
        if (cells[0] != null)
        {
            vertexDamping.Add(cells[0].VertexDamping[0]);
            vertexDamping.Add(cells[0].VertexDamping[1]);
            vertexDamping.Add(cells[0].VertexDamping[2]);
            vertexDamping.Add(cells[0].VertexDamping[3]);
        }
        for (int i = 0; i < cells.Count; ++i)
        {
            if (cells[i] != null)
            {
                vertexDamping.Add(cells[i].VertexDamping[4]);
                vertexDamping.Add(cells[i].VertexDamping[5]);
                vertexDamping.Add(cells[i].VertexDamping[6]);
                vertexDamping.Add(cells[i].VertexDamping[7]);
            }
        }
    }

    protected override void InitEdgeDamping()
    {
        edgeDamping = new List<float>(Edges.Count);
        if (cells[0] != null)
        {
            edgeDamping.Add(cells[0].EdgeDamping[0]);
            edgeDamping.Add(cells[0].EdgeDamping[1]);
            edgeDamping.Add(cells[0].EdgeDamping[2]);
            edgeDamping.Add(cells[0].EdgeDamping[3]);
        }
        for (int i = 0; i < cells.Count; ++i)
        {
            if (cells[i] != null)
            {
                for (int j = 4; j < cells[i].Edges.Count; ++j)
                {
                    edgeDamping.Add(cells[i].EdgeDamping[j]);
                }
            }
        }
    }
}
