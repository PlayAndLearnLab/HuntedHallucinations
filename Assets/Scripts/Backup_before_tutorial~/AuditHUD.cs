using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Live read-out shown under the timer:
///   - "Corrupted assets purged: N"  (only ever goes up)
///   - "Build cleaned: N%" bar: a tug-of-war between the player and the AI.
///        The AI drains it over time and with timed strikes; correct flags refill it; false alarms cost it.
///   - An evil-AI eye next to the bar that reacts to strikes and to the player's results.
///
/// IMPORTANT: nothing here reads the world (items spawning, items nearby), so it can't act as a
/// hallucination detector. It only moves because of time and the player's own reports.
/// Built at runtime and created automatically by AuditScoreManager.
/// </summary>
public class AuditHUD : MonoBehaviour
{
    [Tooltip("Off = the panel stays hidden until the AI becomes active or the player files a report.")]
    [SerializeField] private bool _showFromStart = false;
    [Tooltip("Used when the timer isn't on a standard overlay canvas. Pixels from the top-left corner at 1920x1080.")]
    [SerializeField] private Vector2 _fallbackOffset = new Vector2(30f, 190f);
    [Tooltip("Gap under the timer text, as a fraction of screen height.")]
    [SerializeField] private float _gapBelowTimer = 0.14f; // unused now, kept so the saved scene value does not break

    [Tooltip("Pixels (at 1920x1080) between the end of the timer text and the panel.")]
    [SerializeField] private float _gapRightOfTimer = 30f;

    public bool ShowFromStart { get => _showFromStart; set => _showFromStart = value; }
    /// <summary>Optional custom art for the evil AI. Null = a generated red eye.</summary>
    public Sprite AiSprite { get; set; }

    private RectTransform _panel;
    private TMP_Text _purgedText;
    private TMP_Text _labelText;
    private RectTransform _fillRT;
    private Image _fillImage;
    private Image _barBg;
    private RectTransform _aiRT;
    private Image _aiImage;
    private Canvas _hudCanvas;
    private Canvas _glitchCanvas;
    private TMP_Text _header;
    private RectTransform _bubbleRT;
    private TMP_Text _bubbleText;
    private CanvasGroup _bubbleGroup;
    private float _bubbleTimer;
    private float _nextIdleLine;
    private Image _tintOverlay;
    private RectTransform _glitchRoot;
    private Image[] _glitchBars;
    private float _glitchTimer;
    private const float BubbleTime = 4f;
    private const float GlitchTime = 0.4f;

    // What the AI "says". All generic: nothing here depends on what is in the world.
    private static readonly string[] IdleLines =
    {
        "Trust me, the exit is just ahead. Probably.",
        "No need to check your notes. I'm always right.",
        "Everything in this build is verified. (It isn't.)",
        "I never make mistakes. Ask me anything.",
        "Left. Definitely left. I'm very confident.",
        "Your GDD is outdated. Believe me instead.",
    };
    private static readonly string[] StrikeLines =
    {
        "I just corrupted a bit more of your build.",
        "Oops. Another piece of your game, rewritten.",
        "Do you even remember what that asset looked like?",
        "Your build gets messier by the minute.",
    };
    private static readonly string[] CaughtLines =
    {
        "Hey! How did you spot that?!",
        "That was supposed to be convincing...",
        "Fine. You caught one.",
    };
    private static readonly string[] SlipLines =
    {
        "Ha! That one was real.",
        "You doubted the wrong thing. I love it.",
        "Careful. Not everything is a hallucination.",
    };
    private const string KoLine = "Impossible... my output was perfect...";

    private float _shownFill;
    private Color _targetColor = new Color(0.6f, 0.6f, 0.6f);
    private int _lastPurged;
    private string _lastRank;
    private float _punchTimer;
    private float _flashTimer;
    private float _strikeTimer;
    private float _gloatTimer;   // player slipped: the AI grows
    private float _koTimer;
    private const float KoTime = 1.2f;
    private float _recoilTimer;  // player was right: the AI shrinks back
    private bool _purgedWasPunched;
    private bool _labelWasFlashing;
    private int _lastPct = -1;
    private bool _lastSuppressed;

    private const float PunchTime = 0.3f;
    private const float FlashTime = 0.6f;
    private const float StrikeTime = 0.6f;
    private const float ReactTime = 0.5f;

    private static readonly Color Gray   = new Color(0.6f, 0.6f, 0.6f);
    private static readonly Color Red    = new Color(0.95f, 0.25f, 0.25f);
    private static readonly Color Orange = new Color(1f, 0.65f, 0.15f);
    private static readonly Color Lime   = new Color(0.55f, 0.9f, 0.3f);
    private static readonly Color Green  = new Color(0.2f, 1f, 0.55f);

    IEnumerator Start()
    {
        Build();

        var m = AuditScoreManager.Instance;
        if (m != null)
        {
            m.OnStatsChanged += Refresh;
            m.OnAiStrike += HandleStrike;
            m.OnPlayerResult += HandleResult;
            m.OnKnockout += HandleKnockout;
        }
        Refresh();

        yield return null;
        PlaceUnderTimer();
        SortBehindPanels();
    }

    // The controls panel and the GDD book must cover this HUD, so it sits just below whichever root canvas they live on.
    private void SortBehindPanels()
    {
        UI_script ui = FindAnyObjectByType<UI_script>();
        if (ui == null) return;

        int lowest = int.MaxValue;
        foreach (GameObject panel in new[] { ui.ControlsPanelObject, ui.BookPanelObject })
        {
            if (panel == null) continue;
            Canvas c = panel.GetComponentInParent<Canvas>(true);
            if (c == null) continue;
            lowest = Mathf.Min(lowest, c.rootCanvas.sortingOrder);
        }
        if (lowest == int.MaxValue) return;

        _hudCanvas.sortingOrder = lowest - 1;
        if (_glitchCanvas != null) _glitchCanvas.sortingOrder = lowest - 2;
    }

    void OnDestroy()
    {
        var m = AuditScoreManager.Instance;
        if (m != null)
        {
            m.OnStatsChanged -= Refresh;
            m.OnAiStrike -= HandleStrike;
            m.OnPlayerResult -= HandleResult;
            m.OnKnockout -= HandleKnockout;
        }
    }

    private void HandleKnockout() { _koTimer = KoTime; Say(KoLine); }
    private void HandleStrike()
    {
        _strikeTimer = StrikeTime;
        _glitchTimer = GlitchTime;
        ScrambleGlitch();
        Say(StrikeLines[Random.Range(0, StrikeLines.Length)]);
    }
    private void HandleResult(bool good)
    {
        if (good) { _recoilTimer = ReactTime; Say(CaughtLines[Random.Range(0, CaughtLines.Length)]); }
        else      { _gloatTimer = ReactTime;  Say(SlipLines[Random.Range(0, SlipLines.Length)]); }
    }

    private void Say(string line)
    {
        if (_bubbleText == null) return;
        _bubbleText.text = line;
        _bubbleTimer = BubbleTime;
        _nextIdleLine = Time.unscaledTime + Random.Range(30f, 50f);
    }

    private void ScrambleGlitch()
    {
        if (_glitchBars == null) return;
        float h = Screen.height, w = Screen.width;
        foreach (Image bar in _glitchBars)
        {
            var rt = bar.rectTransform;
            float bh = Random.Range(0.004f, 0.02f) * h;
            float bw = Random.Range(0.3f, 1f) * w;
            rt.sizeDelta = new Vector2(bw, bh);
            rt.anchoredPosition = new Vector2(Random.Range(0f, w - bw), Random.Range(0f, h));
            bar.color = Random.value < 0.5f ? new Color(1f, 0.1f, 0.4f, 0.35f) : new Color(0.1f, 1f, 0.9f, 0.3f);
        }
    }

    // ---------- data -> display ----------

    private void Refresh()
    {
        AuditRunStats s = AuditScoreManager.LastRun;
        if (s == null || _panel == null) return;

        if (s.FakesFlagged > _lastPurged) _punchTimer = PunchTime;
        _lastPurged = s.FakesFlagged;
        _purgedText.text = "Corrupted assets purged: " + s.FakesFlagged;

        if (s.Judgments >= AuditRunStats.MinJudgmentsForRank)
        {
            if (_lastRank != null && s.Rank != _lastRank) _flashTimer = FlashTime;
            _lastRank = s.Rank;
        }
    }

    void Update()
    {
        if (_fillRT == null) return;
        var m = AuditScoreManager.Instance;
        AuditRunStats s = AuditScoreManager.LastRun;
        if (m == null || s == null) return;

        bool active = m.AiActive;
        bool show = _showFromStart || active || s.Judgments > 0;
        if (_panel.gameObject.activeSelf != show) _panel.gameObject.SetActive(show);

        float cleaned = m.Cleaned;
        bool suppressed = m.AiSuppressed;
        _targetColor = cleaned >= 0.9f ? Green : cleaned >= 0.7f ? Lime : cleaned >= 0.5f ? Orange : Red;

        // label
        // Only touch the TMP text when the number or state actually changes (rebuilding TMP every frame is expensive).
        int pctInt = Mathf.RoundToInt(cleaned * 100f);
        if (pctInt != _lastPct || suppressed != _lastSuppressed)
        {
            _lastPct = pctInt; _lastSuppressed = suppressed;
            _labelText.text = suppressed ? "Sense-making: " + pctInt + "%  K.O.!" : "Sense-making: " + pctInt + "%";
        }

        // bar
        _shownFill = Mathf.MoveTowards(_shownFill, cleaned, Time.unscaledDeltaTime * 1.5f);
        _fillRT.anchorMax = new Vector2(Mathf.Clamp01(_shownFill), 1f);
        _fillImage.color = Color.Lerp(_fillImage.color, _targetColor, Time.unscaledDeltaTime * 6f);

        // purged counter pop
        float punch = 1f;
        if (_punchTimer > 0f)
        {
            _punchTimer -= Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(_punchTimer / PunchTime);
            punch = 1f + 0.25f * Mathf.Sin(k * Mathf.PI);
        }
        if (punch != 1f || _purgedWasPunched) _purgedText.rectTransform.localScale = Vector3.one * punch;
        _purgedWasPunched = punch != 1f;

        // rank-change flash
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_flashTimer / FlashTime);
            _labelText.color = Color.Lerp(Color.white, _targetColor, k);
            _labelText.rectTransform.localScale = Vector3.one * (1f + 0.12f * k);
        }
        else if (_labelWasFlashing)
        {
            _labelText.color = Color.white;
            _labelText.rectTransform.localScale = Vector3.one;
        }
        _labelWasFlashing = _flashTimer > 0f;

        AnimateAi(active, suppressed);
        AnimateSpeechAndGlitch(active, suppressed, cleaned);
    }

    private void AnimateSpeechAndGlitch(bool active, bool suppressed, float cleaned)
    {
        float dt = Time.unscaledDeltaTime;

        // idle talk
        if (active && !suppressed && _bubbleTimer <= 0f)
        {
            if (_nextIdleLine <= 0f) _nextIdleLine = Time.unscaledTime + Random.Range(8f, 15f);
            else if (Time.unscaledTime >= _nextIdleLine) Say(IdleLines[Random.Range(0, IdleLines.Length)]);
        }

        // speech bubble fade
        if (_bubbleTimer > 0f)
        {
            _bubbleTimer -= dt;
            _bubbleGroup.alpha = Mathf.Clamp01(_bubbleTimer / 0.5f) * Mathf.Clamp01((BubbleTime - _bubbleTimer) / 0.15f + 0.0f);
        }
        else if (_bubbleGroup.alpha != 0f) _bubbleGroup.alpha = 0f;

        // screen tint: the less clear the picture, the more the AI's noise tints the screen. Based on the bar only.
        float wantTint = active && !suppressed ? 0.11f * (1f - cleaned) : 0f;
        Color tc = _tintOverlay.color;
        float a = Mathf.MoveTowards(tc.a, wantTint, dt * 0.2f);
        if (!Mathf.Approximately(a, tc.a)) { tc.a = a; _tintOverlay.color = tc; }

        // strike glitch bars
        if (_glitchTimer > 0f)
        {
            _glitchTimer -= dt;
            if (!_glitchRoot.gameObject.activeSelf) _glitchRoot.gameObject.SetActive(true);
            if (Random.value < 0.35f) ScrambleGlitch();
        }
        else if (_glitchRoot.gameObject.activeSelf) _glitchRoot.gameObject.SetActive(false);
    }

    private void AnimateAi(bool active, bool suppressed)
    {
        float dt = Time.unscaledDeltaTime;
        Color tint = active ? new Color(1f, 0.85f, 0.85f) : new Color(0.55f, 0.45f, 0.45f);
        float scale = 1f;
        Vector2 offset = Vector2.zero;

        if (active && !suppressed)
            scale += 0.05f * Mathf.Sin(Time.unscaledTime * 3f); // menacing idle pulse

        if (suppressed) { tint = new Color(0.45f, 0.55f, 0.65f); scale = 0.8f; } // knocked out: cold, dim, small
        if (_koTimer > 0f)
        {
            _koTimer -= dt;
            float k = Mathf.Clamp01(_koTimer / KoTime);
            scale = Mathf.Lerp(0.8f, 1.5f, k * k); // big hit, then collapse
            tint = Color.Lerp(tint, Color.white, k);
            offset = Random.insideUnitCircle * 10f * k;
        }

        if (_gloatTimer > 0f)
        {
            _gloatTimer -= dt;
            float k = Mathf.Sin(Mathf.Clamp01(_gloatTimer / ReactTime) * Mathf.PI);
            scale += 0.3f * k;
            tint = Color.Lerp(tint, new Color(1f, 0.15f, 0.15f), k);
        }
        if (_recoilTimer > 0f)
        {
            _recoilTimer -= dt;
            float k = Mathf.Sin(Mathf.Clamp01(_recoilTimer / ReactTime) * Mathf.PI);
            scale -= 0.25f * k;
            tint = Color.Lerp(tint, new Color(0.3f, 1f, 0.6f), k);
        }
        if (_strikeTimer > 0f)
        {
            _strikeTimer -= dt;
            float k = Mathf.Clamp01(_strikeTimer / StrikeTime);
            offset = Random.insideUnitCircle * 6f * k;
            scale += 0.2f * k;
            tint = Color.Lerp(tint, Color.white, k * 0.6f);
            _barBg.color = Color.Lerp(new Color(1f, 1f, 1f, 0.15f), new Color(1f, 0.1f, 0.1f, 0.6f), k);
        }
        else _barBg.color = new Color(1f, 1f, 1f, 0.15f);

        _aiImage.color = tint;
        _aiRT.localScale = Vector3.one * scale;
        _aiRT.anchoredPosition = new Vector2(-14f, 0f) + offset;
    }

    // ---------- runtime UI ----------

    private void PlaceUnderTimer()
    {
        TextMeshProUGUI timerText = TimerManager.Instance != null ? TimerManager.Instance.TimerText : null;
        if (timerText != null && timerText.canvas != null &&
            timerText.canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            var corners = new Vector3[4];
            timerText.rectTransform.GetWorldCorners(corners); // 0 BL, 1 TL, 2 TR, 3 BR (screen pixels)
            float scale = timerText.canvas.scaleFactor;
            float textW = timerText.preferredWidth * scale;
            float left = corners[0].x, right = corners[2].x;
            float textRight;
            switch (timerText.alignment & (TextAlignmentOptions)0xFF)
            {
                case TextAlignmentOptions.Left:
                case TextAlignmentOptions.TopLeft:
                case TextAlignmentOptions.BottomLeft:
                case TextAlignmentOptions.BaselineLeft:
                    textRight = left + textW; break;
                case TextAlignmentOptions.Right:
                case TextAlignmentOptions.TopRight:
                case TextAlignmentOptions.BottomRight:
                case TextAlignmentOptions.BaselineRight:
                    textRight = right; break;
                default:
                    textRight = (left + right) * 0.5f + textW * 0.5f; break;
            }
            // panel pivot is its top-left; line its top up with the timer's top edge
            _panel.position = new Vector3(textRight + _gapRightOfTimer * scale, corners[1].y, 0f);
        }
        else
        {
            _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 1f);
            _panel.anchoredPosition = new Vector2(_fallbackOffset.x, -_fallbackOffset.y);
        }
    }

    private void Build()
    {
        var canvasGO = new GameObject("AuditHUDCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        _hudCanvas = canvas;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        const float iconSize = 84f;
        const float rightInset = iconSize + 28f;

        var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(canvasGO.transform, false);
        _panel = (RectTransform)panelGO.transform;
        _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 1f);
        _panel.pivot = new Vector2(0f, 1f);
        _panel.sizeDelta = new Vector2(560f, 132f);
        var bg = panelGO.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.5f);
        bg.raycastTarget = false;

        _header     = MakeText("Header", panelGO.transform, 20f, -6f, 26f, 12f, rightInset);
        _header.text = "AI  vs.  YOU";
        _header.fontStyle = FontStyles.Bold;
        _header.color = new Color(1f, 0.45f, 0.4f);
        _purgedText = MakeText("Purged", panelGO.transform, 24f, -32f, 30f, 12f, rightInset);
        _labelText  = MakeText("Cleaned", panelGO.transform, 22f, -64f, 28f, 12f, rightInset);

        var barGO = new GameObject("BarBackground", typeof(RectTransform), typeof(Image));
        barGO.transform.SetParent(panelGO.transform, false);
        var barRT = (RectTransform)barGO.transform;
        barRT.anchorMin = new Vector2(0f, 0f);
        barRT.anchorMax = new Vector2(1f, 0f);
        barRT.pivot = new Vector2(0.5f, 0f);
        barRT.offsetMin = new Vector2(12f, 12f);
        barRT.offsetMax = new Vector2(-rightInset, 26f);
        _barBg = barGO.GetComponent<Image>();
        _barBg.color = new Color(1f, 1f, 1f, 0.15f);
        _barBg.raycastTarget = false;

        var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(barGO.transform, false);
        _fillRT = (RectTransform)fillGO.transform;
        _fillRT.anchorMin = Vector2.zero;
        _fillRT.anchorMax = new Vector2(0f, 1f);
        _fillRT.offsetMin = _fillRT.offsetMax = Vector2.zero;
        _fillImage = fillGO.GetComponent<Image>();
        _fillImage.color = Gray;
        _fillImage.raycastTarget = false;

        var aiGO = new GameObject("EvilAI", typeof(RectTransform), typeof(Image));
        aiGO.transform.SetParent(panelGO.transform, false);
        _aiRT = (RectTransform)aiGO.transform;
        _aiRT.anchorMin = _aiRT.anchorMax = new Vector2(1f, 0.5f);
        _aiRT.pivot = new Vector2(1f, 0.5f);
        _aiRT.sizeDelta = new Vector2(iconSize, iconSize);
        _aiRT.anchoredPosition = new Vector2(-14f, 0f);
        _aiImage = aiGO.GetComponent<Image>();
        _aiImage.sprite = AiPortrait.Get(AiSprite);
        _aiImage.preserveAspect = true;
        _aiImage.raycastTarget = false;

        BuildBubble(panelGO.transform);
        BuildOverlays(canvasGO.transform);

        _panel.gameObject.SetActive(_showFromStart);
    }

    private void BuildBubble(Transform panel)
    {
        var go = new GameObject("AiBubble", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(panel, false);
        _bubbleRT = (RectTransform)go.transform;
        _bubbleRT.anchorMin = _bubbleRT.anchorMax = new Vector2(0f, 0f);
        _bubbleRT.pivot = new Vector2(0f, 1f);
        _bubbleRT.anchoredPosition = new Vector2(0f, -6f);
        _bubbleRT.sizeDelta = new Vector2(560f, 44f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.25f, 0.02f, 0.05f, 0.88f);
        img.raycastTarget = false;
        _bubbleGroup = go.GetComponent<CanvasGroup>();
        _bubbleGroup.alpha = 0f;
        _bubbleGroup.blocksRaycasts = false;

        _bubbleText = MakeText("Line", go.transform, 20f, -4f, 36f, 14f, 14f);
        _bubbleText.fontStyle = FontStyles.Italic;
        _bubbleText.color = new Color(1f, 0.8f, 0.8f);
        _bubbleText.alignment = TextAlignmentOptions.MidlineLeft;
    }

    // Full-screen tint + strike glitch bars, on a canvas BELOW the HUD (35) so they never cover the text.
    private void BuildOverlays(Transform hudCanvas)
    {
        var cGO = new GameObject("AuditGlitchCanvas", typeof(Canvas));
        cGO.transform.SetParent(transform, false);
        var c = cGO.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 35;
        _glitchCanvas = c;

        var tintGO = new GameObject("Tint", typeof(RectTransform), typeof(Image));
        tintGO.transform.SetParent(cGO.transform, false);
        var trt = (RectTransform)tintGO.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        _tintOverlay = tintGO.GetComponent<Image>();
        _tintOverlay.color = new Color(0.55f, 0.1f, 0.75f, 0f);
        _tintOverlay.raycastTarget = false;

        var rootGO = new GameObject("GlitchBars", typeof(RectTransform));
        rootGO.transform.SetParent(cGO.transform, false);
        _glitchRoot = (RectTransform)rootGO.transform;
        _glitchRoot.anchorMin = Vector2.zero; _glitchRoot.anchorMax = Vector2.zero;
        _glitchRoot.pivot = Vector2.zero;
        _glitchBars = new Image[6];
        for (int i = 0; i < _glitchBars.Length; i++)
        {
            var bGO = new GameObject("Bar" + i, typeof(RectTransform), typeof(Image));
            bGO.transform.SetParent(_glitchRoot, false);
            var brt = (RectTransform)bGO.transform;
            brt.anchorMin = brt.anchorMax = Vector2.zero;
            brt.pivot = Vector2.zero;
            _glitchBars[i] = bGO.GetComponent<Image>();
            _glitchBars[i].raycastTarget = false;
        }
        _glitchRoot.gameObject.SetActive(false);
    }

    // ---- generated "untrustworthy robot" face ----
    private static float RoundBox(float u, float v, float cx, float cy, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(u - cx) - (hx - r);
        float qy = Mathf.Abs(v - cy) - (hy - r);
        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r; // <0 inside
    }
    private static float Circle(float u, float v, float cx, float cy, float r)
    {
        return Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) - r;
    }
    private static float Segment(float u, float v, float ax, float ay, float bx, float by, float thick)
    {
        float pax = u - ax, pay = v - ay, bax = bx - ax, bay = by - ay;
        float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
        float dx = pax - bax * h, dy = pay - bay * h;
        return Mathf.Sqrt(dx * dx + dy * dy) - thick;
    }
    private static Color Over(Color under, Color c, float dist, float px)
    {
        float a = Mathf.Clamp01(0.5f - dist / px) * c.a;
        return new Color(Mathf.Lerp(under.r, c.r, a), Mathf.Lerp(under.g, c.g, a), Mathf.Lerp(under.b, c.b, a), Mathf.Max(under.a, a));
    }

    /// <summary>
    /// A robot head that is clearly "off": a too-wide grin full of teeth, one glowing red eye,
    /// one glitchy screen eye, a crack, and a blinking antenna. Meant to read as "do not trust".
    /// </summary>
    public static Sprite MakeEyeSprite()
    {
        const int N = 128;
        float px = 2f / N * 1.4f;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[N * N];
        var metal = new Color(0.58f, 0.64f, 0.72f, 1f);
        var dark  = new Color(0.10f, 0.11f, 0.14f, 1f);
        var red   = new Color(1f, 0.12f, 0.10f, 1f);

        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float u = (x + 0.5f) / N * 2f - 1f;
            float v = (y + 0.5f) / N * 2f - 1f;
            Color c = Color.clear;

            // antenna
            c = Over(c, dark, Segment(u, v, 0f, 0.58f, 0f, 0.82f, 0.035f), px);
            c = Over(c, red, Circle(u, v, 0f, 0.86f, 0.09f), px);
            // ears
            c = Over(c, dark, RoundBox(u, v, -0.86f, -0.05f, 0.08f, 0.22f, 0.04f), px);
            c = Over(c, dark, RoundBox(u, v, 0.86f, -0.05f, 0.08f, 0.22f, 0.04f), px);
            // head: dark outline then metal plate
            c = Over(c, dark, RoundBox(u, v, 0f, -0.05f, 0.80f, 0.64f, 0.24f), px);
            c = Over(c, metal, RoundBox(u, v, 0f, -0.05f, 0.74f, 0.58f, 0.20f), px);
            // crack across the forehead
            c = Over(c, dark, Segment(u, v, 0.10f, 0.52f, 0.30f, 0.34f, 0.012f), px);
            c = Over(c, dark, Segment(u, v, 0.30f, 0.34f, 0.22f, 0.22f, 0.012f), px);

            // left eye: glowing red lens
            c = Over(c, dark, Circle(u, v, -0.33f, 0.14f, 0.22f), px);
            c = Over(c, red, Circle(u, v, -0.33f, 0.14f, 0.15f), px);
            c = Over(c, new Color(1f, 0.85f, 0.8f, 1f), Circle(u, v, -0.37f, 0.18f, 0.045f), px);
            // right eye: glitched screen with scan-lines
            c = Over(c, dark, RoundBox(u, v, 0.33f, 0.14f, 0.23f, 0.17f, 0.04f), px);
            float scan = Mathf.Repeat(v * 14f, 1f);
            Color scr = scan < 0.55f ? new Color(1f, 0.35f, 0.15f, 1f) : new Color(0.55f, 0.08f, 0.06f, 1f);
            c = Over(c, scr, RoundBox(u, v, 0.33f, 0.14f, 0.18f, 0.12f, 0.02f), px);

            // grin: dark curved mouth with teeth, way too wide
            float curve = -0.33f + 0.22f * (u / 0.55f) * (u / 0.55f);
            float mouth = Mathf.Max(Mathf.Abs(v - curve) - 0.10f, Mathf.Abs(u) - 0.55f);
            c = Over(c, dark, mouth, px);
            float tooth = Mathf.Max(Mathf.Abs(Mathf.Repeat(u + 0.55f, 0.11f) - 0.055f) - 0.012f, mouth + 0.02f);
            c = Over(c, new Color(0.9f, 0.9f, 0.85f, 1f), tooth, px);

            pixels[y * N + x] = c;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>(old evil-eye version, unused)</summary>
    private static Sprite MakeOldEyeSprite()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float dx = (x + 0.5f) / N * 2f - 1f;
            float dy = (y + 0.5f) / N * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            Color c = Color.clear;
            if (r < 0.95f)
            {
                float edge = Mathf.Clamp01((0.95f - r) / 0.05f);
                c = new Color(0.25f, 0f, 0f, edge);                 // dark red disc
                if (r > 0.82f) c = new Color(0.9f, 0.1f, 0.1f, edge); // red rim
                else if (r < 0.7f)
                {
                    float iris = Mathf.Clamp01(1f - r / 0.7f);
                    c = Color.Lerp(new Color(1f, 0.55f, 0.05f, 1f), new Color(1f, 0.9f, 0.2f, 1f), iris);
                    float slit = (dx * dx) / (0.012f + 0.06f * (1f - Mathf.Abs(dy))) ;
                    if (slit < 1f && Mathf.Abs(dy) < 0.62f) c = new Color(0f, 0f, 0f, 1f); // slit pupil
                }
            }
            px[y * N + x] = c;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    private static TMP_Text MakeText(string name, Transform parent, float size, float top, float height, float leftInset, float rightInset)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(leftInset, top - height);
        rt.offsetMax = new Vector2(-rightInset, top);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        return tmp;
    }
}
