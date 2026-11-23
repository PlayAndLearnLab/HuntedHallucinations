using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers which manual pages the player has actually looked at in the book, and when.
/// AuditScoreManager uses it to tell a flag that was checked against the source from one made from memory or gut feeling.
/// The check is invisible to the player (it never changes what the world shows).
/// </summary>
public static class BookCheckTracker
{
    private static readonly Dictionary<PuzzleData, float> _lastViewed = new Dictionary<PuzzleData, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { _lastViewed.Clear(); }

    public static void Clear() { _lastViewed.Clear(); }

    /// <summary>Called by GDDBookRenderer every time a page is shown.</summary>
    public static void RecordView(PuzzleData page)
    {
        if (page != null) _lastViewed[page] = Time.time;
    }

    /// <summary>True if this page was on screen within the last `windowSeconds`.</summary>
    public static bool WasChecked(PuzzleData page, float windowSeconds)
    {
        if (page == null) return false;
        return _lastViewed.TryGetValue(page, out float t) && Time.time - t <= windowSeconds;
    }
}
