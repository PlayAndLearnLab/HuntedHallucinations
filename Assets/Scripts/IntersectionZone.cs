using UnityEngine;
using System.Collections.Generic;

public class IntersectionZone : MonoBehaviour
{
    private List<WrongPathTrigger> _wrongPathTriggers = new List<WrongPathTrigger>();
    private VisualPuzzleSpawner _visualSpawner;
    private TextPuzzleSpawner   _textSpawner;
    private bool _revealed = false;

    public void RegisterWrongPathTrigger(WrongPathTrigger trigger)
    {
        _wrongPathTriggers.Add(trigger);
    }

    // Called by IntersectionDetector after spawning the puzzle
    public void RegisterPuzzleSpawner(GameObject spawnerObj)
    {
        _visualSpawner = spawnerObj.GetComponent<VisualPuzzleSpawner>();
        _textSpawner   = spawnerObj.GetComponent<TextPuzzleSpawner>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Arm wrong path triggers
        foreach (var trigger in _wrongPathTriggers)
            trigger.Arm();

        // Reveal puzzle the first time the player enters
        if (!_revealed)
        {
            _revealed = true;
            _visualSpawner?.Reveal();
            _textSpawner?.Reveal();
        }
    }
}
