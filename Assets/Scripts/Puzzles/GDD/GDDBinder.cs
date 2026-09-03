using UnityEngine;

/// <summary>
/// Place this on a pickup prefab at the maze start cell (0,0). Picking it up
/// unlocks the GDD book UI (see UI_script.ToggleBook) and kicks off the
/// "collect missing pages" objective via GDDManager.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GDDBinder : MonoBehaviour
{
    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (GDDManager.Instance == null)
        {
            Debug.LogError("GDDBinder: no GDDManager found in the scene.");
            return;
        }

        GDDManager.Instance.CollectBinder();
        Destroy(gameObject);
    }
}