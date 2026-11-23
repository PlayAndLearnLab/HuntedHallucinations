using UnityEngine;

/// <summary>
/// Keeps spawned puzzle assets from sinking into the floor. Prefabs have different pivots
/// (centre, base, somewhere in between), so a fixed spawn height buries some of them.
/// This only ever LIFTS an object: if its lowest mesh point is below the floor, it is raised
/// until it rests on it. Assets that already sit above the floor are left exactly where they are.
/// </summary>
public static class GroundSnap
{
    public static void LiftAboveFloor(GameObject obj, float floorY, float clearance = 0.02f)
    {
        if (obj == null) return;

        bool found = false;
        float lowest = float.MaxValue;

        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
        {
            // Only real geometry: particles, trails and the like have unreliable bounds.
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            lowest = Mathf.Min(lowest, r.bounds.min.y);
            found = true;
        }

        if (!found) return;

        float target = floorY + clearance;
        if (lowest < target)
            obj.transform.position += Vector3.up * (target - lowest);
    }
}
