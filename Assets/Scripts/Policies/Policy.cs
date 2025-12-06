using UnityEngine;
using System.Collections.Generic;

// Abstract class that is equivalent to IPolicy of policy_interface.py

// TODO: I think typically observations would not be passed in, but instead
// state estimation would be used to get the estimated state from observations.
// For the hydrostat, this would mean that the state vector should really be all
// of the positions, velocities, and smell concentrations.

public abstract class Policy : MonoBehaviour
{
    // Must be the same shape as the structure actuators.
    // Returns: an np.ndarray of control inputs which have the same shape as the actuators of the structure.
    // Temporarily deprecated
    // public abstract List<float> CalculateControlInputs(
    //     ArmBuilder arm,
    //     List<float> states,
    //     List<float> observations,
    //     float t);

    // Should be replaced with the version with arm and observations arguments
    public abstract List<float> TempCalculateControlInputs(
        List<float> states,
        float t);
}
