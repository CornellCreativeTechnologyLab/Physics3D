using System.Collections.Generic;
using UnityEngine;

// A field described purely in the container's LOCAL space.
public abstract class FieldModel : MonoBehaviour
{
    // Field vector at a local point: direction = field dir, length = strength.
    // Valid for ANY point, not just sample points (needed later for line tracing/probes).
    public abstract Vector3 SampleLocal(Vector3 localPoint);

    // Is this local point inside the field region?
    public abstract bool ContainsLocal(Vector3 localPoint);

    // Where to place visualization elements, in local space.
    public abstract IEnumerable<Vector3> SamplePoints();
}