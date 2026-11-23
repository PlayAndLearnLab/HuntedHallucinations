using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public enum AuditFeedbackKind { Correct, Wrong, Nothing }

/// <summary>
/// Sight and sound for every F press: a coloured screen-edge pulse, a floating "+5s" / "-10s"
/// next to the timer, and a short sound. Everything is built at runtime and the sounds are
/// generated in code, so it works with zero setup. Add this component to a scene object only if
/// you want to plug in your own AudioClips.
/// </summary>
public class AuditFeedback : MonoBehaviour
{
    public static AuditFeedback Instance { get; private set; }

    [Header("Optional custom sounds (empty = generated tones)")]
    [SerializeField] private AudioClip _correctClip;
    [SerializeField] private AudioClip _wrongClip;
    [SerializeField] private AudioClip _nothingClip;
    [Range(0f, 1f)] [SerializeField] private float _volume = 0.7f;

    [Header("Colours")]
    [SerializeField] private Color _correctColor = new Color(0.25f, 1f, 0.45f, 1f);
    [SerializeField] private Color _wrongColor   = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color _nothingColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    private AudioSource _audio;
    private Image _vignette;
    private RectTransform _canvasRoot;
    private Coroutine _pulse;
    private bool _built;

    public static void Play(AuditFeedbackKind kind, string deltaText = null)
    {
        if (Instance == null)
        {
            var go = new GameObject("AuditFeedback");
            go.AddComponent<AuditFeedback>(); // Awake registers Instance
        }
        if (Instance != null) Instance.PlayInternal(kind, deltaText);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- public flow ----------

    private void PlayInternal(AuditFeedbackKind kind, string deltaText)
    {
        EnsureBuilt();

        Color color = kind == AuditFeedbackKind.Correct ? _correctColor
                    : kind == AuditFeedbackKind.Wrong   ? _wrongColor
                    : _nothingColor;

        float strength = kind == AuditFeedbackKind.Wrong ? 0.85f
                       : kind == AuditFeedbackKind.Correct ? 0.55f
                       : 0.2f;
        float duration = kind == AuditFeedbackKind.Wrong ? 0.7f : 0.45f;

        if (_pulse != null) StopCoroutine(_pulse);
        _pulse = StartCoroutine(PulseRoutine(color, strength, duration));

        if (!string.IsNullOrEmpty(deltaText))
            StartCoroutine(FloatingTextRoutine(deltaText, color));

        PlaySound(kind);
    }

    // ---------- sound ----------

    private void PlaySound(AuditFeedbackKind kind)
    {
        AudioClip clip = kind == AuditFeedbackKind.Correct ? _correctClip
                       : kind == AuditFeedbackKind.Wrong   ? _wrongClip
                       : _nothingClip;

        if (clip == null)
        {
            // Generated once, then cached in the field so the next press is free.
            if (kind == AuditFeedbackKind.Correct)
                clip = _correctClip = BuildClip("audit_correct", 0.22f, t => t < 0.09f ? 660f : 990f, false, 0.5f);
            else if (kind == AuditFeedbackKind.Wrong)
                clip = _wrongClip = BuildClip("audit_wrong", 0.38f, t => 230f - 120f * (t / 0.38f), true, 0.4f);
            else
                clip = _nothingClip = BuildClip("audit_nothing", 0.07f, t => 320f, false, 0.3f);
        }

        _audio.PlayOneShot(clip, _volume);
    }

    private static AudioClip BuildClip(string clipName, float duration, System.Func<float, float> freqAt, bool square, float gain)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(duration * rate);
        var data = new float[n];
        float phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            phase += 2f * Mathf.PI * freqAt(t) / rate;

            float s = Mathf.Sin(phase);
            if (square) s = Mathf.Sign(s) * 0.6f + s * 0.4f; // buzzier, harsher

            float attack = Mathf.Min(1f, t / 0.005f);
            float decay = Mathf.Pow(Mathf.Clamp01(1f - t / duration), 1.5f);
            data[i] = s * attack * decay * gain;
        }

        AudioClip clip = AudioClip.Create(clipName, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ---------- visuals ----------

    private IEnumerator PulseRoutine(Color color, float strength, float duration)
    {
        const float rise = 0.06f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = t < rise ? t / rise : Mathf.Pow(1f - (t - rise) / (duration - rise), 2f);
            _vignette.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(a) * strength);
            yield return null;
        }
        _vignette.color = new Color(color.r, color.g, color.b, 0f);
        _pulse = null;
    }

    private IEnumerator FloatingTextRoutine(string text, Color color)
    {
        var go = new GameObject("DeltaText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(_canvasRoot, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(400f, 100f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 56f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.raycastTarget = false;

        // Start on the timer when it lives on an overlay canvas, otherwise top-centre of the screen.
        Vector3 start = new Vector3(Screen.width * 0.5f, Screen.height * 0.88f, 0f);
        TextMeshProUGUI timerText = TimerManager.Instance != null ? TimerManager.Instance.TimerText : null;
        if (timerText != null && timerText.canvas != null &&
            timerText.canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            start = timerText.transform.position + Vector3.down * (Screen.height * 0.06f);
        }
        rt.position = start;

        float travel = Screen.height * 0.05f;
        const float life = 1.1f;
        float t = 0f;
        while (t < life)
        {
            t += Time.unscaledDeltaTime;
            float k = t / life;
            rt.position = start + Vector3.down * (travel * k);
            // pop in, hold, fade out
            float scale = k < 0.12f ? Mathf.Lerp(0.6f, 1.25f, k / 0.12f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((k - 0.12f) / 0.2f));
            rt.localScale = Vector3.one * scale;
            tmp.color = new Color(color.r, color.g, color.b, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
            yield return null;
        }
        Destroy(go);
    }

    // ---------- runtime UI ----------

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f; // 2D

        var canvasGO = new GameObject("AuditFeedbackCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _canvasRoot = (RectTransform)canvasGO.transform;

        var vgo = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
        vgo.transform.SetParent(canvasGO.transform, false);
        var vrt = (RectTransform)vgo.transform;
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        _vignette = vgo.GetComponent<Image>();
        _vignette.sprite = BuildVignetteSprite();
        _vignette.color = new Color(1f, 1f, 1f, 0f);
        _vignette.raycastTarget = false;
    }

    // Clear centre, glowing edges only: tinted by Image.color at play time.
    private static Sprite BuildVignetteSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                // Distance to the nearest SCREEN EDGE (square, not round), so the glow hugs all four sides
                // equally and the middle ~78% of the screen stays completely clear.
                float r = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));   // 0 centre .. 1 at the edge
                // Clear until 78% of the way to an edge, then a smooth ramp to full at the edge.
                // (Not Mathf.SmoothStep: that one blends two VALUES by t, it is not an edge0/edge1 ramp.)
                float t = Mathf.Clamp01((r - 0.78f) / (1.0f - 0.78f));
                float a = t * t * (3f - 2f * t);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
