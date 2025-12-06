using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;

public abstract class Arm : ConstrainedDynamic
{
    // Not sure if states is necessary
    // states is the array that contains vertically stacked positions and velocities
    // List<Vector3> states;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InitNumParticles();
        InitNumControls();
        InitInvMasses();
    }

    // Update is called once per frame
    void Update()
    {

    }

    // Factory methods implementation
    public override void InitNumParticles()
    {
        numParticles = ModelBuilder.Vertices.Count;
    }

    public override void InitNumControls()
    {
        numControls = ModelBuilder.Edges.Count;
    }

    public override void InitExternalForces()
    {
        externalForces = new Vector3[ModelBuilder.Vertices.Count];
    }

    public override void InitInvMasses()
    {
        int length = ModelBuilder.Masses.Count;
        invMasses = new float[length * 3];
        for (int i = 0; i < length; i++)
        {
            float invMass = 1 / ModelBuilder.Masses[i];
            for (int j = 0; j < 3; j++)
            {
                invMasses[i * 3 + j] = invMass;
            }
        }
    }
    
    public override void InitNumStates()
    {
        numStates = ModelBuilder.Vertices.Count * 6;
    }
}
