using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// "Time is running out" screen effect, driven by TimerManager.
///
/// As the timer approaches zero:
///   - vision gets blurry (URP Depth of Field, Gaussian) — the blur creeps in from the
///     far end of the corridor towards the player until almost everything is out of focus;
///   - a red glow closes in from the screen edges (URP Vignette), pulsing like a heartbeat
///     that speeds up near the end.
///
/// Setup: add this component to any GameObject in the maze scene (the Main Camera,
/// next to TimerManager, is a good place). No Volume or profile asset needed — it builds
/// its own high-priority global Volume at runtime, so the scene's Global Volume is untouched.
/// The camera must have "Post Processing" ticked (MazeTest's Main Camera already does).
///
/// Screen Space - Overlay canvases (timer text, GDD book, puzzle UI) are drawn after
/// post-processing, so they stay sharp and readable.
/// </summary>
public class TimeRunningOutEffect : MonoBehaviour
{
    [Header("When it starts")]
    [Tooltip("The effect begins when this fraction of the total time is left (0.5 = the last half).")]
    [Range(0.05f, 1f)]
    [SerializeField] private float _startAtFractionRemaining = 0.5f;

    [Tooltip("Maps progress (0 = effect just started, 1 = time is up) to effect strength. " +
             "The default eases in slowly, then ramps up hard at the end.")]
    [SerializeField] private AnimationCurve _intensityCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.3f),
        new Keyframe(0.7f, 0.45f, 1.2f, 1.2f),
        new Keyframe(1f, 1f, 2.5f, 0f));

    [Tooltip("How quickly the effect follows the timer (higher = snappier). Stops it popping on the first frame.")]
    [SerializeField] private float _smoothing = 3f;

    [Header("Blur (Depth of Field)")]
    [Tooltip("Distance (m) where the blur starts when the effect first kicks in.")]
    [SerializeField] private float _blurStartFar = 25f;
    [Tooltip("Distance (m) where the blur starts at the very end. 0 = right in front of the player.")]
    [SerializeField] private float _blurStartNear = 0f;
    [Tooltip("Distance (m) over which the blur fades from sharp to full blur, at the start / at the end.")]
    [SerializeField] private float _blurFalloffFar = 20f;
    [SerializeField] private float _blurFalloffNear = 1.5f;
    [Tooltip("Blur radius at the end. URP clamps this between 0.5 and 1.5.")]
    [Range(0.5f, 1.5f)]
    [SerializeField] private float _maxBlurRadius = 1.5f;
    [Tooltip("Better-looking blur, slightly more expensive. Leave off for Chromebooks.")]
    [SerializeField] private bool _highQualityBlur = false;

    [Header("Red glow (Vignette)")]
    [SerializeField] private Color _glowColor = new Color(0.75f, 0f, 0f, 1f);
    [Tooltip("Vignette intensity at the very end (before the pulse).")]
    [Range(0f, 1f)]
    [SerializeField] private float _maxGlowIntensity = 0.5f;
    [Range(0.01f, 1f)]
    [SerializeField] private float _glowSmoothness = 0.85f;

    [Header("Heartbeat pulse")]
    [Tooltip("Extra vignette intensity added at the peak of each beat (scaled by effect strength).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _pulseAmount = 0.12f;
    [Tooltip("Beats per minute when the effect starts, and at the very end.")]
    [SerializeField] private float _pulseBpmStart = 60f;
    [SerializeField] private float _pulseBpmEnd = 150f;

    [Header("Debug")]
    [Tooltip("Tick to preview the effect in Play mode without waiting for the timer.")]
    [SerializeField] private bool _overrideIntensity = false;
    [Range(0f, 1f)]
    [SerializeField] private float _debugIntensity = 0.5f;

    private Volume _volume;
    private VolumeProfile _profile;
    private DepthOfField _dof;
    private Vignette _vignette;

    private float _currentIntensity;
    private float _beatPhase; // accumulated so BPM changes don't make the pulse jump

    /// <summary>Current effect strength, 0..1. Other scripts (audio, UI) can read this.</summary>
    public float Intensity => _currentIntensity;

    private void Awake()
    {
        BuildVolume();
    }

    private void Start()
    {
        WarnIfPostProcessingOff();
    }

    private void BuildVolume()
    {
        GameObject go = new GameObject("TimeRunningOut Volume");
        go.transform.SetParent(transform, false);

        _volume = go.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 100f; // above the scene's Global Volume
        _volume.weight = 1f;

        // Runtime-only profile: never touches your assets on disk.
        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _profile.name = "TimeRunningOut (runtime)";
        _volume.sharedProfile = _profile;

        _dof = _profile.Add<DepthOfField>(false);
        _dof.mode.Override(DepthOfFieldMode.Gaussian);
        _dof.highQualitySampling.Override(_highQualityBlur);

        _vignette = _profile.Add<Vignette>(false);
        _vignette.color.Override(_glowColor);
        _vignette.center.Override(new Vector2(0.5f, 0.5f));
        _vignette.smoothness.Override(_glowSmoothness);
        _vignette.rounded.Override(false);

        Apply(0f);
    }

    private void Update()
    {
        float target = _overrideIntensity ? _debugIntensity : ComputeTargetIntensity();

        // Ease towards the target instead of snapping.
        _currentIntensity = Mathf.MoveTowards(_currentIntensity, target, _smoothing * Time.deltaTime);
        // Once time is up, hold the full effect exactly.
        if (target >= 1f && _currentIntensity > 0.99f) _currentIntensity = 1f;

        Apply(_currentIntensity);
    }

    private float ComputeTargetIntensity()
    {
        TimerManager timer = TimerManager.Instance;
        if (timer == null || !timer.HasStarted || timer.TotalTime <= 0f) return 0f;

        float startAt = timer.TotalTime * _startAtFractionRemaining; // seconds left when effect begins
        if (timer.TimeRemaining >= startAt) return 0f;

        float progress = 1f - (timer.TimeRemaining / startAt); // 0 -> 1 as time runs out
        return Mathf.Clamp01(_intensityCurve.Evaluate(Mathf.Clamp01(progress)));
    }

    private void Apply(float k)
    {
        // Turn the whole volume off when idle so the blur pass costs nothing early in the run.
        bool active = k > 0.001f;
        if (_volume.enabled != active) _volume.enabled = active;
        if (!active) return;

        // --- Blur: the "in focus" zone shrinks towards the player ---
        float start = Mathf.Lerp(_blurStartFar, _blurStartNear, k);
        float falloff = Mathf.Lerp(_blurFalloffFar, _blurFalloffNear, k);
        _dof.gaussianStart.Override(start);
        _dof.gaussianEnd.Override(start + falloff);
        _dof.gaussianMaxRadius.Override(Mathf.Lerp(0.5f, _maxBlurRadius, k));
        _dof.highQualitySampling.Override(_highQualityBlur);

        // --- Red glow with a heartbeat that speeds up ---
        float bpm = Mathf.Lerp(_pulseBpmStart, _pulseBpmEnd, k);
        _beatPhase += Time.deltaTime * bpm / 60f;
        float pulse = HeartbeatShape(_beatPhase % 1f) * _pulseAmount * k;

        _vignette.color.Override(_glowColor);
        _vignette.smoothness.Override(_glowSmoothness);
        _vignette.intensity.Override(Mathf.Clamp01(k * _maxGlowIntensity + pulse));
    }

    /// <summary>
    /// "Lub-dub" shape over one beat (t in 0..1): a strong thump, a smaller second thump, then rest.
    /// </summary>
    private static float HeartbeatShape(float t)
    {
        float lub = Mathf.Exp(-Mathf.Pow((t - 0.08f) / 0.06f, 2f));
        float dub = 0.6f * Mathf.Exp(-Mathf.Pow((t - 0.30f) / 0.06f, 2f));
        return lub + dub;
    }

    private void WarnIfPostProcessingOff()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (cam.TryGetComponent(out UniversalAdditionalCameraData data) && !data.renderPostProcessing)
        {
            Debug.LogWarning("TimeRunningOutEffect: the Main Camera has 'Post Processing' unticked, " +
                             "so the blur and red glow won't be visible. Tick it on the camera's Rendering section.", cam);
        }
    }

    private void OnDestroy()
    {
        if (_profile != null) Destroy(_profile);
    }
}