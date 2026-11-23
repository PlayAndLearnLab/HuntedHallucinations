using UnityEngine;
using TMPro;

public class TimerManager : MonoBehaviour
{
    public static TimerManager Instance { get; private set; }

    [SerializeField] private float _totalTime = 120f;
    [SerializeField] private TextMeshProUGUI _timerText;

    [Header("Game Over")]
    [Tooltip("Must match the scene name in File > Build Settings exactly (case-sensitive).")]
    [SerializeField] private string _gameOverSceneName = "GameOver";
    [SerializeField] private Color _gameOverFadeColor = new Color(0.25f, 0f, 0f, 1f); // dark blood red
    [SerializeField] private float _fadeInTime = 1.0f;
    [SerializeField] private float _holdTime = 0.3f;
    [SerializeField] private float _fadeOutTime = 1.2f;

    private float _timeRemaining;
    private bool _isRunning = false;
    private bool _hasStarted = false;

    public float TimeRemaining => _timeRemaining;
    public TextMeshProUGUI TimerText => _timerText; // used by AuditFeedback to place the floating +/- seconds
    public float TotalTime => _totalTime;
    public bool IsRunning => _isRunning;
    /// <summary>True once StartTimer has been called. Before that TimeRemaining is 0 but time is NOT up.</summary>
    public bool HasStarted => _hasStarted;

    void Start()
    {
        float[] distances = new float[32];
        distances[8] = 50f;  // Layer 8 objects cull at 50 units
        distances[9] = 100f; // Layer 9 objects cull at 100 units
        GetComponent<Camera>().layerCullDistances = distances;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartTimer()
    {
        _timeRemaining = _totalTime;
        _isRunning = true;
        _hasStarted = true;
        RunResult.Begin(_totalTime); // end-screen timing + leaderboard
    }

    void Update()
    {
        if (!_isRunning) return;

        _timeRemaining -= Time.deltaTime;
        UpdateDisplay();

        if (_timeRemaining <= 0)
        {
            _timeRemaining = 0;
            _isRunning = false;
            GameLost();
        }
    }

    private void UpdateDisplay()
    {
        int minutes = Mathf.FloorToInt(_timeRemaining / 60);
        int seconds = Mathf.FloorToInt(_timeRemaining % 60);
        if (_timerText != null)
            _timerText.text = $"{minutes:00}:{seconds:00}";
    }

    public void Stop() => _isRunning = false;

    /// <summary>
    /// Positive = bonus, negative = penalty. Never pushes the clock past TotalTime, and a penalty
    /// can't take it below 1 second, so a mistaken flag can't end the run by itself.
    /// </summary>
    public void AdjustTime(float deltaSeconds)
    {
        if (!_isRunning) return;
        float floor = Mathf.Min(_timeRemaining, 1f);
        float ceiling = Mathf.Max(_totalTime, _timeRemaining);
        float before = _timeRemaining;
        _timeRemaining = Mathf.Clamp(_timeRemaining + deltaSeconds, floor, ceiling);
        RunResult.AddAdjust(_timeRemaining - before); // only what really reached the clock counts toward the official time
        UpdateDisplay();
    }

    private void GameLost()
    {
        Debug.Log("Time is up — game over");
        UpdateDisplay(); // make sure the HUD shows 00:00
        RunResult.Finish(false); // records the attempt and grabs the last frame for the end screen

        // Freeze the player so they can't keep walking during the fade.
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) pc.ToggleMovement(false);

        // Free the cursor so the Game Over scene's buttons are clickable.
        PlayerCamera.SetCursorFree(true);

        if (!Application.CanStreamedLevelBeLoaded(_gameOverSceneName))
        {
            Debug.LogError($"TimerManager: scene '{_gameOverSceneName}' is not in Build Settings (or the name doesn't match exactly).");
            return;
        }

        // Same transition as the win ending, but fading to dark red instead of white.
        WhiteFlashTransition.Play(_gameOverSceneName, _gameOverFadeColor, _fadeInTime, _holdTime, _fadeOutTime);
    }
}

// using UnityEngine;
// using TMPro;

// public class TimerManager : MonoBehaviour
// {
//     public static TimerManager Instance { get; private set; }

//     [SerializeField] private float _totalTime = 120f;
//     [SerializeField] private TextMeshProUGUI _timerText;

//     private float _timeRemaining;
//     private bool _isRunning = false;

//     public float TimeRemaining => _timeRemaining;

//     void Start()
//     {
//         float[] distances = new float[32];
//         distances[8] = 50f;  // Layer 8 objects cull at 50 units
//         distances[9] = 100f; // Layer 9 objects cull at 100 units
//         GetComponent<Camera>().layerCullDistances = distances;
//     }

//     void Awake()
//     {
//         if (Instance != null && Instance != this) { Destroy(gameObject); return; }
//         Instance = this;
//     }

//     public void StartTimer()
//     {
//         _timeRemaining = _totalTime;
//         _isRunning = true;
//     }

//     void Update()
//     {
//         if (!_isRunning) return;

//         _timeRemaining -= Time.deltaTime;
//         UpdateDisplay();

//         if (_timeRemaining <= 0)
//         {
//             _timeRemaining = 0;
//             _isRunning = false;
//             GameLost();
//         }
//     }

//     private void UpdateDisplay()
//     {
//         int minutes = Mathf.FloorToInt(_timeRemaining / 60);
//         int seconds = Mathf.FloorToInt(_timeRemaining % 60);
//         if (_timerText != null)
//             _timerText.text = $"{minutes:00}:{seconds:00}";
//     }

//     public void Stop() => _isRunning = false;

//     private void GameLost()
//     {
//         Debug.Log("Time is up — game over");
//         // Hook your lose screen here
//     }
// }