using UnityEngine;

public class WrongPathTrigger : MonoBehaviour
{
    private bool _fired = false;
    private bool _armed = false;

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"WrongPathTrigger hit by: {other.name} tag: {other.tag}, armed: {_armed}, fired: {_fired}");
        if (_fired || !_armed) return;
        if (!other.CompareTag("Player")) return;

        Debug.Log("Player hit wrong path trigger — committing penalty");
        _fired = true;
        PenaltyMazeManager.Instance.OnWrongPathCommitted();
    }

    public void Arm()
    {
        Debug.Log($"Arming WrongPathTrigger at {transform.position}, {gameObject.name} armed");
        _armed = true;
    }

    public void Disarm()
    {
        _armed = false;
    }
}