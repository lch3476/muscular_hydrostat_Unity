using System.Collections.Generic;
using UnityEngine;

// A class that corresponding to Actor class of actor.py

public class Simulator : MonoBehaviour
{

    [SerializeField] Dynamic model;
    // [SerializeField] Policy policy;

    // TODO: implement Environment
    // [SerializeField] List<Sensor> sensors;
    private List<float> initialState;
    private float[] state;
    private float[] control;

    // TODO: implement Environment
    // [SerializeField] List<Obstacle> obstacles;
    private Dictionary<string, float[]> observations;

    // TODO: implement environment
    //[SerializeField] Environment environment;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InitState();
    }

    // Update is called once per frame
    void Update()
    {
        Step(Time.time, Time.deltaTime);
    }

    // Perform a single step of the actor's operation. Update the state and control.
    // Args:
    //     t: The current time.
    //     dt: The time step for the model evolution.

    // Returns:
    //     The next state of the model after applying the control policy.
    void Step(float t, float dt)
    {
        // Debug.Log("Simulation Step at time: " + t + " with dt: " + dt);
        float lastTime = Time.time;
        // Sense();
        // Debug.Log("Sensing: " + (Time.time - lastTime));
        // lastTime = Time.time;

        // temporary input to 
        //float[] controlInputs = new float[model.NumControls];
        Debug.Log("Model Num Controls: " + model.NumControls);
        float[] controlInputs = Utility.CreateInitializedArray(model.NumControls, 5f);
        // TODO: implement environment and sensor
        // control = CalculateControl(t);
        // Debug.Log("Control: " + (Time.time - lastTime));
        // lastTime = Time.time;

        state = model.DiscreteDynamics(state, controlInputs, t, dt);
        model.ModelBuilder.ParseState(state);
        // Debug.Log("Simulation: " + (Time.time - lastTime));
    }

    // Estimate the current state of the model.
    private float[] EstimateState()
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
    // float[] CalculateControl(float t)
    // {
    //     return policy.TempCalculateControlInputs(EstimateState(), t);
    // }

    private void InitState()
    {
        if (model == null)
        {
            Debug.LogError("Simulator.InitState: model is null");
            state = new float[0];
            return;
        }

        if (model.ModelBuilder == null)
        {
            Debug.LogError("Simulator.InitState: model.ModelBuilder is null");
            state = new float[0];
            return;
        }

        state = model.ModelBuilder.GetState();
    }
}
