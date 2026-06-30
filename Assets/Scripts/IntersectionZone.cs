using UnityEngine;
using System.Collections.Generic;

public class IntersectionZone : MonoBehaviour
{
    private List<WrongPathTrigger> _wrongPathTriggers = new List<WrongPathTrigger>();

    public void RegisterWrongPathTrigger(WrongPathTrigger trigger)
    {
        _wrongPathTriggers.Add(trigger);
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"IntersectionZone hit by: {other.name} tag: {other.tag}");
        if (!other.CompareTag("Player")) return;

        Debug.Log("Player entered intersection zone — arming wrong path triggers");
        // Player reached the intersection — arm all wrong path triggers ahead
        foreach (var trigger in _wrongPathTriggers)
            trigger.Arm();
    }

    // void OnTriggerExit(Collider other)
    // {
    //     if (!other.CompareTag("Player")) return;

    //     // Player left the intersection zone — disarm in case they come back to re-choose
    //     Debug.Log("Player left intersection zone — disarming wrong path triggers");
    //     foreach (var trigger in _wrongPathTriggers)
    //         trigger.Disarm();
    // }
}