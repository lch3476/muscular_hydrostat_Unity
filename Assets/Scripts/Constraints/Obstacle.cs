using System.Collections.Generic;
using UnityEngine;

public abstract class Obstacle : Constraint
{
    // Check if a set of points intersect with the obstacle volume.
    // Args:
    //     points: an nxd array of points to check for intersections where n is the
    //         number of points and d is the dimension
    // Returns:
    //     a boolean array where True indices are intersecting the obstacle
   public abstract List<bool> CheckIntersection(List<Vector3> points);
}
