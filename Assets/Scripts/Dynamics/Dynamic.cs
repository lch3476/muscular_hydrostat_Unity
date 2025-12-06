using UnityEngine;
using System.Collections.Generic;

// Abstract class that is equivalent to DynamicModel of dynamics.py

public abstract class Dynamic : MonoBehaviour
{
    protected int numStates;
    protected int numControls;
    public int NumControls { get { return numControls; } }

    public abstract List<float> ContinuousDynamics(List<float> state, List<float> control, float t);

    // Integrate the continuous dynamics using Euler's method.
    public virtual List<float> IntegratorEuler(List<float> state, List<float> control, float t, float dt)
    {
        List<float> derivative = ContinuousDynamics(state, control, t);
        List<float> nextState = new List<float>(state.Count);
        for (int i = 0; i < state.Count; i++)
        {
            nextState.Add(state[i] + dt * derivative[i]);
        }
        return nextState;
    }

    public virtual List<float> DiscreteDynamics(List<float> state, List<float> control, float t, float dt)
    {
        return IntegratorEuler(state, control, t, dt);
    }

    public virtual List<float> IntegratorRK(List<float> state, List<float> control, float t, float dt)
    {
        List<float> k1 = ContinuousDynamics(state, control, t);
        List<float> tempState = new List<float>(state.Count);
        List<float> k2 = new List<float>(state.Count);
        List<float> k3 = new List<float>(state.Count);
        List<float> k4 = new List<float>(state.Count);

        for (int i = 0; i < state.Count; i++)
        {
            tempState.Add(state[i] + 0.5f * k1[i] * dt);
        }
        k2 = ContinuousDynamics(tempState, control, t);

        tempState.Clear();
        for (int i = 0; i < state.Count; i++)
        {
            tempState.Add(state[i] + 0.5f * k2[i] * dt);
        }
        k3 = ContinuousDynamics(tempState, control, t);

        tempState.Clear();
        for (int i = 0; i < state.Count; i++)
        {
            tempState.Add(state[i] + k3[i] * dt);
        }
        k4 = ContinuousDynamics(tempState, control, t);

        List<float> nextState = new List<float>(state.Count);
        for (int i = 0; i < state.Count; i++)
        {
            nextState.Add(state[i] + (dt / 6f) * (k1[i] + 2f * k2[i] + 2f * k3[i] + k4[i]));
        }
        return nextState;
    }

    // Simulate the system under some policy
    // Args:
    //     init_state: the initial state
    //     policy: a function that takes in a state and time
    //     final_step: the number of total simulated time steps (including initial)
    //     dt: the time interval between samples
    public (List<List<float>> stateTraj, List<List<float>> controlTraj) Simulate(
        List<float> initState,
        Policy policy,
        int finalStep,
        float dt)
    {
        var stateTraj = new List<List<float>>(finalStep);
        var controlTraj = new List<List<float>>(finalStep);
        stateTraj.Add(initState);

        for (int i = 1; i < finalStep; i++)
        {
            float t = i * dt;
            var currentState = stateTraj[i - 1];
            var control = policy.TempCalculateControlInputs(currentState, t);
            var nextState = DiscreteDynamics(currentState, control, t, dt);
            controlTraj.Add(control);
            stateTraj.Add(nextState);
        }

        return (stateTraj, controlTraj);
    }

    // When an actor is placed in an environment, it may need to initialize certain
    // things, such as constraints.
    // public abstract void SetEnvironment(Environment environment);

    // Factory methods
    public abstract void InitNumControls();
    public abstract void InitNumStates();
}
