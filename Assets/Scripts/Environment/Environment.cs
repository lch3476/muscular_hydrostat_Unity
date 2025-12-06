using System.Collections.Generic;
using System.Xml;
using UnityEngine;

public class Environment : MonoBehaviour
{
    [SerializeField] int dimension = 3;
    [SerializeField] List<Obstacle> obstacles;
    [SerializeField] float spatialResolution = 0.2f;
    [SerializeField] float steadyStateError = 0.05f;
    [SerializeField] int maxIterations = 1000;
    [SerializeField] float[,] limits;
    float dt;
    float[] foodMagnitudes;
    float[,] concentration;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dt = Mathf.Pow(spatialResolution, 2) / (dimension * 2) * 0.99f;
    }

    // Update is called once per frame
    void Update()
    {

    }

}
