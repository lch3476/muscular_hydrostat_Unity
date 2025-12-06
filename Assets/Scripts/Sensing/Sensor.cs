using System;
using UnityEngine;

public abstract class Sensor : MonoBehaviour
{
    [SerializeField] string sensorType;
    public string SensorType { get { return sensorType; } }


    // Sense something about the environment using the position of sesnors on the
    // structure.
    // Args:
    //     structure: the structure on which the sensors are housed
    //     state: the current state of the structure
    //     environment: the environment that the structure is in

    // Returns:
    //     a SensorData object which has the type of data and the data itself

    // Raises:
    //     NotImplemnetedError: if not implemneted by concrete class
    public abstract float[,,] Sense(float[] state, Environment environment);
}
