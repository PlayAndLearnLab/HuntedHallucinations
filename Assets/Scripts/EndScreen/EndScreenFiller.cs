using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Fills YOUR end-screen texts (CongratsScene and GameOver) with the run's time, personal-best feedback and the
/// personal-bests list. It never changes any styling: fonts, sizes, colours and positions stay as you set them.
/// Every slot is optional. Audit stats + rank stamp are filled by AuditResultsPanel, not by this script.
///
/// Buttons: hook PlayAgain / ChangeDifficulty / MainMenu to your buttons' OnClick (keys R / D / Esc also work).
/// Clickable arrows around the difficulty switcher can call PreviousBoard / NextBoard (keys Q / E or arrows).
///
/// Pressing Play directly in an end scene (no run played) shows sample values, so you can check the layout.
/// Right-click the component > "Clear all personal bests" to wipe saved times.
/// </summary>
public class EndScreenFiller : MonoBehaviour
{
    public enum Outcome { Auto, Escaped, TimedOut }

    [Tooltip("Auto = taken from the run that just ended. Set Escaped in CongratsScene / TimedOut in GameOver to force it.")]
    [SerializeField] private Outcome _outcome = Outcome.Auto;

    [Header("This run")]
    [Tooltip("e.g. MazeDifficulty2. Shows Easy / Medium / Hard.")]
    [SerializeField] private TMP_Text _difficultyValue;
    [Tooltip("Optional, e.g. Text2. Lets the timed-out screen say 'Best on Easy:' instead of 'Final time:'.")]
    [SerializeField] private TMP_Text _timeLabel;
    [SerializeField] private string _winTimeLabel = "Final time:";
    [Tooltip("{0} = the difficulty.")]
    [SerializeField] private string _loseTimeLabel = "Best on {0}:";
    [Tooltip("e.g. Time. 03:15.40 on a win; the stored best (or --:--) when time ran out.")]
    [SerializeField] private TMP_Text _timeValue;
    [Tooltip("The line under the time: new best / behind your best / first escape / reach the exit.")]
    [SerializeField] private TMP_Text _personalBestLine;

    [Header("Personal bests")]
    [Tooltip("The '<  Easy  >' line under 'Personal Bests:'.")]
    [SerializeField] private TMP_Text _boardSwitcher;
    [SerializeField] private string _switcherFormat = "<   {0}   >";
    [Tooltip("One text for the whole list; columns are aligned with <pos> tags.")]
    [SerializeField] private TMP_Text _boardList;
    [Tooltip("Optional: 'Escapes: 5 / 9 attempts'.")]
    [SerializeField] private TMP_Text _attemptsLine;
    [Range(1, PersonalLeaderboard.MaxEntries)] [SerializeField] private int _rowsShown = 5;
    [Tooltip("Tabs, in order. The run's own difficulty is added if missing. Must match the names RunResult derives " +
             "from your MazeDifficultySettings assets (any name containing easy / medium / hard).")]
    [SerializeField] private string[] _difficultyTabs = { "Easy", "Medium", "Hard" };

    [Header("List columns (% of the list text's width)")]
    [SerializeField] private float _timeColumn = 12f;
    [SerializeField] private float _dateColumn = 52f;
    [SerializeField] private float _markerColumn = 78f;
    [SerializeField] private string _thisRunMarker = "< this run";
    [SerializeField] private string _emptyBoardText = "No escapes on {0} yet.";

    [Header("Look")]
    [Tooltip("Used only for 'New personal best!' / 'First escape' and the current run's row.")]
    [SerializeField] private Color _highlight = new Color(1f, 0.78f, 0.35f);

    [Header("Buttons (scene names must be in Build Settings)")]
    [SerializeField] private string _mazeSceneName = "MazeTest";
    [SerializeField] private string _difficultySceneName = "Difficulty";
    [SerializeField] private string _mainMenuSceneName = "Home";
    [SerializeField] private bool _keyboardShortcuts = true;

    [Header("Testing")]
    [Tooltip("Shows sample values when you press Play directly in this scene (never saved).")]
    [SerializeField] private bool _sampleWhenNoRun = true;

    private bool _escaped, _sample, _leaving;
    private string _difficulty;
    private int _tab;
    private readonly System.Collections.Generic.List<string> _tabs = new System.Collections.Generic.List<string>();

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _sample = !RunResult.HasRun && _sampleWhenNoRun;
        _escaped = _outcome == Outcome.Auto ? (RunResult.HasRun ? RunResult.Escaped : true) : _outcome == Outcome.Escaped;
        _difficulty = RunResult.HasRun ? RunResult.DifficultyKey : "Easy";

        _tabs.Clear();
        _tabs.AddRange(_difficultyTabs);
        if (!_tabs.Contains(_difficulty)) _tabs.Add(_difficulty);
        _tab = Mathf.Max(0, _tabs.IndexOf(_difficulty));

        FillRun();
        RefreshBoard();
    }

    void Update()
    {
        if (!_keyboardShortcuts || _leaving || Keyboard.current == null) return;
        Keyboard k = Keyboard.current;
        if (k.qKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame)  PreviousBoard();
        if (k.eKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) NextBoard();
        if (k.rKey.wasPressedThisFrame) PlayAgain();
        if (k.dKey.wasPressedThisFrame) ChangeDifficulty();
        if (k.escapeKey.wasPressedThisFrame) MainMenu();
    }

    // ---------- buttons ----------

    /// <summary>Same difficulty, straight back into the maze (GameSession.SelectedDifficulty is kept).</summary>
    public void PlayAgain()        => Load(_mazeSceneName);
    public void ChangeDifficulty() => Load(_difficultySceneName);
    public void MainMenu()         => Load(_mainMenuSceneName);
    public void NextBoard()        { _tab = (_tab + 1) % _tabs.Count; RefreshBoard(); }
    public void PreviousBoard()    { _tab = (_tab - 1 + _tabs.Count) % _tabs.Count; RefreshBoard(); }

    private void Load(string scene)
    {
        if (_leaving) return;
        if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"EndScreenFiller: scene '{scene}' is not in Build Settings (or the name doesn't match exactly).");
            return;
        }
        _leaving = true;
        SceneManager.LoadScene(scene);
    }

    [ContextMenu("Clear all personal bests")]
    private void ClearAllBests()
    {
        foreach (string d in _difficultyTabs) PersonalLeaderboard.Clear(d);
        Debug.Log("EndScreenFiller: personal bests cleared.");
    }

    // ---------- this run ----------

    private void FillRun()
    {
        float official   = _sample ? 195.40f : RunResult.OfficialSeconds;
        float best       = _sample ? 188.27f : RunResult.PreviousBest;          // best BEFORE this run
        int   boardIndex = _sample ? 1       : RunResult.BoardIndex;
        if (!_escaped && !_sample) best = PersonalLeaderboard.BestTime(_difficulty);

        Set(_difficultyValue, _difficulty);
        Set(_timeLabel, _escaped ? _winTimeLabel : string.Format(_loseTimeLabel, _difficulty));
        Set(_timeValue, RunResult.FormatTime(_escaped ? official : best));

        if (_personalBestLine == null) return;
        if (!_escaped)
            _personalBestLine.text = best < 0f ? "Reach the exit to set your first time." : "Reach the exit to beat it.";
        else if (boardIndex == 0 && best < 0f)
            _personalBestLine.text = Highlight($"First escape on {_difficulty}!");
        else if (boardIndex == 0)
            _personalBestLine.text = Highlight($"New personal best! {best - official:0.00}s faster");
        else if (best >= 0f)
            _personalBestLine.text = $"+{official - best:0.00}s behind your best ({RunResult.FormatTime(best)})";
        else
            _personalBestLine.text = "";
    }

    // ---------- personal bests ----------

    private void RefreshBoard()
    {
        string key = _tabs[_tab];
        Set(_boardSwitcher, string.Format(_switcherFormat, key));

        LeaderboardBoard board = PersonalLeaderboard.Get(key);
        bool thisRunsBoard = RunResult.HasRun && _escaped && key == _difficulty;

        if (_boardList != null)
        {
            if (board.entries.Count == 0)
            {
                _boardList.text = string.Format(_emptyBoardText, key);
            }
            else
            {
                var sb = new StringBuilder();
                int n = Mathf.Min(_rowsShown, board.entries.Count);
                for (int i = 0; i < n; i++)
                {
                    LeaderboardEntry e = board.entries[i];
                    string date = e.dateTicks > 0
                        ? new System.DateTime(e.dateTicks).ToString("dd MMM", CultureInfo.InvariantCulture)
                        : "";
                    string row = $"{i + 1}.<pos={_timeColumn}%>{RunResult.FormatTime(e.time)}<pos={_dateColumn}%>{date}";

                    bool mine = thisRunsBoard && i == RunResult.BoardIndex;
                    if (mine) row = Highlight(row + $"<pos={_markerColumn}%>{_thisRunMarker}");

                    if (i > 0) sb.Append('\n');
                    sb.Append(row);
                }
                _boardList.text = sb.ToString();
            }
        }

        Set(_attemptsLine, board.attempts > 0 ? $"Escapes: {board.escapes} / {board.attempts} attempts" : "");
    }

    // ---------- helpers ----------

    private string Highlight(string s) => $"<color=#{ColorUtility.ToHtmlStringRGB(_highlight)}>{s}</color>";

    private static void Set(TMP_Text t, string value)
    {
        if (t != null) t.text = value;
    }
}
