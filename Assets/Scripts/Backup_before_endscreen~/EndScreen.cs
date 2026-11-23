using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Results screen for BOTH endings (CongratsScene and GameOver). Drop it on an empty GameObject in each scene:
/// it builds its whole UI at runtime (like LearningObjectivesSplash), reads RunResult + AuditScoreManager.LastRun,
/// saves the run to the personal leaderboard, and turns off the scene's old canvases.
///
/// Layout: left = outcome, final time, personal-best delta, grade, audit stats. Right = personal bests per difficulty
/// (tabs, Q/E or arrows to switch). Bottom = Retry [R] / Difficulty [D] / Main menu [Esc].
///
/// Opening a scene directly in the editor (no run played) shows preview numbers so the layout can be tuned.
/// Right-click the component > "Clear all personal bests" to wipe saved times.
/// </summary>
public class EndScreen : MonoBehaviour
{
    public enum Outcome { Auto, Escaped, TimedOut }

    [Header("Which ending")]
    [Tooltip("Auto = taken from the run that just ended. Set it per scene if you want to force it.")]
    [SerializeField] private Outcome _outcome = Outcome.Auto;

    [Header("Flow (a button hides itself if its scene isn't in Build Settings)")]
    [SerializeField] private string _retrySceneName = "MazeTest";
    [SerializeField] private string _difficultySceneName = "DifficultyMenu";
    [SerializeField] private string _mainMenuSceneName = "Intro";

    [Header("Background")]
    [Tooltip("Picture behind the results (e.g. the developer's room for the win). Empty = the frozen last frame of the run.")]
    [SerializeField] private Sprite _background;
    [SerializeField] private bool _useLastFrameIfNoBackground = true;
    [Tooltip("CRT scanlines over the background (on by default for the timed-out screen only).")]
    [SerializeField] private bool _scanlinesOnLose = true;
    [Tooltip("Turns off this scene's other canvases (the old end-screen texts) so only this screen shows.")]
    [SerializeField] private bool _hideOtherCanvases = true;

    [Header("Look")]
    [Tooltip("Optional fonts. Empty = TextMeshPro's default.")]
    [SerializeField] private TMP_FontAsset _headlineFont;
    [SerializeField] private TMP_FontAsset _bodyFont;
    [Tooltip("Optional. Empty = Resources/AIPortrait, or the generated face (see AiPortrait).")]
    [SerializeField] private Sprite _aiSprite;
    [SerializeField] private Color _winAccent = new Color(1f, 0.74f, 0.32f);
    [SerializeField] private Color _loseAccent = new Color(1f, 0.33f, 0.3f);
    [Tooltip("Wait this long before the reveal starts, so the flash transition can fade out first.")]
    [SerializeField] private float _revealDelay = 0.9f;

    [Header("Wording: escaped")]
    [SerializeField] private string _winTag = "RUN COMPLETE";
    [SerializeField] private string _winHeadline = "BUILD ESCAPED";
    [TextArea] [SerializeField] private string _winSubline = "Congratulations! You got out of the maze. Welcome back to the real world :)";

    [Header("Wording: timed out")]
    [SerializeField] private string _loseTag = "RUN FAILED";
    [SerializeField] private string _loseHeadline = "TIME'S UP";
    [TextArea] [SerializeField] private string _loseSubline = "You could not get out of the maze in time... You are trapped here forever. Until you find the exit?";
    [SerializeField] private string[] _aiTaunts =
    {
        "Told you. The exit was the other way.",
        "You should have checked your GDD.",
        "I was 100% confident. Was I right?",
        "Stay. The build is cosier with you in it.",
    };

    [Header("Leaderboard")]
    [Tooltip("Tabs shown on the board, in order. The run's own difficulty is added if it isn't in this list.")]
    [SerializeField] private string[] _difficultyTabs = { "Easy", "Medium", "Hard" };

    // ---------- palette ----------
    private static readonly Color TextMain  = new Color(0.96f, 0.96f, 0.98f);
    private static readonly Color TextMuted = new Color(0.68f, 0.70f, 0.78f);
    private static readonly Color PanelFill = new Color(0.07f, 0.07f, 0.11f, 0.78f);
    private static readonly Color CardFill  = new Color(1f, 1f, 1f, 0.06f);
    private static readonly Color Gold      = new Color(1f, 0.82f, 0.30f);
    private static readonly Color Faster    = new Color(0.45f, 0.92f, 0.60f);

    // ---------- run data ----------
    private bool _escaped;
    private bool _preview;
    private AuditRunStats _stats;
    private float _official, _real, _adjust, _previousBest, _totalTime;
    private string _difficulty, _grade;
    private int _boardIndex;
    private Color _accent;

    // ---------- built UI ----------
    private Sprite _rounded;
    private RectTransform _root;
    private TMP_Text _heroValue;
    private RectTransform _gradeBox;
    private RectTransform _pbBadge;
    private readonly List<RevealItem> _reveals = new List<RevealItem>();
    private readonly List<string> _tabs = new List<string>();
    private readonly List<Image> _tabImages = new List<Image>();
    private readonly List<TMP_Text> _tabLabels = new List<TMP_Text>();
    private readonly List<BoardRow> _rows = new List<BoardRow>();
    private TMP_Text _boardEmpty, _boardFooter;
    private int _tab;
    private bool _skip;
    private bool _leaving;
    private bool _interactive; // R / D / Esc only once the reveal is over (the first key press just skips it)
    private Action _retry, _changeDifficulty, _mainMenu;

    private class RevealItem { public CanvasGroup group; public RectTransform rt; public Vector2 basePos; public float delay; }
    private class BoardRow { public Image bg; public TMP_Text place, time, grade, date, tag; }

    // =====================================================================

    IEnumerator Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ReadRun();
        if (_hideOtherCanvases) HideOtherCanvases();
        EnsureEventSystem();
        Build();

        yield return Reveal();
    }

    void Update()
    {
        if (_leaving) return;
        Keyboard k = Keyboard.current;
        if (k == null) return;

        if (k.qKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame)  SelectTab(_tab - 1);
        if (k.eKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) SelectTab(_tab + 1);

        if (!_interactive) return;
        if (k.rKey.wasPressedThisFrame) _retry?.Invoke();
        if (k.dKey.wasPressedThisFrame) _changeDifficulty?.Invoke();
        if (k.escapeKey.wasPressedThisFrame || k.mKey.wasPressedThisFrame) _mainMenu?.Invoke();
    }

    [ContextMenu("Clear all personal bests")]
    private void ClearAllBests()
    {
        foreach (string d in _difficultyTabs) PersonalLeaderboard.Clear(d);
        Debug.Log("EndScreen: personal bests cleared.");
    }

    // ---------- data ----------

    private void ReadRun()
    {
        _preview = !RunResult.HasRun;

        if (_preview)
        {
            // Opened straight in the editor: show believable numbers, never touch saved data.
            _escaped = _outcome != Outcome.TimedOut;
            _stats = new AuditRunStats { FakesSeen = 7, FakesFlagged = 5, FalseAlarms = 1, ChecksPossible = 6, ChecksDone = 4, Cleaned = 0.62f, HeldBeforeExit = 0.62f };
            _real = 207.4f; _adjust = 12f; _official = _real - _adjust;
            _totalTime = 240f;
            _difficulty = "Medium";
            _previousBest = 201.36f;
            _boardIndex = -1;
            _grade = RunResult.ComputeGrade(_stats, _escaped);
        }
        else
        {
            _escaped = _outcome == Outcome.Auto ? RunResult.Escaped : _outcome == Outcome.Escaped;
            _stats = AuditScoreManager.LastRun ?? new AuditRunStats();
            _real = RunResult.RealSeconds;
            _adjust = RunResult.AuditAdjust;
            _official = RunResult.OfficialSeconds;
            _totalTime = RunResult.TotalTime;
            _difficulty = RunResult.DifficultyKey;
            _previousBest = RunResult.PreviousBest;
            _boardIndex = RunResult.BoardIndex;
            _grade = RunResult.Grade;
        }

        _accent = _escaped ? _winAccent : _loseAccent;

        _tabs.Clear();
        _tabs.AddRange(_difficultyTabs);
        if (!_tabs.Contains(_difficulty)) _tabs.Add(_difficulty);
        _tab = Mathf.Max(0, _tabs.IndexOf(_difficulty));
    }

    private void HideOtherCanvases()
    {
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas) continue;
            if (c.gameObject.scene != gameObject.scene) continue; // leave the DontDestroyOnLoad flash overlay alone
            if (c.transform.IsChildOf(transform)) continue;
            c.gameObject.SetActive(false);
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem (end screen)", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // =====================================================================
    // Build
    // =====================================================================

    private void Build()
    {
        _rounded = MakeRoundedSprite(48, 14);

        var canvasGO = new GameObject("EndScreenCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // under the flash transition (short.MaxValue)
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0f; // match width: the two columns never overlap on 3:2 / 16:10 Chromebook screens
        _root = (RectTransform)canvasGO.transform;

        BuildBackground();
        BuildLeftColumn();
        BuildBoard();
        BuildButtons();
        RefreshBoard();
    }

    private void BuildBackground()
    {
        var baseFill = Box("BaseFill", _root, new Color(0.03f, 0.03f, 0.06f, 1f));
        Stretch(baseFill);

        Texture tex = _background != null ? _background.texture
                    : (_useLastFrameIfNoBackground ? RunResult.LastFrame : null);
        if (tex != null)
        {
            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            bgGO.transform.SetParent(_root, false);
            var raw = bgGO.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            if (_background != null)
            {
                Rect r = _background.textureRect; // respects sprites packed in an atlas
                raw.uvRect = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
            }
            // a picture you chose is just dimmed; the frozen maze frame is tinted to match the ending
            raw.color = _background != null ? new Color(0.78f, 0.78f, 0.82f)
                      : _escaped ? new Color(0.55f, 0.55f, 0.62f) : new Color(0.62f, 0.26f, 0.28f);
            var fit = bgGO.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            float w = _background != null ? _background.textureRect.width : tex.width;
            float h = _background != null ? _background.textureRect.height : tex.height;
            fit.aspectRatio = h > 0 ? w / h : 16f / 9f;
            var rt = (RectTransform)bgGO.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        // Readability: dark on the left (results) and right (board), lighter in the middle so the art shows through.
        var shade = new GameObject("Shade", typeof(RectTransform), typeof(RawImage));
        shade.transform.SetParent(_root, false);
        var shadeImg = shade.GetComponent<RawImage>();
        shadeImg.texture = MakeHorizontalShade();
        shadeImg.raycastTarget = false;
        Stretch((RectTransform)shade.transform);

        if (!_escaped && _scanlinesOnLose)
        {
            var lines = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage));
            lines.transform.SetParent(_root, false);
            var li = lines.GetComponent<RawImage>();
            li.texture = MakeScanlineTexture();
            li.uvRect = new Rect(0f, 0f, 1f, 1080f / 4f);
            li.raycastTarget = false;
            Stretch((RectTransform)lines.transform);
        }
    }

    private void BuildLeftColumn()
    {
        const float x = 110f;
        const float width = 960f;

        // tag + headline + subline
        var tag = Text("Tag", _root, _escaped ? _winTag : _loseTag, 26f, FontStyles.Bold, _accent, TextAlignmentOptions.TopLeft,
                       0f, 1f, new Vector2(x, -86f), new Vector2(width, 34f), false);
        tag.characterSpacing = 18f;
        AddReveal(tag.rectTransform, 0.00f);

        var head = Text("Headline", _root, _escaped ? _winHeadline : _loseHeadline, 92f, FontStyles.Bold, TextMain, TextAlignmentOptions.TopLeft,
                        0f, 1f, new Vector2(x, -118f), new Vector2(width, 110f), true);
        head.enableAutoSizing = true; head.fontSizeMin = 50f; head.fontSizeMax = 92f;
        AddReveal(head.rectTransform, 0.08f);

        var sub = Text("Subline", _root, _escaped ? _winSubline : _loseSubline, 28f, FontStyles.Normal, TextMuted, TextAlignmentOptions.TopLeft,
                       0f, 1f, new Vector2(x, -234f), new Vector2(width, 80f), false);
        AddReveal(sub.rectTransform, 0.16f);

        // difficulty chip
        var chip = Rounded("DifficultyChip", _root, new Color(_accent.r, _accent.g, _accent.b, 0.18f));
        Anchor(chip, 0f, 1f); chip.anchoredPosition = new Vector2(x, -326f); chip.sizeDelta = new Vector2(230f, 46f);
        var chipText = Text("Label", chip, _difficulty.ToUpperInvariant() + "  MAZE", 22f, FontStyles.Bold, _accent, TextAlignmentOptions.Center,
                            0.5f, 0.5f, Vector2.zero, new Vector2(230f, 46f), false);
        chipText.characterSpacing = 8f;
        AddReveal(chip, 0.22f);

        BuildHero(x, width);
        BuildGradeAndStats(x, width);
    }

    private void BuildHero(float x, float width)
    {
        var hero = Rounded("HeroCard", _root, PanelFill);
        Anchor(hero, 0f, 1f); hero.anchoredPosition = new Vector2(x, -396f); hero.sizeDelta = new Vector2(width, 232f);
        AddReveal(hero, 0.30f);

        string label = _escaped ? "FINAL TIME" : $"YOUR BEST ON {_difficulty.ToUpperInvariant()}";
        var l = Text("Label", hero, label, 22f, FontStyles.Bold, TextMuted, TextAlignmentOptions.TopLeft,
                     0f, 1f, new Vector2(40f, -26f), new Vector2(560f, 30f), false);
        l.characterSpacing = 10f;

        float shownBest = _escaped ? _official : _previousBest;
        _heroValue = Text("Value", hero, RunResult.FormatTime(_escaped ? 0f : shownBest), 112f, FontStyles.Bold, TextMain, TextAlignmentOptions.TopLeft,
                          0f, 1f, new Vector2(34f, -46f), new Vector2(600f, 140f), true);

        string detail;
        if (_escaped)
        {
            string sign = _adjust >= 0f ? "-" : "+"; // a clock bonus takes time OFF the official time
            detail = $"Clock {RunResult.FormatTime(_real)}   <color=#{ColorUtility.ToHtmlStringRGB(TextMuted)}>·</color>   Audit {sign}{Mathf.Abs(_adjust):0.00}s";
        }
        else
        {
            detail = _previousBest < 0f
                ? $"Reach the exit within {RunResult.FormatTime(_totalTime, false)} to set your first time."
                : $"Reach the exit within {RunResult.FormatTime(_totalTime, false)} to beat it.";
        }
        Text("Detail", hero, detail, 22f, FontStyles.Normal, TextMuted, TextAlignmentOptions.BottomLeft,
             0f, 0f, new Vector2(40f, 22f), new Vector2(620f, 30f), false);

        if (_escaped) BuildPersonalBestBadge(hero);
        else          BuildAiTaunt(hero);
    }

    private void BuildPersonalBestBadge(RectTransform hero)
    {
        bool first = _previousBest < 0f;
        bool best  = _boardIndex == 0 || (_preview && _official < _previousBest);
        string title;
        Color color;
        string line;

        if (best && first)      { title = "FIRST ESCAPE";       color = Gold;   line = $"on {_difficulty}"; }
        else if (best)          { title = "NEW PERSONAL BEST";  color = Gold;   line = $"{_previousBest - _official:0.00}s faster"; }
        else                    { title = "PERSONAL BEST";      color = TextMuted; line = $"+{_official - _previousBest:0.00}s  ({RunResult.FormatTime(_previousBest)})"; }

        _pbBadge = Rounded("PBBadge", hero, best ? new Color(color.r, color.g, color.b, 0.22f) : CardFill);
        Anchor(_pbBadge, 1f, 1f); _pbBadge.anchoredPosition = new Vector2(-32f, -38f); _pbBadge.sizeDelta = new Vector2(290f, 54f);
        var t = Text("Title", _pbBadge, title, 22f, FontStyles.Bold, color, TextAlignmentOptions.Center,
                     0.5f, 0.5f, Vector2.zero, new Vector2(290f, 54f), true);
        t.characterSpacing = 6f;

        Text("Delta", hero, line, 26f, FontStyles.Bold, best ? Faster : TextMuted, TextAlignmentOptions.TopRight,
             1f, 1f, new Vector2(-34f, -106f), new Vector2(300f, 36f), false);

        if (_boardIndex > 0)
            Text("Place", hero, $"#{_boardIndex + 1} on your board", 22f, FontStyles.Normal, TextMuted, TextAlignmentOptions.TopRight,
                 1f, 1f, new Vector2(-34f, -146f), new Vector2(300f, 30f), false);

        if (!best) _pbBadge = null; // only pulse a real record
    }

    private void BuildAiTaunt(RectTransform hero)
    {
        var face = new GameObject("AI", typeof(RectTransform), typeof(Image));
        face.transform.SetParent(hero, false);
        var frt = (RectTransform)face.transform;
        Anchor(frt, 1f, 0.5f); frt.anchoredPosition = new Vector2(-30f, 0f); frt.sizeDelta = new Vector2(120f, 120f);
        var img = face.GetComponent<Image>();
        img.sprite = AiPortrait.Get(_aiSprite);
        img.preserveAspect = true;
        img.raycastTarget = false;

        var bubble = Rounded("Bubble", hero, new Color(0.55f, 0.25f, 0.85f, 0.30f));
        Anchor(bubble, 1f, 0.5f); bubble.anchoredPosition = new Vector2(-166f, 8f); bubble.sizeDelta = new Vector2(250f, 110f);
        string line = _aiTaunts != null && _aiTaunts.Length > 0 ? _aiTaunts[UnityEngine.Random.Range(0, _aiTaunts.Length)] : "";
        Text("Line", bubble, line, 22f, FontStyles.Italic, TextMain, TextAlignmentOptions.Center,
             0.5f, 0.5f, Vector2.zero, new Vector2(226f, 96f), false);
    }

    private void BuildGradeAndStats(float x, float width)
    {
        const float top = -652f;

        // grade "stamp"
        var gradeBox = Rounded("Grade", _root, PanelFill);
        Anchor(gradeBox, 0f, 1f);
        gradeBox.pivot = new Vector2(0.5f, 0.5f);
        gradeBox.anchoredPosition = new Vector2(x + 105f, top - 105f);
        gradeBox.sizeDelta = new Vector2(210f, 210f);
        Color gc = GradeColor(_grade);
        var outline = gradeBox.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(gc.r, gc.g, gc.b, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);
        Text("Letter", gradeBox, _grade, 150f, FontStyles.Bold, gc, TextAlignmentOptions.Center,
             0.5f, 0.5f, new Vector2(0f, 12f), new Vector2(210f, 180f), true);
        Text("Caption", gradeBox, "GRADE", 18f, FontStyles.Bold, TextMuted, TextAlignmentOptions.Center,
             0.5f, 0f, new Vector2(0f, 12f), new Vector2(210f, 26f), false).characterSpacing = 10f;
        _gradeBox = gradeBox;
        _gradeBox.localScale = Vector3.zero; // stamped in by Reveal()

        string rank = _stats.Rank;
        var rankText = Text("Rank", _root, rank, 24f, FontStyles.Bold, TextMain, TextAlignmentOptions.Top,
                            0f, 1f, new Vector2(x - 10f, top - 222f), new Vector2(230f, 34f), false);
        AddReveal(rankText.rectTransform, 0.75f);

        // 2 x 2 stat cards
        float cardsX = x + 240f;
        float cardW = (width - 240f - 16f) / 2f;
        const float cardH = 100f;

        string accuracy = _stats.Accuracy < 0f ? "-" : $"{Mathf.RoundToInt(_stats.Accuracy * 100f)}%";
        string checks   = _stats.ChecksPossible == 0 ? "-" : $"{_stats.ChecksDone}/{_stats.ChecksPossible}";
        string held     = $"{Mathf.RoundToInt(_stats.HeldRatio * 100f)}%";

        StatCard("Caught",   $"{_stats.FakesFlagged}/{_stats.FakesSeen}", "HALLUCINATIONS CAUGHT", cardsX,               top,               cardW, cardH, 0.55f);
        StatCard("False",    _stats.FalseAlarms.ToString(),               "FALSE ALARMS",          cardsX + cardW + 16f, top,               cardW, cardH, 0.62f, _stats.FalseAlarms > 0 ? _loseAccent : TextMain);
        StatCard("Accuracy", accuracy,                                    "AUDIT ACCURACY",        cardsX,               top - cardH - 14f, cardW, cardH, 0.69f);
        StatCard("Checked",  checks,                                      "CHECKED THE GDD FIRST", cardsX + cardW + 16f, top - cardH - 14f, cardW, cardH, 0.76f);

        var heldText = Text("Held", _root, $"Sense-making held at <b>{held}</b> of its maximum before the exit",
                            20f, FontStyles.Normal, TextMuted, TextAlignmentOptions.TopLeft,
                            0f, 1f, new Vector2(cardsX, top - 2f * cardH - 32f), new Vector2(width - 240f, 30f), false);
        if (_escaped) AddReveal(heldText.rectTransform, 0.82f);
        else heldText.gameObject.SetActive(false);
    }

    private void StatCard(string name, string value, string label, float x, float y, float w, float h, float delay, Color? valueColor = null)
    {
        var card = Rounded(name, _root, CardFill);
        Anchor(card, 0f, 1f); card.anchoredPosition = new Vector2(x, y); card.sizeDelta = new Vector2(w, h);
        Text("Value", card, value, 44f, FontStyles.Bold, valueColor ?? TextMain, TextAlignmentOptions.TopLeft,
             0f, 1f, new Vector2(22f, -10f), new Vector2(w - 30f, 54f), true);
        Text("Label", card, label, 17f, FontStyles.Bold, TextMuted, TextAlignmentOptions.TopLeft,
             0f, 1f, new Vector2(22f, -66f), new Vector2(w - 30f, 26f), false).characterSpacing = 4f;
        AddReveal(card, delay);
    }

    // ---------- leaderboard ----------

    private void BuildBoard()
    {
        const float w = 640f;
        var panel = Rounded("Board", _root, PanelFill);
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-110f, -86f);
        panel.sizeDelta = new Vector2(w, 820f);
        AddReveal(panel, 0.40f);

        var title = Text("Title", panel, "PERSONAL BESTS", 28f, FontStyles.Bold, TextMain, TextAlignmentOptions.TopLeft,
                         0f, 1f, new Vector2(34f, -28f), new Vector2(400f, 36f), true);
        title.characterSpacing = 8f;
        Text("Hint", panel, "Q / E", 18f, FontStyles.Bold, TextMuted, TextAlignmentOptions.TopRight,
             1f, 1f, new Vector2(-34f, -36f), new Vector2(120f, 26f), false);

        // tabs
        float tabW = (w - 68f - 8f * (_tabs.Count - 1)) / _tabs.Count;
        for (int i = 0; i < _tabs.Count; i++)
        {
            int index = i;
            var tab = new GameObject("Tab " + _tabs[i], typeof(RectTransform), typeof(Image), typeof(Button));
            tab.transform.SetParent(panel, false);
            var rt = (RectTransform)tab.transform;
            Anchor(rt, 0f, 1f); rt.anchoredPosition = new Vector2(34f + i * (tabW + 8f), -84f); rt.sizeDelta = new Vector2(tabW, 50f);
            var img = tab.GetComponent<Image>();
            img.sprite = _rounded; img.type = Image.Type.Sliced;
            var btn = tab.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => SelectTab(index));
            var label = Text("Label", rt, _tabs[i].ToUpperInvariant(), 20f, FontStyles.Bold, TextMain, TextAlignmentOptions.Center,
                             0.5f, 0.5f, Vector2.zero, new Vector2(tabW, 50f), false);
            label.characterSpacing = 6f;
            _tabImages.Add(img);
            _tabLabels.Add(label);
        }

        // column headers
        const float headY = -158f;
        Text("H#",    panel, "#",     17f, FontStyles.Bold, TextMuted, TextAlignmentOptions.TopLeft,  0f, 1f, new Vector2(46f, headY),  new Vector2(40f, 24f), false);
        Text("HTime", panel, "TIME",  17f, FontStyles.Bold, TextMuted, TextAlignmentOptions.TopLeft,  0f, 1f, new Vector2(110f, headY), new Vector2(160f, 24f), false);
        Text("HGr",   panel, "GRADE", 17f, FontStyles.Bold, TextMuted, TextAlignmentOptions.TopLeft,  0f, 1f, new Vector2(300f, headY), new Vector2(90f, 24f), false);
        Text("HDate", panel, "DATE",  17f, FontStyles.Bold, TextMuted, TextAlignmentOptions.TopRight, 1f, 1f, new Vector2(-46f, headY), new Vector2(160f, 24f), false);

        // rows
        const float rowH = 52f;
        for (int i = 0; i < PersonalLeaderboard.MaxEntries; i++)
        {
            var bg = Rounded("Row" + (i + 1), panel, Color.clear);
            Anchor(bg, 0f, 1f); bg.anchoredPosition = new Vector2(24f, -190f - i * (rowH + 4f)); bg.sizeDelta = new Vector2(w - 48f, rowH);
            var row = new BoardRow
            {
                bg    = bg.GetComponent<Image>(),
                place = Text("Place", bg, "", 22f, FontStyles.Bold,   TextMuted, TextAlignmentOptions.MidlineLeft,  0f, 0.5f, new Vector2(22f, 0f),  new Vector2(50f, rowH), false),
                time  = Text("Time",  bg, "", 26f, FontStyles.Bold,   TextMain,  TextAlignmentOptions.MidlineLeft,  0f, 0.5f, new Vector2(86f, 0f),  new Vector2(170f, rowH), true),
                grade = Text("Grade", bg, "", 24f, FontStyles.Bold,   TextMain,  TextAlignmentOptions.MidlineLeft,  0f, 0.5f, new Vector2(286f, 0f), new Vector2(60f, rowH), true),
                tag   = Text("Tag",   bg, "", 16f, FontStyles.Bold,   Gold,      TextAlignmentOptions.MidlineLeft,  0f, 0.5f, new Vector2(340f, 0f), new Vector2(70f, rowH), false),
                date  = Text("Date",  bg, "", 20f, FontStyles.Normal, TextMuted, TextAlignmentOptions.MidlineRight, 1f, 0.5f, new Vector2(-22f, 0f), new Vector2(160f, rowH), false),
            };
            _rows.Add(row);
        }

        _boardEmpty = Text("Empty", panel, "", 24f, FontStyles.Normal, TextMuted, TextAlignmentOptions.Center,
                           0.5f, 1f, new Vector2(0f, -330f), new Vector2(w - 80f, 120f), false);
        _boardFooter = Text("Footer", panel, "", 20f, FontStyles.Normal, TextMuted, TextAlignmentOptions.BottomLeft,
                            0f, 0f, new Vector2(34f, 24f), new Vector2(w - 68f, 28f), false);
    }

    private void SelectTab(int index)
    {
        if (_tabs.Count == 0) return;
        _tab = (index % _tabs.Count + _tabs.Count) % _tabs.Count;
        RefreshBoard();
    }

    private void RefreshBoard()
    {
        for (int i = 0; i < _tabImages.Count; i++)
        {
            bool on = i == _tab;
            _tabImages[i].color = on ? new Color(_accent.r, _accent.g, _accent.b, 0.9f) : CardFill;
            _tabLabels[i].color = on ? new Color(0.06f, 0.06f, 0.09f) : TextMuted;
        }

        string key = _tabs[_tab];
        LeaderboardBoard board = PersonalLeaderboard.Get(key);
        bool thisRunsBoard = key == _difficulty && !_preview;

        for (int i = 0; i < _rows.Count; i++)
        {
            BoardRow r = _rows[i];
            bool has = i < board.entries.Count;
            r.bg.gameObject.SetActive(has);
            if (!has) continue;

            LeaderboardEntry e = board.entries[i];
            bool mine = thisRunsBoard && i == _boardIndex && _escaped;
            r.bg.color = mine ? new Color(_accent.r, _accent.g, _accent.b, 0.22f) : (i % 2 == 0 ? CardFill : Color.clear);
            r.place.text = (i + 1).ToString();
            r.place.color = i == 0 ? Gold : TextMuted;
            r.time.text = RunResult.FormatTime(e.time);
            r.time.color = mine ? _accent : TextMain;
            r.grade.text = string.IsNullOrEmpty(e.grade) ? "-" : e.grade;
            r.grade.color = GradeColor(e.grade);
            r.tag.text = mine ? "NEW" : "";
            r.date.text = e.dateTicks > 0
                ? new DateTime(e.dateTicks).ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
                : "";
        }

        bool empty = board.entries.Count == 0;
        _boardEmpty.gameObject.SetActive(empty);
        _boardEmpty.text = $"No escapes on {key} yet.\nFind the exit to set your first time.";

        _boardFooter.text = board.attempts > 0
            ? $"Escapes  <b>{board.escapes}</b> / {board.attempts} attempts"
            : "No attempts yet";
    }

    // ---------- buttons ----------

    private void BuildButtons()
    {
        var specs = new List<(string label, string key, string scene, bool primary)>
        {
            ("RETRY",      "R",   _retrySceneName,      true),
            ("DIFFICULTY", "D",   _difficultySceneName, false),
            ("MAIN MENU",  "ESC", _mainMenuSceneName,   false),
        };

        float x = 110f;
        foreach (var s in specs)
        {
            if (string.IsNullOrEmpty(s.scene) || !Application.CanStreamedLevelBeLoaded(s.scene))
            {
                Debug.Log($"EndScreen: '{s.label}' button hidden, scene '{s.scene}' is not in Build Settings.");
                continue;
            }

            string scene = s.scene;
            Action go = () => Leave(scene);
            if (s.key == "R") _retry = go;
            else if (s.key == "D") _changeDifficulty = go;
            else _mainMenu = go;

            float w = s.primary ? 300f : 280f;
            var btnGO = new GameObject("Button " + s.label, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(_root, false);
            var rt = (RectTransform)btnGO.transform;
            Anchor(rt, 0f, 0f); rt.anchoredPosition = new Vector2(x, 70f); rt.sizeDelta = new Vector2(w, 72f);
            var img = btnGO.GetComponent<Image>();
            img.sprite = _rounded; img.type = Image.Type.Sliced;
            img.color = s.primary ? _accent : new Color(1f, 1f, 1f, 0.10f);
            var btn = btnGO.GetComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.onClick.AddListener(() => go());

            Color fg = s.primary ? new Color(0.06f, 0.06f, 0.09f) : TextMain;
            var keyChip = Rounded("Key", rt, s.primary ? new Color(0f, 0f, 0f, 0.22f) : new Color(1f, 1f, 1f, 0.14f));
            Anchor(keyChip, 0f, 0.5f); keyChip.anchoredPosition = new Vector2(16f, 0f); keyChip.sizeDelta = new Vector2(s.key.Length > 1 ? 64f : 44f, 40f);
            Text("K", keyChip, s.key, 18f, FontStyles.Bold, fg, TextAlignmentOptions.Center, 0.5f, 0.5f, Vector2.zero, keyChip.sizeDelta, false);
            float labelX = 16f + keyChip.sizeDelta.x + 14f;
            Text("Label", rt, s.label, 24f, FontStyles.Bold, fg, TextAlignmentOptions.MidlineLeft,
                 0f, 0.5f, new Vector2(labelX, 0f), new Vector2(w - labelX - 10f, 72f), true).characterSpacing = 6f;

            AddReveal(rt, 0.90f);
            x += w + 18f;
        }
    }

    private void Leave(string scene)
    {
        if (_leaving) return;
        _leaving = true;
        SceneManager.LoadScene(scene);
    }

    // =====================================================================
    // Reveal animation (any key / click skips to the end)
    // =====================================================================

    private void AddReveal(RectTransform rt, float delay)
    {
        var g = rt.gameObject.GetComponent<CanvasGroup>();
        if (g == null) g = rt.gameObject.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        _reveals.Add(new RevealItem { group = g, rt = rt, basePos = rt.anchoredPosition, delay = delay });
    }

    private IEnumerator Reveal()
    {
        const float slide = 28f;
        const float fade = 0.35f;
        const float heroStart = 0.45f;
        const float heroDuration = 1.1f;
        const float gradeStart = 0.55f;
        float end = 1.2f + heroDuration;

        float wait = 0f;
        while (wait < _revealDelay && !_skip)
        {
            wait += Time.unscaledDeltaTime;
            CheckSkip();
            yield return null;
        }

        float t = 0f;
        while (t < end)
        {
            CheckSkip();
            if (_skip) t = end;

            foreach (RevealItem r in _reveals)
            {
                float p = Ease(Mathf.Clamp01((t - r.delay) / fade));
                r.group.alpha = p;
                r.rt.anchoredPosition = r.basePos + new Vector2(0f, -slide * (1f - p));
            }

            if (_escaped)
            {
                float h = Mathf.Clamp01((t - heroStart) / heroDuration);
                _heroValue.text = RunResult.FormatTime(_official * Ease(h));
            }

            float gp = Mathf.Clamp01((t - gradeStart) / 0.28f);
            float scale = gp <= 0f ? 0f : Mathf.Lerp(2.4f, 1f, Ease(gp)); // "stamp" from big to normal
            _gradeBox.localScale = Vector3.one * scale;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        _interactive = true;

        // idle: gentle pulse on a new record
        while (_pbBadge != null)
        {
            _pbBadge.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.unscaledTime * 4f));
            yield return null;
        }
    }

    private void CheckSkip()
    {
        if (_skip) return;
        bool key = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (key || click) _skip = true;
    }

    private static float Ease(float p) => 1f - Mathf.Pow(1f - p, 3f);

    private static Color GradeColor(string grade)
    {
        switch (grade)
        {
            case "S": return Gold;
            case "A": return new Color(0.45f, 0.92f, 0.60f);
            case "B": return new Color(0.45f, 0.75f, 1f);
            case "C": return new Color(1f, 0.62f, 0.30f);
            case "D": return new Color(1f, 0.38f, 0.38f);
            default:  return TextMuted;
        }
    }

    // =====================================================================
    // Tiny UI + texture helpers
    // =====================================================================

    private RectTransform Box(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    private RectTransform Rounded(string name, Transform parent, Color color)
    {
        RectTransform rt = Box(name, parent, color);
        var img = rt.GetComponent<Image>();
        img.sprite = _rounded;
        img.type = Image.Type.Sliced;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(x, y);
        rt.pivot = new Vector2(x, y);
    }

    private TMP_Text Text(string name, Transform parent, string text, float size, FontStyles style, Color color,
                          TextAlignmentOptions align, float ax, float ay, Vector2 pos, Vector2 sizeDelta, bool headline)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        Anchor(rt, ax, ay);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var t = go.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = headline ? (_headlineFont != null ? _headlineFont : _bodyFont) : _bodyFont;
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.richText = true;
        return t;
    }

    private static Sprite MakeRoundedSprite(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        float r = radius;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private static Texture2D MakeHorizontalShade()
    {
        const int w = 256;
        var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < w; x++)
        {
            float u = x / (w - 1f);
            // dark under the results column, lighter in the middle, dark again under the board
            float a = u < 0.55f ? Mathf.Lerp(0.88f, 0.45f, u / 0.55f) : Mathf.Lerp(0.45f, 0.80f, (u - 0.55f) / 0.45f);
            tex.SetPixel(x, 0, new Color(0.02f, 0.02f, 0.05f, a));
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeScanlineTexture()
    {
        var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;
        tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.35f));
        tex.SetPixel(0, 1, new Color(0f, 0f, 0f, 0f));
        tex.SetPixel(0, 2, new Color(0f, 0f, 0f, 0f));
        tex.SetPixel(0, 3, new Color(0f, 0f, 0f, 0.12f));
        tex.Apply();
        return tex;
    }
}
