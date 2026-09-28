using UnityEngine;

public class ExitPortal : MonoBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("Must match the scene name in File > Build Settings exactly (case-sensitive).")]
    [SerializeField] private string _endingSceneName = "CongratsScene";

    [Header("Flash Transition")]
    [SerializeField] private float _flashInTime = 1.2f;
    [SerializeField] private float _holdWhiteTime = 0.25f;
    [SerializeField] private float _flashOutTime = 1.5f;

    private bool _isTransitioning = false;

    // ---------- Setup diagnostics (read these in the Console on maze build) ----------
    private void Awake()
    {
        Collider col = GetComponentInChildren<Collider>();

        if (col == null)
            Debug.LogError("ExitPortal: no Collider found on this object or its children — it can never trigger.", this);
        else if (!col.isTrigger)
            Debug.LogWarning($"ExitPortal: collider '{col.name}' is not set to Is Trigger — OnTriggerEnter will not fire.", col);
        else if (col.gameObject != gameObject && GetComponent<Rigidbody>() == null)
            Debug.LogWarning($"ExitPortal: the collider lives on child '{col.name}' but this script is on '{name}'. " +
                             "OnTriggerEnter only fires on the object that owns the collider (or the Rigidbody). " +
                             "Move this script onto the collider's object.", this);

        Debug.Log($"ExitPortal spawned at {transform.position}, lossyScale {transform.lossyScale}", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"ExitPortal triggered by {other.name} (tag: {other.tag})");

        if (_isTransitioning) return;

        // Tag check, with a fallback in case the collider sits on an untagged child of the player.
        bool isPlayer = other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null;
        if (!isPlayer) return;

        // Fail loudly and early if the scene isn't in Build Settings.
        if (!Application.CanStreamedLevelBeLoaded(_endingSceneName))
        {
            Debug.LogError($"ExitPortal: scene '{_endingSceneName}' is not in Build Settings (or the name doesn't match exactly).");
            return;
        }

        _isTransitioning = true;
        BeginEnding(other);
    }

    private void BeginEnding(Collider player)
    {
        // Stop the timer — wrapped so an exception in TimerManager can never block the ending.
        try
        {
            if (TimerManager.Instance != null)
                TimerManager.Instance.Stop();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }

        // Freeze the player during the flash.
        PlayerController pc = player.GetComponentInParent<PlayerController>();
        if (pc != null) pc.ToggleMovement(false);

        // Free the cursor so the ending scene can have clickable buttons.
        PlayerCamera.SetCursorFree(true);

        WhiteFlashTransition.Play(_endingSceneName, _flashInTime, _holdWhiteTime, _flashOutTime);
    }
}


// using UnityEngine;
// using UnityEngine.SceneManagement;

// public class ExitPortal : MonoBehaviour
// {
//     [Header("Scene Configuration")]
//     [SerializeField] private string _endingSceneName = "CongratsScene"; // Match exact scene name in Build Settings

//     private bool _isTransitioning = false;

//     private void OnTriggerEnter(Collider other)
//     {
//         Debug.Log($"ExitPortal triggered by {other.name}");
//         // Prevent double triggers if the player stays inside the collider
//         if (_isTransitioning) return;

//         if (other.CompareTag("Player"))
//         {
//             _isTransitioning = true;
//             LoadEndingScene();
//         }
//     }

//     private void LoadEndingScene()
//     {
//         // Stop timer or clean up maze data if needed before switching
//         if (TimerManager.Instance != null)
//         {
//             TimerManager.Instance.Stop();
//         }

//         SceneManager.LoadScene(_endingSceneName);
//     }
// }

