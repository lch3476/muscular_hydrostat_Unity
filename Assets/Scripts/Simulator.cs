using System.Collections.Generic;
using UnityEngine;

// A class that corresponding to Actor class of actor.py

public class Simulator : MonoBehaviour
{

    [SerializeField] Dynamic model;
    [SerializeField] Policy policy;

    // TODO: implement Environment
    // [SerializeField] List<Sensor> sensors;
    private List<float> initialState;
    private List<float> state;
    private List<float> control;

    // TODO: implement Environment
    // [SerializeField] List<Obstacle> obstacles;
    private Dictionary<string, float[]> observations;

    // TODO: implement environment
    //[SerializeField] Environment environment;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    // Perform a single step of the actor's operation. Update the state and control.
    // Args:
    //     t: The current time.
    //     dt: The time step for the model evolution.

    // Returns:
    //     The next state of the model after applying the control policy.
    void Step(float t, float dt)
    {

        float lastTime = Time.time;
        // Sense();
        // Debug.Log("Sensing: " + (Time.time - lastTime));
        // lastTime = Time.time;

        // temporary input to 
        List<float> controlInputs = new List<float>(new float[model.NumControls]);
        // TODO: implement environment and sensor
        // control = CalculateControl(t);
        // Debug.Log("Control: " + (Time.time - lastTime));
        // lastTime = Time.time;

        state = model.DiscreteDynamics(state, controlInputs, t, dt);
        Debug.Log("Simulation: " + (Time.time - lastTime));
    }

    // Estimate the current state of the model.
    private List<float> EstimateState()
    {
        // TODO: add estimation process.
        return state;
    }

    // Take sensor measurements for all sensors and return a dictionary of data.
    // Returns:
    //     A dictionary of sensor data where each key is the sensor type.
    // void Sense()
    // {
    // Implement sensing logic here
    // }

    // Calculate the control input based on the current state and time.
    // Args:
    //     t: The current time.
    // Returns:
    //     The control input as a jnp.ndarray.
    List<float> CalculateControl(float t)
    {
        return policy.TempCalculateControlInputs(EstimateState(), t);
    }
}
