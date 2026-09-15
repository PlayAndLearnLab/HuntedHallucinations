using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Central singleton for the GDD Binder / Page mechanic.
///
/// - Picking up the Binder (at spawn, cell 0,0) sets HasBinder and unlocks the book UI.
/// - Picking up a Page sets CurrentPage to that page's PuzzleData, which is always the
///   ground-truth reference for the very next intersection ahead on the solution path
///   (guaranteed by IntersectionDetector.SpawnGDDPages, which places the page exactly
///   one cell before that intersection).
/// - GDDBookRenderer reads CurrentPage whenever the book is opened.
/// </summary>
public class GDDManager : MonoBehaviour
{
    public static GDDManager Instance { get; private set; }

    public bool HasBinder { get; private set; }

    private readonly List<PuzzleData> _collectedPages = new List<PuzzleData>();

    /// <summary>All pages collected so far, in pickup order — what GDDBookRenderer flips through.</summary>
    public IReadOnlyList<PuzzleData> CollectedPages => _collectedPages;

    /// <summary>The most recently collected page — the reference the player should be
    /// checking against the intersection they're about to reach.</summary>
    public PuzzleData CurrentPage { get; private set; }

    public event Action OnBinderCollected;
    public event Action<PuzzleData> OnPageCollected;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void CollectBinder()
    {
        if (HasBinder) return;
        HasBinder = true;
        OnBinderCollected?.Invoke();
        Log("You found the Game Design Document! Collect the missing design pages to help you navigate the maze.");
    }
 

    public void CollectPage(PuzzleData data)
    {
        if (data == null) return;
        if (_collectedPages.Contains(data))
        {
            CurrentPage = data; // already have it — just keep it as the "current" reference
            return;
        }

        _collectedPages.Add(data);
        CurrentPage = data;
        OnPageCollected?.Invoke(data);
        Log(string.IsNullOrEmpty(data.pageTitle)
            ? "Design page recovered. Check your GDD before the next junction."
            : $"Design page recovered: {data.pageTitle}");
    }

    public bool HasCollected(PuzzleData data) => _collectedPages.Contains(data);

    // Reuses the existing puzzle-hint popup channel (PuzzleUI) so log prompts
    // show up through the same UI the puzzles already use. Falls back to a
    // console log if PuzzleUI isn't in the scene yet (e.g. very early Awake order).
    private void Log(string message)
    {
        if (PuzzleUI.Instance != null)
            PuzzleUI.Instance.ShowPuzzlePopup(message);
        else
            Debug.Log($"[GDD] {message}");
    }
}


// using UnityEngine;
// using System;
// using System.Collections.Generic;

// /// <summary>
// /// Central singleton for the GDD Binder / Page mechanic.
// ///
// /// - Picking up the Binder (at spawn, cell 0,0) sets HasBinder and unlocks the book UI.
// /// - Picking up a Page sets CurrentPage to that page's PuzzleData, which is always the
// ///   ground-truth reference for the very next intersection ahead on the solution path
// ///   (guaranteed by IntersectionDetector.SpawnGDDPages, which places the page exactly
// ///   one cell before that intersection).
// /// - GDDBookRenderer reads CurrentPage whenever the book is opened.
// /// </summary>
// public class GDDManager : MonoBehaviour
// {
//     public static GDDManager Instance { get; private set; }

//     public bool HasBinder { get; private set; }

//     private readonly List<PuzzleData> _collectedPages = new List<PuzzleData>();

//     /// <summary>All pages collected so far, in pickup order — what GDDBookRenderer flips through.</summary>
//     public IReadOnlyList<PuzzleData> CollectedPages => _collectedPages;

//     /// <summary>The most recently collected page — the reference the player should be
//     /// checking against the intersection they're about to reach.</summary>
//     public PuzzleData CurrentPage { get; private set; }

//     public event Action OnBinderCollected;
//     public event Action<PuzzleData> OnPageCollected;

//     void Awake()
//     {
//         if (Instance != null && Instance != this) { Destroy(gameObject); return; }
//         Instance = this;
//     }

//     public void CollectBinder()
//     {
//         if (HasBinder) return;
//         HasBinder = true;
//         OnBinderCollected?.Invoke();
//         Log("GDD Binder acquired. Collect the missing design pages to audit the AI's claims before each junction.");
//     }

//     public void CollectPage(PuzzleData data)
//     {
//         if (data == null) return;
//         _collectedPages.Add(data);
//         CurrentPage = data;
//         OnPageCollected?.Invoke(data);
//         Log(string.IsNullOrEmpty(data.pageTitle)
//             ? "Design page recovered. Check your GDD before the next junction."
//             : $"Design page recovered: {data.pageTitle}");
//     }

//     public bool HasCollected(PuzzleData data) => _collectedPages.Contains(data);

//     // Reuses the existing puzzle-hint popup channel (PuzzleUI) so log prompts
//     // show up through the same UI the puzzles already use. Falls back to a
//     // console log if PuzzleUI isn't in the scene yet (e.g. very early Awake order).
//     private void Log(string message)
//     {
//         if (PuzzleUI.Instance != null)
//             PuzzleUI.Instance.ShowPuzzlePopup(message);
//         else
//             Debug.Log($"[GDD] {message}");
//     }
// }
