using UnityEngine;

/// <summary>
/// A single missing design-doc page. IntersectionDetector spawns one of these
/// on the floor exactly one cell before every intersection on the true
/// solution path, pre-wired (via Setup) to that intersection's PuzzleData —
/// so the player always holds the correct reference before they have to choose.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GDDPage : MonoBehaviour
{
    private PuzzleData _data;

    public void Setup(PuzzleData data)
    {
        _data = data;
    }

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (_data == null)
            Debug.LogWarning("GDDPage picked up with no assigned PuzzleData — was Setup() called before this object was reachable?");

        if (GDDManager.Instance != null)
        {
            GDDManager.Instance.CollectPage(_data);
        }
        else
        {
            Debug.LogError("GDDPage: no GDDManager found in the scene.");
            Destroy(gameObject);
        }
            

        
    }
}