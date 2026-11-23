using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// "Hurt in a shooter" screen effect that builds up as TimerManager runs out:
/// the edges of the screen blur (radial blur, centre stays sharp) and everything
/// turns progressively red, with a red glow at the edges that pulses like a heartbeat.
/// It reaches full strength exactly when the timer hits 0.
/// The same effect also builds up when the Sense-making bar is low (by default from 50%,
/// full at 10%) and clears again as the bar recovers. Timer and bar don't stack: the stronger wins.
///
/// SETUP (once):
///   1. Add this component to any GameObject in the maze scene (e.g. the Main Camera).
///   2. Select the URP Renderer(s) your game uses (Assets/Settings/PC_Renderer and Mobile_Renderer),
///      click "Add Renderer Feature" > "Time Running Out Feature".
///
/// If step 2 is missing (or on the wrong renderer), the Console says so and a simpler
/// fallback (red tint + red vignette + light blur via URP post-processing) is used instead.
/// Post-processing is switched on automatically for the camera(s) actually rendering the game.
///
/// Screen Space - Overlay UI (timer text, manual, puzzles) is drawn on top and stays sharp.
/// Tick "Override Intensity" in Play mode to preview any strength without waiting.
/// </summary>
public class TimeRunningOutEffect : MonoBehaviour
{
    public enum Progression
    {
        Smooth, // grows continuously with the timer
        Steps   // jumps up in N stages (each jump eased in over a short time)
    }

    [Header("Timing")]
    [Tooltip("The effect begins when this fraction of the total time is left. 1 = from the very start of the timer, 0.5 = last half.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float _startAtFractionRemaining = 1f;

    [SerializeField] private Progression _progression = Progression.Smooth;

    [Tooltip("Smooth mode: maps progress (0 = effect starts, 1 = time is up) to strength. " +
             "The default is noticeable early, then ramps up hard at the end.")]
    [SerializeField] private AnimationCurve _smoothCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.6f),
        new Keyframe(0.6f, 0.4f, 0.9f, 0.9f),
        new Keyframe(1f, 1f, 2.2f, 0f));

    [Tooltip("Steps mode: number of stages. Stage k is reached at k/N of the way, the last one exactly at 0:00.")]
    [Range(2, 12)]
    [SerializeField] private int _stepCount = 5;

    [Tooltip("Seconds for the effect to ease into a new level (both modes). Keeps step jumps from popping.")]
    [SerializeField] private float _blendTime = 1.0f;

    [Header("Sense-making bar")]
    [Tooltip("Also blur / redden the screen when the Sense-making bar (AuditScoreManager) is low. " +
             "Timer and bar are not added together: whichever is stronger wins.")]
    [SerializeField] private bool _useSenseMakingBar = true;
    [Tooltip("The bar effect begins when the bar drops below this value (0.5 = 50%).")]
    [Range(0f, 1f)]
    [SerializeField] private float _barEffectStartsBelow = 0.5f;
    [Tooltip("The bar effect is at full strength at or below this value (0.1 = 10%).")]
    [Range(0f, 1f)]
    [SerializeField] private float _barFullStrengthAt = 0.1f;
    [Tooltip("Maps bar weakness (0 = at the start threshold, 1 = at the full-strength level) to effect strength.")]
    [SerializeField] private AnimationCurve _barCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Strength cap for the bar effect, in case full strength makes the maze unplayable. 1 = same as the timer at 0:00.")]
    [Range(0f, 1f)]
    [SerializeField] private float _barMaxStrength = 1f;

    [Header("Blur (at full strength)")]
    [Tooltip("How far the edges smear towards the centre. 0.1 = subtle, 0.25 = heavy.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _maxBlur = 0.22f;
    [Tooltip("Radius of the sharp area in the middle of the screen when the effect starts / at the end (0.5 ≈ screen height).")]
    [SerializeField] private float _clearRadiusStart = 0.55f;
    [SerializeField] private float _clearRadiusEnd = 0.12f;
    [Tooltip("How wide the transition from sharp to blurred is.")]
    [SerializeField] private float _blurFalloff = 0.35f;

    [Header("Red (at full strength)")]
    [SerializeField] private Color _tintColor = new Color(0.85f, 0.05f, 0.05f, 1f);
    [Tooltip("Red grade over the whole screen.")]
    [Range(0f, 1f)]
    [SerializeField] private float _maxTint = 0.4f;
    [Tooltip("Red glow at the screen edges.")]
    [Range(0f, 1.5f)]
    [SerializeField] private float _maxEdgeRed = 0.9f;
    [Range(0f, 1f)]
    [SerializeField] private float _maxDesaturate = 0.35f;
    [Range(0f, 1f)]
    [SerializeField] private float _maxEdgeDarken = 0.35f;

    [Header("Heartbeat pulse (edge glow)")]
    [Range(0f, 1f)]
    [SerializeField] private float _pulseAmount = 0.35f;
    [SerializeField] private float _pulseBpmStart = 60f;
    [SerializeField] private float _pulseBpmEnd = 140f;

    [Header("Debug")]
    [SerializeField] private bool _overrideIntensity = false;
    [Range(0f, 1f)]
    [SerializeField] private float _debugIntensity = 0.5f;
    [Tooltip("Live status, updated every second in Play mode. Read this if nothing shows on screen.")]
    [TextArea(3, 8)]
    [SerializeField] private string _status = "(enter Play mode)";

    // --- Fallback (used only if the renderer feature isn't running) ---
    private Volume _fallbackVolume;
    private ColorAdjustments _fbColor;
    private Vignette _fbVignette;
    private DepthOfField _fbDof;
    private bool _usingFallback;
    private float _activeSince = -1f;
    private float _nextStatusTime;
    private bool _warnedNoTimer;

    /// <summary>True while the effect is visible; the renderer feature skips its pass otherwise.</summary>
    public static bool IsActive { get; private set; }

    /// <summary>Current strength 0..1 (e.g. to drive a heartbeat sound volume).</summary>
    public float Intensity => _current;

    private float _current;
    private float _velocity;
    private float _beatPhase;

    private static readonly int IntensityId  = Shader.PropertyToID("_TRO_Intensity");
    private static readonly int BlurId       = Shader.PropertyToID("_TRO_BlurStrength");
    private static readonly int ClearId      = Shader.PropertyToID("_TRO_ClearRadius");
    private static readonly int FalloffId    = Shader.PropertyToID("_TRO_Falloff");
    private static readonly int TintColorId  = Shader.PropertyToID("_TRO_TintColor");
    private static readonly int TintId       = Shader.PropertyToID("_TRO_TintAmount");
    private static readonly int EdgeRedId    = Shader.PropertyToID("_TRO_EdgeRed");
    private static readonly int DesatId      = Shader.PropertyToID("_TRO_Desaturate");
    private static readonly int DarkenId     = Shader.PropertyToID("_TRO_EdgeDarken");

    private void Update()
    {
        float target = _overrideIntensity ? _debugIntensity : ComputeTarget();

        // Ease towards the target; snap to full once time is up so the apex is exact.
        _current = Mathf.SmoothDamp(_current, target, ref _velocity, Mathf.Max(0.01f, _blendTime * 0.5f));
        if (target >= 1f && _current > 0.98f) _current = 1f;
        if (target <= 0f && _current < 0.002f) _current = 0f;

        Apply(_current);
        CheckRendererFeature();
        ApplyFallback(_usingFallback ? _current : 0f);

        if (Time.unscaledTime >= _nextStatusTime)
        {
            _nextStatusTime = Time.unscaledTime + 1f;
            EnsureCamerasHavePostProcessing();
            UpdateStatus(target);
        }
    }

    // ------------------------------------------------------------------ diagnostics

    private void CheckRendererFeature()
    {
        if (!IsActive) { _activeSince = -1f; return; }
        if (_activeSince < 0f) _activeSince = Time.unscaledTime;
        if (_usingFallback) return;

        // Give the feature a moment, then check it actually drew recently.
        bool featureDrawing = Time.frameCount - TimeRunningOutFeature.LastDrawnFrame < 30;
        if (!featureDrawing && Time.unscaledTime - _activeSince > 1f)
        {
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            string reason = !string.IsNullOrEmpty(TimeRunningOutFeature.LastSkipReason)
                ? $"It is installed but skipped drawing: {TimeRunningOutFeature.LastSkipReason}."
                : $"It is not on the renderer in use. The active URP asset is '{(rp != null ? rp.name : "none")}' — " +
                  "select it, look at its Renderer List, open that renderer and use Add Renderer Feature > Time Running Out Feature.";
            Debug.LogError("TimeRunningOutEffect: the 'Time Running Out Feature' renderer feature isn't drawing, so the radial blur " +
                           "can't show. " + reason + " Using the simpler post-processing fallback for now.", this);
            _usingFallback = true;
        }
    }

    private void EnsureCamerasHavePostProcessing()
    {
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam.cameraType != CameraType.Game || cam.targetTexture != null) continue;
            if (cam.TryGetComponent(out UniversalAdditionalCameraData data) && !data.renderPostProcessing)
            {
                data.renderPostProcessing = true;
                Debug.LogWarning($"TimeRunningOutEffect: turned on Post Processing for camera '{cam.name}' — it was off, " +
                                 "which hides every screen effect. Tick it on that camera (or its prefab) to make this permanent.", cam);
            }
        }
    }

    private void UpdateStatus(float target)
    {
        TimerManager timer = TimerManager.Instance;
        string timerLine = timer == null ? "TimerManager: NOT FOUND in scene"
            : !timer.HasStarted ? "TimerManager: found, not started yet"
            : $"TimerManager: {timer.TimeRemaining:0.0}s left of {timer.TotalTime:0}s";

        if (timer != null && !timer.HasStarted && Time.timeSinceLevelLoad > 5f && !_warnedNoTimer)
        {
            _warnedNoTimer = true;
            Debug.LogWarning("TimeRunningOutEffect: TimerManager.StartTimer() hasn't been called after 5 s, so the effect stays off.", this);
        }

        string renderLine = !IsActive ? "Renderer: idle (effect strength is 0)"
            : _usingFallback ? "Renderer: FALLBACK (feature missing — see Console error)"
            : Time.frameCount - TimeRunningOutFeature.LastDrawnFrame < 30 ? "Renderer: feature drawing OK"
            : "Renderer: waiting for feature...";

        RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
        AuditScoreManager audit = AuditScoreManager.Instance;
        string barLine = audit == null ? "Sense-making bar: not in scene"
            : $"Sense-making bar: {audit.Cleaned:P0} (timer wants {ComputeTimerTarget():0.00}, bar wants {ComputeBarTarget():0.00})";
        _status = $"{timerLine}\n{barLine}\nStrength: {_current:0.00} (target {target:0.00})\n{renderLine}\n" +
                  $"Active URP asset: {(rp != null ? rp.name : "none")}";
    }

    // ------------------------------------------------------------------ fallback

    private void ApplyFallback(float k)
    {
        if (k <= 0f)
        {
            if (_fallbackVolume != null) _fallbackVolume.enabled = false;
            return;
        }

        if (_fallbackVolume == null) BuildFallbackVolume();
        _fallbackVolume.enabled = true;

        float pulse = 1f + _pulseAmount * (HeartbeatShape(_beatPhase) - 0.3f); // _beatPhase advanced in Apply()

        Color filter = Color.Lerp(Color.white, Color.Lerp(Color.white, _tintColor, 0.75f), k * _maxTint / 0.4f);
        _fbColor.colorFilter.Override(filter);
        _fbColor.saturation.Override(-100f * _maxDesaturate * k);
        _fbVignette.intensity.Override(Mathf.Clamp01(0.55f * k * pulse * _maxEdgeRed / 0.9f));

        float start = Mathf.Lerp(20f, 0f, k);
        _fbDof.gaussianStart.Override(start);
        _fbDof.gaussianEnd.Override(start + Mathf.Lerp(15f, 2f, k));
        _fbDof.gaussianMaxRadius.Override(Mathf.Lerp(0.5f, 1.5f, k));
    }

    private void BuildFallbackVolume()
    {
        GameObject go = new GameObject("TimeRunningOut Fallback Volume");
        go.transform.SetParent(transform, false);
        _fallbackVolume = go.AddComponent<Volume>();
        _fallbackVolume.isGlobal = true;
        _fallbackVolume.priority = 100f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _fallbackVolume.sharedProfile = profile;

        _fbColor = profile.Add<ColorAdjustments>(false);
        _fbVignette = profile.Add<Vignette>(false);
        _fbVignette.color.Override(_tintColor);
        _fbVignette.smoothness.Override(0.9f);
        _fbDof = profile.Add<DepthOfField>(false);
        _fbDof.mode.Override(DepthOfFieldMode.Gaussian);
    }

    /// <summary>How strong the Sense-making bar wants the effect right now (0..1, before easing). Used by AmbientTutorial.</summary>
    public float BarTarget => ComputeBarTarget();
    /// <summary>How strong the timer wants the effect right now (0..1, before easing).</summary>
    public float TimerTarget => ComputeTimerTarget();

    /// <summary>Strength from the timer and from the Sense-making bar; the stronger one wins (no stacking).</summary>
    private float ComputeTarget() => Mathf.Max(ComputeTimerTarget(), ComputeBarTarget());

    /// <summary>0 while the bar is at or above the start threshold, rising to 1 at the full-strength level.</summary>
    private float ComputeBarTarget()
    {
        if (!_useSenseMakingBar) return 0f;
        AuditScoreManager audit = AuditScoreManager.Instance;
        if (audit == null || audit.AiSuppressed) return 0f; // no bar in this scene, or the AI is K.O.'d

        float bar = audit.Cleaned;
        if (bar >= _barEffectStartsBelow) return 0f;

        float span = Mathf.Max(0.001f, _barEffectStartsBelow - _barFullStrengthAt);
        float weakness = Mathf.Clamp01((_barEffectStartsBelow - bar) / span); // 0 -> 1 as the bar falls
        return Mathf.Clamp01(_barCurve.Evaluate(weakness)) * _barMaxStrength;
    }

    private float ComputeTimerTarget()
    {
        TimerManager timer = TimerManager.Instance;
        if (timer == null || !timer.HasStarted || timer.TotalTime <= 0f) return 0f;

        float startAt = timer.TotalTime * _startAtFractionRemaining; // seconds left when the effect begins
        if (timer.TimeRemaining >= startAt) return 0f;

        float progress = Mathf.Clamp01(1f - timer.TimeRemaining / startAt); // 0 -> 1 as time runs out

        if (_progression == Progression.Steps)
        {
            // floor: level k/N is reached at k/N of the way; the last level (1) exactly at 0:00.
            return Mathf.Floor(progress * _stepCount + 1e-4f) / _stepCount;
        }

        return Mathf.Clamp01(_smoothCurve.Evaluate(progress));
    }

    private void Apply(float k)
    {
        IsActive = k > 0f;
        Shader.SetGlobalFloat(IntensityId, k);
        if (!IsActive) return;

        float bpm = Mathf.Lerp(_pulseBpmStart, _pulseBpmEnd, k);
        _beatPhase = (_beatPhase + Time.deltaTime * bpm / 60f) % 1f;
        float pulse = 1f + _pulseAmount * (HeartbeatShape(_beatPhase) - 0.3f);

        Shader.SetGlobalFloat(BlurId, _maxBlur * k);
        Shader.SetGlobalFloat(ClearId, Mathf.Lerp(_clearRadiusStart, _clearRadiusEnd, k));
        Shader.SetGlobalFloat(FalloffId, _blurFalloff);
        Shader.SetGlobalColor(TintColorId, _tintColor);
        Shader.SetGlobalFloat(TintId, _maxTint * k);
        Shader.SetGlobalFloat(EdgeRedId, _maxEdgeRed * k * pulse);
        Shader.SetGlobalFloat(DesatId, _maxDesaturate * k);
        Shader.SetGlobalFloat(DarkenId, _maxEdgeDarken * k);
    }

    /// <summary>"Lub-dub" over one beat (t in 0..1).</summary>
    private static float HeartbeatShape(float t)
    {
        float lub = Mathf.Exp(-Mathf.Pow((t - 0.08f) / 0.06f, 2f));
        float dub = 0.6f * Mathf.Exp(-Mathf.Pow((t - 0.30f) / 0.06f, 2f));
        return lub + dub;
    }

    private void OnDisable()
    {
        // Globals survive scene loads: reset so the Game Over / menu scenes aren't blurred.
        _current = 0f;
        IsActive = false;
        Shader.SetGlobalFloat(IntensityId, 0f);
        if (_fallbackVolume != null) _fallbackVolume.enabled = false;
    }

    private void OnDestroy()
    {
        if (_fallbackVolume != null && _fallbackVolume.sharedProfile != null)
            Destroy(_fallbackVolume.sharedProfile);
    }
}

// using UnityEngine;

// /// <summary>
// /// "Hurt in a shooter" screen effect that builds up as TimerManager runs out:
// /// the edges of the screen blur (radial blur, centre stays sharp) and everything
// /// turns progressively red, with a red glow at the edges that pulses like a heartbeat.
// /// It reaches full strength exactly when the timer hits 0.
// ///
// /// SETUP (once):
// ///   1. Add this component to any GameObject in the maze scene (e.g. the Main Camera).
// ///   2. Select Assets/Settings/PC_Renderer and Mobile_Renderer, click
// ///      "Add Renderer Feature" > "Time Running Out Feature".
// ///
// /// Screen Space - Overlay UI (timer text, manual, puzzles) is drawn on top and stays sharp.
// /// Tick "Override Intensity" in Play mode to preview any strength without waiting.
// /// </summary>
// public class TimeRunningOutEffect : MonoBehaviour
// {
//     public enum Progression
//     {
//         Smooth, // grows continuously with the timer
//         Steps   // jumps up in N stages (each jump eased in over a short time)
//     }

//     [Header("Timing")]
//     [Tooltip("The effect begins when this fraction of the total time is left. 1 = from the very start of the timer, 0.5 = last half.")]
//     [Range(0.05f, 1f)]
//     [SerializeField] private float _startAtFractionRemaining = 1f;

//     [SerializeField] private Progression _progression = Progression.Smooth;

//     [Tooltip("Smooth mode: maps progress (0 = effect starts, 1 = time is up) to strength. " +
//              "The default is noticeable early, then ramps up hard at the end.")]
//     [SerializeField] private AnimationCurve _smoothCurve = new AnimationCurve(
//         new Keyframe(0f, 0f, 0f, 0.6f),
//         new Keyframe(0.6f, 0.4f, 0.9f, 0.9f),
//         new Keyframe(1f, 1f, 2.2f, 0f));

//     [Tooltip("Steps mode: number of stages. Stage k is reached at k/N of the way, the last one exactly at 0:00.")]
//     [Range(2, 12)]
//     [SerializeField] private int _stepCount = 5;

//     [Tooltip("Seconds for the effect to ease into a new level (both modes). Keeps step jumps from popping.")]
//     [SerializeField] private float _blendTime = 1.0f;

//     [Header("Blur (at full strength)")]
//     [Tooltip("How far the edges smear towards the centre. 0.1 = subtle, 0.25 = heavy.")]
//     [Range(0f, 0.5f)]
//     [SerializeField] private float _maxBlur = 0.22f;
//     [Tooltip("Radius of the sharp area in the middle of the screen when the effect starts / at the end (0.5 ≈ screen height).")]
//     [SerializeField] private float _clearRadiusStart = 0.55f;
//     [SerializeField] private float _clearRadiusEnd = 0.12f;
//     [Tooltip("How wide the transition from sharp to blurred is.")]
//     [SerializeField] private float _blurFalloff = 0.35f;

//     [Header("Red (at full strength)")]
//     [SerializeField] private Color _tintColor = new Color(0.85f, 0.05f, 0.05f, 1f);
//     [Tooltip("Red grade over the whole screen.")]
//     [Range(0f, 1f)]
//     [SerializeField] private float _maxTint = 0.4f;
//     [Tooltip("Red glow at the screen edges.")]
//     [Range(0f, 1.5f)]
//     [SerializeField] private float _maxEdgeRed = 0.9f;
//     [Range(0f, 1f)]
//     [SerializeField] private float _maxDesaturate = 0.35f;
//     [Range(0f, 1f)]
//     [SerializeField] private float _maxEdgeDarken = 0.35f;

//     [Header("Heartbeat pulse (edge glow)")]
//     [Range(0f, 1f)]
//     [SerializeField] private float _pulseAmount = 0.35f;
//     [SerializeField] private float _pulseBpmStart = 60f;
//     [SerializeField] private float _pulseBpmEnd = 140f;

//     [Header("Debug")]
//     [SerializeField] private bool _overrideIntensity = false;
//     [Range(0f, 1f)]
//     [SerializeField] private float _debugIntensity = 0.5f;

//     /// <summary>True while the effect is visible; the renderer feature skips its pass otherwise.</summary>
//     public static bool IsActive { get; private set; }

//     /// <summary>Current strength 0..1 (e.g. to drive a heartbeat sound volume).</summary>
//     public float Intensity => _current;

//     private float _current;
//     private float _velocity;
//     private float _beatPhase;

//     private static readonly int IntensityId  = Shader.PropertyToID("_TRO_Intensity");
//     private static readonly int BlurId       = Shader.PropertyToID("_TRO_BlurStrength");
//     private static readonly int ClearId      = Shader.PropertyToID("_TRO_ClearRadius");
//     private static readonly int FalloffId    = Shader.PropertyToID("_TRO_Falloff");
//     private static readonly int TintColorId  = Shader.PropertyToID("_TRO_TintColor");
//     private static readonly int TintId       = Shader.PropertyToID("_TRO_TintAmount");
//     private static readonly int EdgeRedId    = Shader.PropertyToID("_TRO_EdgeRed");
//     private static readonly int DesatId      = Shader.PropertyToID("_TRO_Desaturate");
//     private static readonly int DarkenId     = Shader.PropertyToID("_TRO_EdgeDarken");

//     private void Update()
//     {
//         float target = _overrideIntensity ? _debugIntensity : ComputeTarget();

//         // Ease towards the target; snap to full once time is up so the apex is exact.
//         _current = Mathf.SmoothDamp(_current, target, ref _velocity, Mathf.Max(0.01f, _blendTime * 0.5f));
//         if (target >= 1f && _current > 0.98f) _current = 1f;
//         if (target <= 0f && _current < 0.002f) _current = 0f;

//         Apply(_current);
//     }

//     private float ComputeTarget()
//     {
//         TimerManager timer = TimerManager.Instance;
//         if (timer == null || !timer.HasStarted || timer.TotalTime <= 0f) return 0f;

//         float startAt = timer.TotalTime * _startAtFractionRemaining; // seconds left when the effect begins
//         if (timer.TimeRemaining >= startAt) return 0f;

//         float progress = Mathf.Clamp01(1f - timer.TimeRemaining / startAt); // 0 -> 1 as time runs out

//         if (_progression == Progression.Steps)
//         {
//             // floor: level k/N is reached at k/N of the way; the last level (1) exactly at 0:00.
//             return Mathf.Floor(progress * _stepCount + 1e-4f) / _stepCount;
//         }

//         return Mathf.Clamp01(_smoothCurve.Evaluate(progress));
//     }

//     private void Apply(float k)
//     {
//         IsActive = k > 0f;
//         Shader.SetGlobalFloat(IntensityId, k);
//         if (!IsActive) return;

//         float bpm = Mathf.Lerp(_pulseBpmStart, _pulseBpmEnd, k);
//         _beatPhase = (_beatPhase + Time.deltaTime * bpm / 60f) % 1f;
//         float pulse = 1f + _pulseAmount * (HeartbeatShape(_beatPhase) - 0.3f);

//         Shader.SetGlobalFloat(BlurId, _maxBlur * k);
//         Shader.SetGlobalFloat(ClearId, Mathf.Lerp(_clearRadiusStart, _clearRadiusEnd, k));
//         Shader.SetGlobalFloat(FalloffId, _blurFalloff);
//         Shader.SetGlobalColor(TintColorId, _tintColor);
//         Shader.SetGlobalFloat(TintId, _maxTint * k);
//         Shader.SetGlobalFloat(EdgeRedId, _maxEdgeRed * k * pulse);
//         Shader.SetGlobalFloat(DesatId, _maxDesaturate * k);
//         Shader.SetGlobalFloat(DarkenId, _maxEdgeDarken * k);
//     }

//     /// <summary>"Lub-dub" over one beat (t in 0..1).</summary>
//     private static float HeartbeatShape(float t)
//     {
//         float lub = Mathf.Exp(-Mathf.Pow((t - 0.08f) / 0.06f, 2f));
//         float dub = 0.6f * Mathf.Exp(-Mathf.Pow((t - 0.30f) / 0.06f, 2f));
//         return lub + dub;
//     }

//     private void OnDisable()
//     {
//         // Globals survive scene loads: reset so the Game Over / menu scenes aren't blurred.
//         _current = 0f;
//         IsActive = false;
//         Shader.SetGlobalFloat(IntensityId, 0f);
//     }
// }

// using UnityEngine;
// using UnityEngine.Rendering;
// using UnityEngine.Rendering.Universal;

// /// <summary>
// /// "Time is running out" screen effect, driven by TimerManager.
// ///
// /// As the timer approaches zero:
// ///   - vision gets blurry (URP Depth of Field, Gaussian) — the blur creeps in from the
// ///     far end of the corridor towards the player until almost everything is out of focus;
// ///   - a red glow closes in from the screen edges (URP Vignette), pulsing like a heartbeat
// ///     that speeds up near the end.
// ///
// /// Setup: add this component to any GameObject in the maze scene (the Main Camera,
// /// next to TimerManager, is a good place). No Volume or profile asset needed — it builds
// /// its own high-priority global Volume at runtime, so the scene's Global Volume is untouched.
// /// The camera must have "Post Processing" ticked (MazeTest's Main Camera already does).
// ///
// /// Screen Space - Overlay canvases (timer text, manual, puzzle UI) are drawn after
// /// post-processing, so they stay sharp and readable.
// /// </summary>
// public class TimeRunningOutEffect : MonoBehaviour
// {
//     [Header("When it starts")]
//     [Tooltip("The effect begins when this fraction of the total time is left (0.5 = the last half).")]
//     [Range(0.05f, 1f)]
//     [SerializeField] private float _startAtFractionRemaining = 0.5f;

//     [Tooltip("Maps progress (0 = effect just started, 1 = time is up) to effect strength. " +
//              "The default eases in slowly, then ramps up hard at the end.")]
//     [SerializeField] private AnimationCurve _intensityCurve = new AnimationCurve(
//         new Keyframe(0f, 0f, 0f, 0.3f),
//         new Keyframe(0.7f, 0.45f, 1.2f, 1.2f),
//         new Keyframe(1f, 1f, 2.5f, 0f));

//     [Tooltip("How quickly the effect follows the timer (higher = snappier). Stops it popping on the first frame.")]
//     [SerializeField] private float _smoothing = 3f;

//     [Header("Blur (Depth of Field)")]
//     [Tooltip("Distance (m) where the blur starts when the effect first kicks in.")]
//     [SerializeField] private float _blurStartFar = 25f;
//     [Tooltip("Distance (m) where the blur starts at the very end. 0 = right in front of the player.")]
//     [SerializeField] private float _blurStartNear = 0f;
//     [Tooltip("Distance (m) over which the blur fades from sharp to full blur, at the start / at the end.")]
//     [SerializeField] private float _blurFalloffFar = 20f;
//     [SerializeField] private float _blurFalloffNear = 1.5f;
//     [Tooltip("Blur radius at the end. URP clamps this between 0.5 and 1.5.")]
//     [Range(0.5f, 1.5f)]
//     [SerializeField] private float _maxBlurRadius = 1.5f;
//     [Tooltip("Better-looking blur, slightly more expensive. Leave off for Chromebooks.")]
//     [SerializeField] private bool _highQualityBlur = false;

//     [Header("Red glow (Vignette)")]
//     [SerializeField] private Color _glowColor = new Color(0.75f, 0f, 0f, 1f);
//     [Tooltip("Vignette intensity at the very end (before the pulse).")]
//     [Range(0f, 1f)]
//     [SerializeField] private float _maxGlowIntensity = 0.5f;
//     [Range(0.01f, 1f)]
//     [SerializeField] private float _glowSmoothness = 0.85f;

//     [Header("Heartbeat pulse")]
//     [Tooltip("Extra vignette intensity added at the peak of each beat (scaled by effect strength).")]
//     [Range(0f, 0.5f)]
//     [SerializeField] private float _pulseAmount = 0.12f;
//     [Tooltip("Beats per minute when the effect starts, and at the very end.")]
//     [SerializeField] private float _pulseBpmStart = 60f;
//     [SerializeField] private float _pulseBpmEnd = 150f;

//     [Header("Debug")]
//     [Tooltip("Tick to preview the effect in Play mode without waiting for the timer.")]
//     [SerializeField] private bool _overrideIntensity = false;
//     [Range(0f, 1f)]
//     [SerializeField] private float _debugIntensity = 0.5f;

//     private Volume _volume;
//     private VolumeProfile _profile;
//     private DepthOfField _dof;
//     private Vignette _vignette;

//     private float _currentIntensity;
//     private float _beatPhase; // accumulated so BPM changes don't make the pulse jump

//     /// <summary>Current effect strength, 0..1. Other scripts (audio, UI) can read this.</summary>
//     public float Intensity => _currentIntensity;

//     private void Awake()
//     {
//         BuildVolume();
//     }

//     private void Start()
//     {
//         WarnIfPostProcessingOff();
//     }

//     private void BuildVolume()
//     {
//         GameObject go = new GameObject("TimeRunningOut Volume");
//         go.transform.SetParent(transform, false);

//         _volume = go.AddComponent<Volume>();
//         _volume.isGlobal = true;
//         _volume.priority = 100f; // above the scene's Global Volume
//         _volume.weight = 1f;

//         // Runtime-only profile: never touches your assets on disk.
//         _profile = ScriptableObject.CreateInstance<VolumeProfile>();
//         _profile.name = "TimeRunningOut (runtime)";
//         _volume.sharedProfile = _profile;

//         _dof = _profile.Add<DepthOfField>(false);
//         _dof.mode.Override(DepthOfFieldMode.Gaussian);
//         _dof.highQualitySampling.Override(_highQualityBlur);

//         _vignette = _profile.Add<Vignette>(false);
//         _vignette.color.Override(_glowColor);
//         _vignette.center.Override(new Vector2(0.5f, 0.5f));
//         _vignette.smoothness.Override(_glowSmoothness);
//         _vignette.rounded.Override(false);

//         Apply(0f);
//     }

//     private void Update()
//     {
//         float target = _overrideIntensity ? _debugIntensity : ComputeTargetIntensity();

//         // Ease towards the target instead of snapping.
//         _currentIntensity = Mathf.MoveTowards(_currentIntensity, target, _smoothing * Time.deltaTime);
//         // Once time is up, hold the full effect exactly.
//         if (target >= 1f && _currentIntensity > 0.99f) _currentIntensity = 1f;

//         Apply(_currentIntensity);
//     }

//     private float ComputeTargetIntensity()
//     {
//         TimerManager timer = TimerManager.Instance;
//         if (timer == null || !timer.HasStarted || timer.TotalTime <= 0f) return 0f;

//         float startAt = timer.TotalTime * _startAtFractionRemaining; // seconds left when effect begins
//         if (timer.TimeRemaining >= startAt) return 0f;

//         float progress = 1f - (timer.TimeRemaining / startAt); // 0 -> 1 as time runs out
//         return Mathf.Clamp01(_intensityCurve.Evaluate(Mathf.Clamp01(progress)));
//     }

//     private void Apply(float k)
//     {
//         // Turn the whole volume off when idle so the blur pass costs nothing early in the run.
//         bool active = k > 0.001f;
//         if (_volume.enabled != active) _volume.enabled = active;
//         if (!active) return;

//         // --- Blur: the "in focus" zone shrinks towards the player ---
//         float start = Mathf.Lerp(_blurStartFar, _blurStartNear, k);
//         float falloff = Mathf.Lerp(_blurFalloffFar, _blurFalloffNear, k);
//         _dof.gaussianStart.Override(start);
//         _dof.gaussianEnd.Override(start + falloff);
//         _dof.gaussianMaxRadius.Override(Mathf.Lerp(0.5f, _maxBlurRadius, k));
//         _dof.highQualitySampling.Override(_highQualityBlur);

//         // --- Red glow with a heartbeat that speeds up ---
//         float bpm = Mathf.Lerp(_pulseBpmStart, _pulseBpmEnd, k);
//         _beatPhase += Time.deltaTime * bpm / 60f;
//         float pulse = HeartbeatShape(_beatPhase % 1f) * _pulseAmount * k;

//         _vignette.color.Override(_glowColor);
//         _vignette.smoothness.Override(_glowSmoothness);
//         _vignette.intensity.Override(Mathf.Clamp01(k * _maxGlowIntensity + pulse));
//     }

//     /// <summary>
//     /// "Lub-dub" shape over one beat (t in 0..1): a strong thump, a smaller second thump, then rest.
//     /// </summary>
//     private static float HeartbeatShape(float t)
//     {
//         float lub = Mathf.Exp(-Mathf.Pow((t - 0.08f) / 0.06f, 2f));
//         float dub = 0.6f * Mathf.Exp(-Mathf.Pow((t - 0.30f) / 0.06f, 2f));
//         return lub + dub;
//     }

//     private void WarnIfPostProcessingOff()
//     {
//         Camera cam = Camera.main;
//         if (cam == null) return;
//         if (cam.TryGetComponent(out UniversalAdditionalCameraData data) && !data.renderPostProcessing)
//         {
//             Debug.LogWarning("TimeRunningOutEffect: the Main Camera has 'Post Processing' unticked, " +
//                              "so the blur and red glow won't be visible. Tick it on the camera's Rendering section.", cam);
//         }
//     }

//     private void OnDestroy()
//     {
//         if (_profile != null) Destroy(_profile);
//     }
// }