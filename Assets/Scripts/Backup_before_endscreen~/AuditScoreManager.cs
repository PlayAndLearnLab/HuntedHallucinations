using UnityEngine;

/// <summary>
/// Per-run stats for the "ambient hallucination" mechanic. Kept in a static
/// (see AuditScoreManager.LastRun) so the Congrats / GameOver scenes can read
/// them after the maze scene has been unloaded.
/// </summary>
public class AuditRunStats
{
    public int FakesSeen;      // hallucinated items the player actually came near
    public int RealSeen;       // genuine "bait" items the player actually came near
    public int FakesFlagged;   // correct reports
    public int FalseAlarms;    // genuine items flagged by mistake

    /// <summary>How "clean" the build is, 0..1: the AI-vs-player tug-of-war bar. Drives the rank.</summary>
    public float Cleaned = 0.5f;

    /// <summary>Reports that could have been checked against a GDD page (excludes wrong-game assets and phantom prompts).</summary>
    public int ChecksPossible;
    /// <summary>...and how many of those had the right page opened first.</summary>
    public int ChecksDone;

    /// <summary>Reports needed before accuracy is shown as a rank / on the reliability bar.</summary>
    public const int MinJudgmentsForRank = 3;

    public int Judgments => FakesFlagged + FalseAlarms;

    /// <summary>Correct reports / all reports. -1 when the player never reported anything.</summary>
    public float Accuracy => Judgments == 0 ? -1f : (float)FakesFlagged / Judgments;

    public string Rank
    {
        get
        {
            if (Judgments == 0) return "No audits filed";
            if (Judgments < MinJudgmentsForRank) return "Rookie Auditor";
            float a = Cleaned; // the tug-of-war bar, not raw accuracy: it has to be held, not just reached once
            if (a >= 0.9f) return "Lead Auditor";
            if (a >= 0.7f) return "Senior QA";
            if (a >= 0.5f) return "Junior Tester";
            return "Still Learning";
        }
    }
}

/// <summary>
/// Resolves a player's "flag as hallucination" report: updates the run stats,
/// gives a time bonus / penalty through TimerManager.AdjustTime, and shows a
/// one-line message explaining what just happened.
///
/// Reporting a fake  -> time bonus, the item glitches away.
/// Reporting a real  -> time penalty, the item stays (it was genuine).
/// Ignoring anything -> no effect at all.
/// </summary>
public class AuditScoreManager : MonoBehaviour
{
    public static AuditScoreManager Instance { get; private set; }

    /// <summary>Stats of the current / most recent run. Reset when the maze scene loads.</summary>
    public static AuditRunStats LastRun { get; private set; } = new AuditRunStats();

    [Header("Time effects (seconds)")]
    [SerializeField] private float _correctFlagBonus = 5f;
    [SerializeField] private float _wrongFlagPenalty = 10f;

    [Header("Phantom prompts (the F prompt itself is not always right)")]
    [Tooltip("Pressing F while a prompt is showing but nothing is actually wrong nearby.")]
    [SerializeField] private float _phantomFlagPenalty = 5f;

    [Header("Intersection assets (the puzzle itself)")]
    [Tooltip("Smaller than the corridor bonus: spotting these is already rewarded by finding the exit.")]
    [SerializeField] private float _junctionCorrectFlagBonus = 3f;
    [Tooltip("Much bigger than the corridor penalty: flagging a genuine junction asset reveals the right exit, so it must cost more than guessing is worth.")]
    [SerializeField] private float _junctionWrongFlagPenalty = 20f;

    [Header("Messages")]
    [SerializeField] private string _correctMessage = "Confirmed: corrupted asset. +{0}s";
    [SerializeField] private string _wrongMessage   = "That one was genuine - check your GDD ({0}). -{1}s";
    [SerializeField] private string _nothingMessage = "Nothing suspicious here.";
    [SerializeField] private string _phantomMessage = "False alarm - nothing was wrong here. The prompt can be wrong too. -{0}s";

    [Header("Build cleanliness (the AI-vs-player bar)")]
    [Range(0f, 1f)] [SerializeField] private float _startCleaned = 0.5f;
    [Tooltip("Fraction of the bar the AI eats per second once it is active (0.002 = 0.2% per second).")]
    [SerializeField] private float _aiDrainPerSecond = 0.002f;
    [Tooltip("The AI also takes a visible chunk every so often. Strikes run on a timer and are unrelated to what has spawned, so they reveal nothing.")]
    [SerializeField] private float _strikeIntervalMin = 15f;
    [SerializeField] private float _strikeIntervalMax = 25f;
    [SerializeField] private float _strikeDamage = 0.03f;
    [SerializeField] private float _gainCorrectFlag = 0.06f;
    [SerializeField] private float _gainJunctionFlag = 0.04f;
    [SerializeField] private float _lossWrongFlag = 0.08f;
    [SerializeField] private float _lossJunctionWrongFlag = 0.10f;
    [SerializeField] private float _lossPhantomFlag = 0.05f;
    [Tooltip("How much a good record protects you from the AI. 0 = none. 0.5 = a perfect record halves drain and strikes. The AI keeps fighting back the whole run, so even a perfect run sits around the cap, not at 100%.")]
    [Range(0f, 1f)] [SerializeField] private float _recordShieldV2 = 0.5f;
    [Tooltip("The bar can't go above this until the player reaches the exit portal. The portal is the final punch (K.O.) that takes it to 100%.")]
    [Range(0.5f, 1f)] [SerializeField] private float _capBeforeKnockout = 0.8f;

    [Header("Sense-making: checking the source")]
    [Tooltip("A correct flag counts less when the matching GDD page wasn't opened first (the player may remember it, so it still counts).")]
    [Range(0f, 1f)] [SerializeField] private float _uncheckedFactor = 0.7f;
    [Tooltip("Wrong-game assets can be spotted by gut feeling alone, so there is no page to check. A little less than a checked flag.")]
    [Range(0f, 1f)] [SerializeField] private float _noPageFactor = 0.6f;
    [Tooltip("A false alarm made AFTER checking the page costs this fraction of a normal one (they tried to verify and misjudged).")]
    [Range(0f, 1f)] [SerializeField] private float _checkedMistakeFactor = 0.6f;
    [Tooltip("How recently (seconds) the matching page must have been on screen to count as 'checked'.")]
    [SerializeField] private float _checkWindow = 60f;
    [SerializeField] private string _uncheckedTip = " Tip: open your GDD (I) to be sure next time.";
    [SerializeField] private string _checkedGoodTip = " Nice: you checked your GDD first.";
    [SerializeField] private string _checkedMistakeTip = " You checked your GDD, so look closer at the details next time.";

    [Header("HUD")]
    [Tooltip("Optional: your own image for the evil AI next to the bar. Empty = a generated red eye.")]
    [SerializeField] private Sprite _aiSprite;
    [Tooltip("Creates the purged counter + reliability bar under the timer automatically.")]
    [SerializeField] private bool _autoCreateHud = true;
    [Tooltip("Off = the HUD stays hidden until the player files their first report. Turn on to see it right away (handy for testing).")]
    [SerializeField] private bool _hudShowFromStartV2 = true;

    public event System.Action OnStatsChanged;

    // ---- AI-vs-player bar ----
    private float _cleaned;
    private bool _aiActive;
    private float _nextStrike;
    private bool _knockedOut;

    public float Cleaned => _cleaned;
    /// <summary>True once the AI has started acting (after the first finished junction or first report).</summary>
    public bool AiActive => _aiActive;
    /// <summary>True once the player has reached the exit portal (K.O.): the AI is beaten and stops acting.</summary>
    public bool AiSuppressed => _knockedOut;
    public event System.Action OnKnockout;
    public event System.Action OnAiStrike;
    /// <summary>Fired on every flag result: true = the player was right, false = a false alarm.</summary>
    public event System.Action<bool> OnPlayerResult;

    void OnEnable()  { IntersectionZone.PlayerLeft += HandleJunctionFinished; }
    void OnDisable() { IntersectionZone.PlayerLeft -= HandleJunctionFinished; }

    private void HandleJunctionFinished(IntersectionZone zone) => ActivateAi();

    private void ActivateAi()
    {
        if (_aiActive) return;
        _aiActive = true;
        ScheduleStrike();
    }

    private void ScheduleStrike()
    {
        _nextStrike = Time.time + Random.Range(_strikeIntervalMin, Mathf.Max(_strikeIntervalMin, _strikeIntervalMax));
    }

    void Update()
    {
        if (!_aiActive) return;
        if (TimerManager.Instance == null || !TimerManager.Instance.IsRunning) return;
        if (AiSuppressed) return;

        // The AI is weaker against a player with a good record (same accuracy the end screen reports).
        float skill = LastRun.Judgments >= AuditRunStats.MinJudgmentsForRank ? Mathf.Max(0f, LastRun.Accuracy) : 0f;
        float pressure = 1f - _recordShieldV2 * skill;

        _cleaned = Mathf.Max(0f, _cleaned - _aiDrainPerSecond * pressure * Time.deltaTime);

        if (Time.time >= _nextStrike)
        {
            ScheduleStrike();
            _cleaned = Mathf.Max(0f, _cleaned - _strikeDamage * pressure);
            OnAiStrike?.Invoke();
        }
        LastRun.Cleaned = _cleaned;
    }

    /// <summary>Called by the exit portal: the final punch. Takes the bar to 100% and ends the fight.</summary>
    public void Knockout()
    {
        if (_knockedOut) return;
        _knockedOut = true;
        _cleaned = 1f;
        LastRun.Cleaned = 1f;
        OnKnockout?.Invoke();
        OnStatsChanged?.Invoke();
    }

    private void ChangeCleaned(float delta, bool good)
    {
        ActivateAi();
        if (_knockedOut) return;
        _cleaned = Mathf.Clamp(_cleaned + delta, 0f, _capBeforeKnockout);
        LastRun.Cleaned = _cleaned;
        OnPlayerResult?.Invoke(good);
    }

    void Start()
    {
        if (_autoCreateHud && FindAnyObjectByType<AuditHUD>() == null)
        {
            AuditHUD hud = new GameObject("AuditHUD").AddComponent<AuditHUD>();
            hud.ShowFromStart = _hudShowFromStartV2;
            hud.AiSprite = _aiSprite;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LastRun = new AuditRunStats(); // fresh stats for every maze scene load
        _cleaned = _startCleaned;
        LastRun.Cleaned = _cleaned;
        BookCheckTracker.Clear();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Called once per item, the first time the player comes close enough to have seen it.</summary>
    public void RegisterEncountered(bool isFake)
    {
        if (isFake) LastRun.FakesSeen++; else LastRun.RealSeen++;
        OnStatsChanged?.Invoke();
    }

    private enum CheckState { NoPage, Checked, Unchecked }

    private CheckState GetCheckState(AmbientHallucination item)
    {
        if (item.Obvious || item.Source == null) return CheckState.NoPage;
        return BookCheckTracker.WasChecked(item.Source, _checkWindow) ? CheckState.Checked : CheckState.Unchecked;
    }

    public void ReportFlag(AmbientHallucination item)
    {
        if (item == null || item.Resolved) return;

        CheckState check = GetCheckState(item);
        if (check != CheckState.NoPage)
        {
            LastRun.ChecksPossible++;
            if (check == CheckState.Checked) LastRun.ChecksDone++;
        }

        if (item.IsFake)
        {
            float bonus = item.IsJunction ? _junctionCorrectFlagBonus : _correctFlagBonus;
            float factor = check == CheckState.Checked ? 1f : check == CheckState.Unchecked ? _uncheckedFactor : _noPageFactor;
            LastRun.FakesFlagged++;
            ChangeCleaned((item.IsJunction ? _gainJunctionFlag : _gainCorrectFlag) * factor, true);
            if (TimerManager.Instance != null) TimerManager.Instance.AdjustTime(+bonus);
            item.ResolveCaught();
            string tip = check == CheckState.Checked ? _checkedGoodTip : check == CheckState.Unchecked ? _uncheckedTip : "";
            ShowMessage(string.Format(_correctMessage, Mathf.RoundToInt(bonus)) + tip);
            AuditFeedback.Play(AuditFeedbackKind.Correct, "+" + Mathf.RoundToInt(bonus) + "s");
        }
        else
        {
            float penalty = item.IsJunction ? _junctionWrongFlagPenalty : _wrongFlagPenalty;
            LastRun.FalseAlarms++;
            float lossFactor = check == CheckState.Checked ? _checkedMistakeFactor : 1f;
            ChangeCleaned(-(item.IsJunction ? _lossJunctionWrongFlag : _lossWrongFlag) * lossFactor, false);
            if (TimerManager.Instance != null) TimerManager.Instance.AdjustTime(-penalty);
            item.ResolveGenuine();
            ShowMessage(string.Format(_wrongMessage, item.DisplayName, Mathf.RoundToInt(penalty)) + (check == CheckState.Checked ? _checkedMistakeTip : ""));
            AuditFeedback.Play(AuditFeedbackKind.Wrong, "-" + Mathf.RoundToInt(penalty) + "s");
        }

        OnStatsChanged?.Invoke();
    }

    /// <summary>F pressed while a "phantom" prompt was showing: the prompt was a false alarm, so the report is one too.</summary>
    public void ReportPhantom()
    {
        LastRun.FalseAlarms++;
        ChangeCleaned(-_lossPhantomFlag, false);
        if (TimerManager.Instance != null) TimerManager.Instance.AdjustTime(-_phantomFlagPenalty);
        ShowMessage(string.Format(_phantomMessage, Mathf.RoundToInt(_phantomFlagPenalty)));
        AuditFeedback.Play(AuditFeedbackKind.Wrong, "-" + Mathf.RoundToInt(_phantomFlagPenalty) + "s");
        OnStatsChanged?.Invoke();
    }

    /// <summary>F pressed with nothing flaggable in range: no penalty, but the press is acknowledged.</summary>
    public void ReportNothing()
    {
        ShowMessage(_nothingMessage);
        AuditFeedback.Play(AuditFeedbackKind.Nothing);
    }

    private static void ShowMessage(string message)
    {
        if (GDDMessageUI.Instance != null)      GDDMessageUI.Instance.ShowMessage(message);
        else if (PuzzleUI.Instance != null)     PuzzleUI.Instance.ShowPuzzlePopup(message);
        else                                    Debug.Log($"[Audit] {message}");
    }
}
