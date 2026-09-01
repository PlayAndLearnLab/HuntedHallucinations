using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitPortal : MonoBehaviour
{
    [Header("Scene Configuration")]
    [SerializeField] private string _endingSceneName = "CongratsScene"; // Match exact scene name in Build Settings

    private bool _isTransitioning = false;

    private void OnTriggerEnter(Collider other)
    {
        // Prevent double triggers if the player stays inside the collider
        if (_isTransitioning) return;

        if (other.CompareTag("Player"))
        {
            _isTransitioning = true;
            LoadEndingScene();
        }
    }

    private void LoadEndingScene()
    {
        // Stop timer or clean up maze data if needed before switching
        if (TimerManager.Instance != null)
        {
            TimerManager.Instance.Stop();
        }

        SceneManager.LoadScene(_endingSceneName);
    }
}

