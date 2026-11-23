using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Added at runtime by AmbientHallucinationSpawner to every corridor item
/// (a hallucinated one OR a genuine "bait" one). Holds the hidden truth
/// (IsFake) and plays the resolve effects. Has no collider - the player
/// interacts with it through AuditFlagController's distance check.
/// </summary>
public class AmbientHallucination : MonoBehaviour
{
    public static readonly List<AmbientHallucination> Active = new List<AmbientHallucination>();

    public bool IsFake { get; private set; }
    public bool Resolved { get; private set; }
    public bool Encountered { get; private set; }

    /// <summary>True for the assets/labels shown at an intersection (the puzzle itself), false for corridor items.</summary>
    public bool IsJunction { get; private set; }
    public string DisplayName { get; private set; } = "this asset";

    /// <summary>The puzzle this item was built from (its manual page can settle it). Null for items with no page.</summary>
    public PuzzleData Source { get; set; }
    /// <summary>An asset from another game: spottable by gut feeling alone, no page needed.</summary>
    public bool Obvious { get; set; }

    private bool _billboard;
    private Renderer[] _renderers;
    private Canvas[] _canvases;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _canvases  = GetComponentsInChildren<Canvas>(true);
    }

    void OnEnable()  => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    public void Setup(bool isFake, string displayName, bool billboard, bool isJunction = false)
    {
        IsFake = isFake;
        IsJunction = isJunction;
        DisplayName = string.IsNullOrEmpty(displayName) ? "this asset" : displayName;
        _billboard = billboard;
    }

    // Text labels face the camera (their forward points away from the viewer,
    // same convention the intersection text labels use).
    void LateUpdate()
    {
        if (!_billboard) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 away = transform.position - cam.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(away);
    }

    /// <summary>
    /// Makes any spawned puzzle object flaggable. Used by the intersection spawners so that
    /// every hallucination in the game can be reported, not just the corridor ones.
    /// </summary>
    public static AmbientHallucination Attach(GameObject obj, bool isFake, string displayName, bool isJunction, PuzzleData source = null)
    {
        if (obj == null) return null;
        AmbientHallucination item = obj.GetComponent<AmbientHallucination>();
        if (item == null) item = obj.AddComponent<AmbientHallucination>();
        item.Setup(isFake, displayName, false, isJunction);
        item.Source = source;
        return item;
    }

    /// <summary>
    /// False while the object is hidden (an intersection asset before the player reaches the junction).
    /// Hidden things can't be flagged or counted as seen.
    /// </summary>
    public bool IsVisibleNow
    {
        get
        {
            if (!gameObject.activeInHierarchy) return false;
            if (_renderers != null)
                foreach (Renderer r in _renderers) if (r != null && r.enabled) return true;
            if (_canvases != null)
                foreach (Canvas c in _canvases) if (c != null && c.enabled) return true;
            return false;
        }
    }

    /// <summary>Marks the item as seen. Returns true only the first time.</summary>
    public bool MarkEncountered()
    {
        if (Encountered) return false;
        Encountered = true;
        return true;
    }

    /// <summary>The player correctly flagged a fake: glitch out, then disappear.</summary>
    public void ResolveCaught()
    {
        if (Resolved) return;
        Resolved = true;
        StartCoroutine(GlitchAndRemove());
    }

    /// <summary>The player flagged a genuine item: it stays, but can't be flagged again.</summary>
    public void ResolveGenuine()
    {
        Resolved = true;
    }

    private IEnumerator GlitchAndRemove()
    {
        for (int i = 0; i < 8; i++)
        {
            SetVisible(i % 2 == 1);
            yield return new WaitForSeconds(0.06f);
        }
        Destroy(gameObject);
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer r in _renderers) if (r != null) r.enabled = visible;
        foreach (Canvas c in _canvases)    if (c != null) c.enabled = visible;
    }
}
