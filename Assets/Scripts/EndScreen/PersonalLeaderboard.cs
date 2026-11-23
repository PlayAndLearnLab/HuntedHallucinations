using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LeaderboardEntry
{
    public float time;      // official time (real time minus audit bonuses, plus penalties)
    public float real;      // wall-clock time
    public string grade;    // S / A / B / C / D / -
    public int caught;      // hallucinations flagged
    public int seen;        // hallucinations encountered
    public long dateTicks;  // DateTime.Now.Ticks when it was set
}

[Serializable]
public class LeaderboardBoard
{
    public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
    public int attempts;
    public int escapes;
}

/// <summary>
/// Local "personal bests" per difficulty, saved in PlayerPrefs as JSON (works in the editor, desktop and WebGL builds).
/// Only escapes go on the board; every run (won or lost) counts as an attempt.
/// </summary>
public static class PersonalLeaderboard
{
    public const int MaxEntries = 10;
    private const string KeyPrefix = "HH_PersonalBests_";

    public static LeaderboardBoard Get(string difficulty)
    {
        string json = PlayerPrefs.GetString(KeyPrefix + difficulty, "");
        if (string.IsNullOrEmpty(json)) return new LeaderboardBoard();
        try
        {
            LeaderboardBoard board = JsonUtility.FromJson<LeaderboardBoard>(json);
            if (board == null) return new LeaderboardBoard();
            if (board.entries == null) board.entries = new List<LeaderboardEntry>();
            return board;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"PersonalLeaderboard: could not read saved times for {difficulty} ({e.Message}). Starting fresh.");
            return new LeaderboardBoard();
        }
    }

    /// <summary>Best official time on this difficulty, or -1 if the player never escaped it.</summary>
    public static float BestTime(string difficulty)
    {
        LeaderboardBoard b = Get(difficulty);
        return b.entries.Count > 0 ? b.entries[0].time : -1f;
    }

    public static void RecordAttempt(string difficulty, bool escaped)
    {
        LeaderboardBoard b = Get(difficulty);
        b.attempts++;
        if (escaped) b.escapes++;
        Save(difficulty, b);
    }

    /// <summary>Adds an escape. Returns its 0-based place on the board, or -1 if it didn't make the top list.</summary>
    public static int Submit(string difficulty, LeaderboardEntry entry)
    {
        LeaderboardBoard b = Get(difficulty);
        b.entries.Add(entry);
        b.entries.Sort((x, y) => x.time.CompareTo(y.time));
        int index = b.entries.IndexOf(entry);
        if (b.entries.Count > MaxEntries) b.entries.RemoveRange(MaxEntries, b.entries.Count - MaxEntries);
        Save(difficulty, b);
        return index < MaxEntries ? index : -1;
    }

    public static void Clear(string difficulty)
    {
        PlayerPrefs.DeleteKey(KeyPrefix + difficulty);
        PlayerPrefs.Save();
    }

    private static void Save(string difficulty, LeaderboardBoard b)
    {
        PlayerPrefs.SetString(KeyPrefix + difficulty, JsonUtility.ToJson(b));
        PlayerPrefs.Save();
    }
}
