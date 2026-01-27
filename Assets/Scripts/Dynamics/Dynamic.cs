using UnityEngine;
using System.Collections.Generic;

// Abstract class that is equivalent to DynamicModel of dynamics.py

[DefaultExecutionOrder(-50)]
public abstract class Dynamic : MonoBehaviour
{
    [SerializeField] ModelBuilder modelBuilder;
    public ModelBuilder ModelBuilder { get { return modelBuilder; } }

    private int numStates;
    public int NumStates { get { return numStates; } set { numStates = value; } }
    private int numControls;
    public int NumControls { get { return numControls; } set { numControls = value; } }

    protected void Awake()
    {
        InitNumControls();
        InitNumStates();
    }

    public abstract float[] ContinuousDynamics(float[] state, float[] control, float t);

    // Integrate the continuous dynamics using Euler's method.
    public virtual float[] IntegratorEuler(float[] state, float[] control, float t, float dt)
    {
        float[] derivative = ContinuousDynamics(state, control, t);
        float[] nextState = new float[state.Length];
        for (int i = 0; i < state.Length; i++)
        {
            nextState[i] = state[i] + dt * derivative[i];
        }
        return nextState;
    }

    public virtual float[] DiscreteDynamics(float[] state, float[] control, float t, float dt)
    {
        return IntegratorEuler(state, control, t, dt);
    }

    public virtual float[] IntegratorRK(float[] state, float[] control, float t, float dt)
    {
        int n = state.Length;
        float[] k1 = ContinuousDynamics(state, control, t);
        float[] tempState = new float[n];
        float[] k2;
        float[] k3;
        float[] k4;

        // k2
        for (int i = 0; i < n; i++) tempState[i] = state[i] + 0.5f * k1[i] * dt;
        k2 = ContinuousDynamics(tempState, control, t);

        // k3
        for (int i = 0; i < n; i++) tempState[i] = state[i] + 0.5f * k2[i] * dt;
        k3 = ContinuousDynamics(tempState, control, t);

        // k4
        for (int i = 0; i < n; i++) tempState[i] = state[i] + k3[i] * dt;
        k4 = ContinuousDynamics(tempState, control, t);

        float[] nextState = new float[n];
        for (int i = 0; i < n; i++)
        {
            nextState[i] = state[i] + (dt / 6f) * (k1[i] + 2f * k2[i] + 2f * k3[i] + k4[i]);
        }
        return nextState;
    }

    // Simulate the system under some policy
    // Args:
    //     init_state: the initial state
    //     policy: a function that takes in a state and time
    //     final_step: the number of total simulated time steps (including initial)
    //     dt: the time interval between samples
    public (float[,] stateTraj, float[,] controlTraj) Simulate(
        float[] initState,
        Policy policy,
        int finalStep,
        float dt)
    {
        int stateDim = initState.Length;
        int controlDim = this.NumControls;

        var stateTraj = new float[finalStep, stateDim];
        var controlTraj = new float[finalStep, controlDim];

        // set initial state (row 0)
        for (int j = 0; j < stateDim; j++) stateTraj[0, j] = initState[j];

        for (int i = 1; i < finalStep; i++)
        {
            float t = i * dt;

            // extract current state (row i-1)
            float[] currentState = new float[stateDim];
            for (int j = 0; j < stateDim; j++) currentState[j] = stateTraj[i - 1, j];

            // Policy expects List<float>, so convert
            float[] currentStateList = currentState;
            float[] controlList = policy.CalculateControlInputs(currentStateList, t);

            // convert control to float[] and store in controlTraj at row i-1
            float[] control = new float[controlDim];
            for (int j = 0; j < controlDim && j < controlList.Length; j++)
            {
                control[j] = controlList[j];
                controlTraj[i - 1, j] = controlList[j];
            }

            // compute next state
            float[] nextState = DiscreteDynamics(currentState, control, t, dt);

            // store next state in row i
            for (int j = 0; j < stateDim && j < nextState.Length; j++)
            {
                stateTraj[i, j] = nextState[j];
            }
        }

        return (stateTraj, controlTraj);
    }

    // When an actor is placed in an environment, it may need to initialize certain
    // things, such as constraints.
    // public abstract void SetEnvironment(Environment environment);

    // Factory methods
    protected abstract void InitNumControls();
    protected abstract void InitNumStates();
}
