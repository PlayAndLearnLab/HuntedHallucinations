using System.Collections;
using UnityEngine;

/// <summary>
/// Timing + outcome of the current / most recent maze run, kept in statics so the end scenes can read it
/// after the maze scene is gone (same idea as AuditScoreManager.LastRun).
///
/// Times:
///   RealSeconds     = wall-clock time from the start of the timer to the end of the run.
///   AuditAdjust     = net seconds the audit put ON the clock (+ bonuses for good flags, - penalties).
///   OfficialSeconds = RealSeconds - AuditAdjust. This is the time on the leaderboard: like a racing game,
///                     good flags shave time off and false alarms add to it, so auditing well matters for the PB.
///
/// TimerManager calls Begin / AddAdjust / Finish(false); ExitPortal calls Finish(true).
/// </summary>
public static class RunResult
{
    public static bool HasRun { get; private set; }
    public static bool Escaped { get; private set; }
    public static float RealSeconds { get; private set; }
    public static float AuditAdjust { get; private set; }
    public static float OfficialSeconds => Mathf.Max(0f, RealSeconds - AuditAdjust);
    public static float TotalTime { get; private set; }
    public static string DifficultyKey { get; private set; } = "Medium";
    public static string Grade { get; private set; } = "-";

    /// <summary>Index (0-based) of this run on its difficulty's board, or -1 (lost / didn't make the top list).</summary>
    public static int BoardIndex { get; private set; } = -1;
    /// <summary>Best official time on this difficulty BEFORE this run, or -1 if there was none.</summary>
    public static float PreviousBest { get; private set; } = -1f;
    public static bool IsNewBest => Escaped && BoardIndex == 0;

    /// <summary>The last frame of the maze, captured right before the ending transition. Can be null.</summary>
    public static Texture2D LastFrame { get; private set; }

    private static float _startTime;
    private static bool _running;

    public static void Begin(float totalTime)
    {
        _running = true;
        _startTime = Time.time;
        TotalTime = totalTime;
        AuditAdjust = 0f;
        RealSeconds = 0f;
        BoardIndex = -1;
        PreviousBest = -1f;
        DifficultyKey = CurrentDifficultyKey();
    }

    /// <summary>Pass the delta that was REALLY applied to the clock (after clamping).</summary>
    public static void AddAdjust(float appliedDelta)
    {
        if (_running) AuditAdjust += appliedDelta;
    }

    public static void Finish(bool escaped)
    {
        if (!_running) return;
        _running = false;

        HasRun = true;
        Escaped = escaped;
        RealSeconds = Time.time - _startTime;

        AuditRunStats s = AuditScoreManager.LastRun;
        Grade = ComputeGrade(s, escaped);

        PersonalLeaderboard.RecordAttempt(DifficultyKey, escaped);
        PreviousBest = PersonalLeaderboard.BestTime(DifficultyKey);
        BoardIndex = -1;

        if (escaped)
        {
            var entry = new LeaderboardEntry
            {
                time = OfficialSeconds,
                real = RealSeconds,
                grade = Grade,
                caught = s != null ? s.FakesFlagged : 0,
                seen = s != null ? s.FakesSeen : 0,
                dateTicks = System.DateTime.Now.Ticks,
            };
            BoardIndex = PersonalLeaderboard.Submit(DifficultyKey, entry);
        }

        CaptureLastFrame();
    }

    // ---------- grade ----------

    /// <summary>S / A / B / C / D from how well the sense-making bar was held (relative to its cap). "-" if too few reports.</summary>
    public static string ComputeGrade(AuditRunStats s, bool escaped)
    {
        if (s == null || s.Judgments < AuditRunStats.MinJudgmentsForRank) return "-";
        float r = s.HeldRatio;
        if (r >= 0.9f) return escaped ? "S" : "A";
        if (r >= 0.7f) return "A";
        if (r >= 0.5f) return "B";
        if (r >= 0.3f) return "C";
        return "D";
    }

    // ---------- difficulty ----------

    /// <summary>
    /// "Easy" / "Medium" / "Hard" from the chosen MazeDifficultySettings asset's name (e.g. "DifficultySettings_Hard" -> "Hard").
    /// Any other name is used as-is.
    /// </summary>
    public static string CurrentDifficultyKey()
    {
        MazeDifficultySettings settings = GameSession.SelectedDifficulty;
        if (settings == null && PenaltyMazeManager.Instance != null) settings = PenaltyMazeManager.Instance.DifficultySettings;
        if (settings == null) return "Medium";
        return NormalizeDifficulty(settings.name);
    }

    public static string NormalizeDifficulty(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Medium";
        string n = raw.ToLowerInvariant();
        if (n.Contains("easy")) return "Easy";
        if (n.Contains("medium") || n.Contains("normal")) return "Medium";
        if (n.Contains("hard")) return "Hard";
        return raw;
    }

    // ---------- last frame ----------

    private static void CaptureLastFrame()
    {
        var go = new GameObject("RunResult FrameGrab");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<FrameGrabber>();
    }

    private class FrameGrabber : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return new WaitForEndOfFrame();
            if (LastFrame != null) Object.Destroy(LastFrame);
            LastFrame = ScreenCapture.CaptureScreenshotAsTexture();
            if (LastFrame != null) LastFrame.hideFlags = HideFlags.HideAndDontSave; // survives the scene change
            Destroy(gameObject);
        }
    }

    /// <summary>Formats seconds as mm:ss.cc (e.g. 03:07.42).</summary>
    public static string FormatTime(float seconds, bool centiseconds = true)
    {
        if (seconds < 0f) return "--:--";
        int total = Mathf.FloorToInt(seconds);
        int m = total / 60;
        int s = total % 60;
        if (!centiseconds) return $"{m:00}:{s:00}";
        int cs = Mathf.FloorToInt((seconds - total) * 100f);
        return $"{m:00}:{s:00}.{cs:00}";
    }
}
