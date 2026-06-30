using UnityEngine;
using TMPro;

public class TimerManager : MonoBehaviour
{
    public static TimerManager Instance { get; private set; }

    [SerializeField] private float _totalTime = 120f;
    [SerializeField] private TextMeshProUGUI _timerText;

    private float _timeRemaining;
    private bool _isRunning = false;

    public float TimeRemaining => _timeRemaining;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartTimer()
    {
        _timeRemaining = _totalTime;
        _isRunning = true;
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

    private void GameLost()
    {
        Debug.Log("Time is up — game over");
        // Hook your lose screen here
    }
}