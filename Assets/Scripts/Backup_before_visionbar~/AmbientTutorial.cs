using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Teaches the ambient-hallucination loop at the moment it happens, instead of up front.
/// Every hint shows ONCE per run (optionally once per device), one at a time, top-centre of the screen:
///
///   0. Opening popup (a few seconds after the Binder is picked up, or before the first hint below):
///      what the random items are and what to do about them.
///   1. First obvious fake in range   -> "the AI made this up, flag it" (+ an AI taunt).
///   2. First genuine asset in range   -> "this one is real, leave it, wrong flags cost time".
///      Only fires for a plain game asset (no manual page, not a junction), so it never reveals a puzzle-based item.
///   3. First puzzle-based item in range -> "you can't tell by looking: open your manual, then decide". Reveals nothing.
///   4. First time the Sense-making bar moves (first flag or first AI strike) -> what the bar means; the bar pulses.
///
/// Hints pause and hide while the book or another panel has the cursor, and resume after.
/// Created automatically by AuditScoreManager (Auto Create Tutorial). All wording is in the Inspector.
/// </summary>
public class AmbientTutorial : MonoBehaviour
{
    [Header("Opening popup")]
    [TextArea(2, 4)] [SerializeField] private string _openingText =
        "<b>The AI is slipping fake things into your game.</b>\nCheck your manual. Flag only what doesn't match.";
    [Tooltip("Seconds after the Binder pickup (lets the 'You found the Manual' message be read first).")]
    [SerializeField] private float _openingDelayAfterBinder = 4.5f;

    [Header("First-time hints")]
    [TextArea(2, 4)] [SerializeField] private string _obviousFakeText =
        "<b>This doesn't belong in your game.</b>\nThe AI made it up. Press F to flag it.";
    [SerializeField] private string _obviousFakeAiLine = "Like my new asset? I made it myself.";
    [TextArea(2, 4)] [SerializeField] private string _genuineText =
        "<b>This one is from your game. It's real.</b>\nLeave real things alone: wrong flags cost time.";
    [SerializeField] private string _genuineAiLine = "Go on, flag it. I dare you.";
    [TextArea(2, 4)] [SerializeField] private string _puzzleItemText =
        "<b>Real or AI-made? You can't tell by looking.</b>\nOpen your manual (I), find its page, then decide.";
    [TextArea(2, 4)] [SerializeField] private string _barText =
        "<b>Sense-making bar (top left)</b>\nGrows most when you check your manual before flagging. The AI drains it.";

    [Header("Timing")]
    [Tooltip("How long each hint stays on screen (seconds). Paused while the book is open.")]
    [SerializeField] private float _hintDuration = 6.5f;
    [SerializeField] private float _gapBetweenHints = 0.6f;
    [Tooltip("How close (units) the player must be to an item for its hint to fire. Same as the F prompt range by default.")]
    [SerializeField] private float _triggerRange = 3f;

    [Header("Repeat")]
    [Tooltip("Off = hints show on every run (good for shared classroom devices). On = each hint only ever shows once on this device.")]
    [SerializeField] private bool _onlyOncePerDevice = false;

    [Header("Look")]
    [Tooltip("Screen position of the hint's centre: X 0 = left edge, 1 = right edge; Y 0 = bottom, 1 = top. Updates live in Play mode.")]
    [SerializeField] private Vector2 _anchor = new Vector2(0.5f, 0.72f);
    [Tooltip("Extra nudge in pixels (at 1920x1080) after the anchor. Updates live in Play mode.")]
    [SerializeField] private Vector2 _offset = Vector2.zero;
    [Tooltip("Size multiplier for the hint panel. Updates live in Play mode.")]
    [SerializeField] private float _scale = 1f;
    [Tooltip("On = reuse the parchment look of the manual message popup. Off (or no popup in the scene) = a plain dark panel.")]
    [SerializeField] private bool _useParchment = true;

    private enum Hint { Opening, ObviousFake, Genuine, PuzzleItem, Bar }

    private readonly HashSet<Hint> _done = new HashSet<Hint>();
    private readonly List<Hint> _queue = new List<Hint>();
    private readonly HashSet<Hint> _inRange = new HashSet<Hint>(); // item hints whose kind of item is near the player right now

    private Hint _current;
    private bool _showing;
    private float _remaining;
    private float _nextAllowed;
    private float _openingAt = -1f;
    private bool _flaggedSinceShow;

    private GameObject _player;
    private AuditHUD _hud;
    private CanvasGroup _group;
    private TMP_Text _text;
    private RectTransform _panelRT;
    private float _fade;

    private const string PrefsPrefix = "AmbientTutorial.";

    void Start()
    {
        Build();

        if (GDDManager.Instance != null)
        {
            if (GDDManager.Instance.HasBinder) ScheduleOpening();
            else GDDManager.Instance.OnBinderCollected += ScheduleOpening;
        }
        if (AuditScoreManager.Instance != null)
        {
            AuditScoreManager.Instance.OnPlayerResult += HandleResult;
            AuditScoreManager.Instance.OnAiStrike += HandleStrike;
        }
    }

    void OnDestroy()
    {
        if (GDDManager.Instance != null) GDDManager.Instance.OnBinderCollected -= ScheduleOpening;
        if (AuditScoreManager.Instance != null)
        {
            AuditScoreManager.Instance.OnPlayerResult -= HandleResult;
            AuditScoreManager.Instance.OnAiStrike -= HandleStrike;
        }
    }

    private void ScheduleOpening()
    {
        if (_openingAt < 0f) _openingAt = Time.unscaledTime + _openingDelayAfterBinder;
    }

    private void HandleResult(bool good) { _flaggedSinceShow = true; Request(Hint.Bar); }
    private void HandleStrike() => Request(Hint.Bar);

    // ---------- queue ----------

    private bool AlreadyShown(Hint h) =>
        _done.Contains(h) || (_onlyOncePerDevice && PlayerPrefs.GetInt(PrefsPrefix + h, 0) == 1);

    private void Request(Hint h)
    {
        if (AlreadyShown(h) || _queue.Contains(h) || (_showing && _current == h)) return;

        // The opening popup always comes first: it says what all the other hints are about.
        if (h != Hint.Opening && !AlreadyShown(Hint.Opening) && !_queue.Contains(Hint.Opening) && !(_showing && _current == Hint.Opening))
            _queue.Add(Hint.Opening);

        if (h == Hint.Opening) _queue.Insert(0, h);
        else _queue.Add(h);
    }

    void Update()
    {
        if (_openingAt >= 0f && Time.unscaledTime >= _openingAt)
        {
            _openingAt = -1f;
            Request(Hint.Opening);
        }

        WatchItems();

        bool blocked = Cursor.lockState != CursorLockMode.Locked; // book / controls panel open
        float dt = Time.unscaledDeltaTime;

        if (_showing)
        {
            if (!blocked) _remaining -= dt;
            // The player walked away from (or flagged) the item this hint is about: wrap it up quickly
            // so "this one" never ends up sitting next to a different object.
            if (IsItemHint(_current) && !_inRange.Contains(_current) && _remaining > 1.5f)
            {
                // Barely seen (player ran past)? Let it come back at the next item of that kind.
                if (_hintDuration - _remaining < 2.5f && !_flaggedSinceShow) Forget(_current);
                _remaining = 1.5f;
            }
            if (_remaining <= 0f)
            {
                _showing = false;
                _nextAllowed = Time.unscaledTime + _gapBetweenHints;
            }
        }
        else if (_queue.Count > 0 && !blocked && Time.unscaledTime >= _nextAllowed)
        {
            Hint next = _queue[0];
            _queue.RemoveAt(0);
            // An item hint only makes sense while that item is still in front of the player.
            // If they walked past it, drop it for now: the next item of that kind asks again.
            if (!IsItemHint(next) || _inRange.Contains(next)) Show(next);
        }

        ApplyPlacement(); // cheap; lets the Inspector values move the hint live

        // fade: visible only while a hint is running and nothing covers the screen
        float want = _showing && !blocked ? 1f : 0f;
        _fade = Mathf.MoveTowards(_fade, want, dt * 5f);
        if (_group != null && !Mathf.Approximately(_group.alpha, _fade)) _group.alpha = _fade;
    }

    private void Show(Hint h)
    {
        _current = h;
        _showing = true;
        _remaining = _hintDuration;
        _flaggedSinceShow = false;
        _done.Add(h);
        if (_onlyOncePerDevice) { PlayerPrefs.SetInt(PrefsPrefix + h, 1); PlayerPrefs.Save(); }

        if (_text != null) _text.text = TextFor(h);

        if (_hud == null) _hud = FindAnyObjectByType<AuditHUD>();
        switch (h)
        {
            case Hint.ObviousFake: if (_hud != null) _hud.SayLine(_obviousFakeAiLine); break;
            case Hint.Genuine:     if (_hud != null) _hud.SayLine(_genuineAiLine); break;
            case Hint.Bar:         if (_hud != null) _hud.HighlightBar(_hintDuration); break;
        }
    }

    private void Forget(Hint h)
    {
        _done.Remove(h);
        if (_onlyOncePerDevice) { PlayerPrefs.DeleteKey(PrefsPrefix + h); PlayerPrefs.Save(); }
    }

    private static bool IsItemHint(Hint h) => h == Hint.ObviousFake || h == Hint.Genuine || h == Hint.PuzzleItem;

    private string TextFor(Hint h)
    {
        switch (h)
        {
            case Hint.Opening:     return _openingText;
            case Hint.ObviousFake: return _obviousFakeText;
            case Hint.Genuine:     return _genuineText;
            case Hint.PuzzleItem:  return _puzzleItemText;
            default:               return _barText;
        }
    }

    // ---------- triggers ----------

    // Fires the item hints the first time the player is close to the matching kind of item.
    private void WatchItems()
    {
        _inRange.Clear();
        if (_player == null) _player = GameObject.FindWithTag("Player");
        if (_player == null) return;

        // Only the CLOSEST item counts (the same one F would flag), so "this one" in a hint can't point at the wrong object.
        AmbientHallucination closest = null;
        float bestSqr = _triggerRange * _triggerRange;
        foreach (AmbientHallucination item in AmbientHallucination.Active)
        {
            if (item == null || item.Resolved || !item.IsVisibleNow) continue;

            Vector3 d = item.transform.position - _player.transform.position;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr > bestSqr) continue;
            bestSqr = sqr;
            closest = item;
        }
        if (closest == null || closest.IsJunction) return; // junction puzzles have their own messages

        Hint h;
        if (closest.Obvious && closest.IsFake)          h = Hint.ObviousFake;
        else if (closest.Source != null)                h = Hint.PuzzleItem;
        else if (!closest.IsFake && !closest.Obvious)   h = Hint.Genuine;
        else return; // an always-fake item with no page that isn't marked Obvious: nothing to say without revealing it

        _inRange.Add(h);
        if (!AlreadyShown(h)) Request(h);
    }

    // ---------- UI ----------

    private void ApplyPlacement()
    {
        if (_panelRT == null) return;
        if (_panelRT.anchorMin != _anchor || _panelRT.anchorMax != _anchor) _panelRT.anchorMin = _panelRT.anchorMax = _anchor;
        if (_panelRT.anchoredPosition != _offset) _panelRT.anchoredPosition = _offset;
        Vector3 sc = Vector3.one * Mathf.Max(0.1f, _scale);
        if (_panelRT.localScale != sc) _panelRT.localScale = sc;
    }

    private void Build()
    {
        var canvasGO = new GameObject("AmbientTutorialCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45; // hidden anyway while the book is open (see Update)
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform panel = _useParchment ? CloneParchment(canvasGO.transform) : null;
        if (panel == null) panel = BuildPlainPanel(canvasGO.transform);

        panel.pivot = new Vector2(0.5f, 0.5f);
        _panelRT = panel;
        ApplyPlacement();

        _group = panel.GetComponent<CanvasGroup>();
        if (_group == null) _group = panel.gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
    }

    // Copies the manual message popup (parchment art + font) so hints look like the rest of the game.
    private RectTransform CloneParchment(Transform parent)
    {
        GDDMessageUI ui = GDDMessageUI.Instance != null ? GDDMessageUI.Instance : FindAnyObjectByType<GDDMessageUI>();
        if (ui == null || ui.PanelGroup == null) return null;
        // If the popup script sits on the panel itself, a copy would run its Awake and destroy itself: use the plain panel.
        if (ui.PanelGroup.GetComponentInChildren<GDDMessageUI>(true) != null) return null;

        RectTransform src = (RectTransform)ui.PanelGroup.transform;
        GameObject copy = Instantiate(ui.PanelGroup.gameObject, parent, false);
        copy.name = "TutorialHint";
        copy.SetActive(true);

        // The copy must be passive: strip any behaviours other than layout/graphics.
        foreach (MonoBehaviour mb in copy.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(mb is Graphic) && !(mb is BaseMeshEffect) && !(mb is LayoutGroup) && !(mb is ContentSizeFitter) && !(mb is LayoutElement) && !(mb is Mask) && !(mb is RectMask2D))
                Destroy(mb);

        RectTransform rt = (RectTransform)copy.transform;
        Vector2 size = src.rect.size;
        if (size.x < 300f || size.y < 80f) size = new Vector2(660f, 210f);
        rt.sizeDelta = new Vector2(Mathf.Max(size.x * 1.1f, 640f), Mathf.Max(size.y, 190f)); // a little wider: two lines of hint

        _text = copy.GetComponentInChildren<TMP_Text>(true);
        if (_text == null) { Destroy(copy); return null; }
        float baseSize = _text.fontSize;
        _text.enableAutoSizing = true;
        _text.fontSizeMax = baseSize * 1.25f; // a bit bigger than normal messages: read in ~3 seconds
        _text.fontSizeMin = baseSize * 0.7f;
        _text.richText = true;
        _text.raycastTarget = false;
        return rt;
    }

    private RectTransform BuildPlainPanel(Transform parent)
    {
        var panelGO = new GameObject("TutorialHint", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelGO.transform.SetParent(parent, false);
        var rt = (RectTransform)panelGO.transform;
        rt.sizeDelta = new Vector2(720f, 150f);
        var bg = panelGO.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.04f, 0.08f, 0.85f);
        bg.raycastTarget = false;

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(panelGO.transform, false);
        var trt = (RectTransform)textGO.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(24f, 14f); trt.offsetMax = new Vector2(-24f, -14f);
        var tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 30f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = 30f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.95f, 0.8f);
        tmp.richText = true;
        tmp.raycastTarget = false;
        _text = tmp;
        return rt;
    }
}
