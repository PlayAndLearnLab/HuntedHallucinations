using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the opening story slideshow (Diapo 1-6). Lives in its own scene
/// (e.g. "IntroScene") that plays before the maze scene loads — attach to a
/// Canvas panel ("IntroPanel") that fills the screen.
///
/// Flow:
///  - On Start(), if there are slides, shows the panel and plays through
///    _slides one at a time.
///  - Space / a "Continue" button / a configured auto-advance timer moves
///    to the next slide, with a short crossfade between them.
///  - When the last slide is dismissed, fades the panel out and loads
///    _nextSceneName (the maze scene). OnIntroFinished also fires just
///    before the load, in case anything needs to react first.
///
/// If _slides is empty, the manager loads _nextSceneName immediately, so
/// the game still works with no intro configured.
/// </summary>
public class IntroSlideshowManager : MonoBehaviour
{
    public static IntroSlideshowManager Instance { get; private set; }

    [Header("Slides")]
    [SerializeField] private IntroSlideData[] _slides;

    [Header("UI References")]
    [SerializeField] private GameObject  _introPanel;   // root panel to show/hide
    [SerializeField] private CanvasGroup _canvasGroup;  // on _introPanel, for fading
    [SerializeField] private Image       _slideImage;
    [SerializeField] private GameObject  _slideImageContainer; // parent of _slideImage; hidden for blank slides
    [SerializeField] private Button      _continueButton;
    [SerializeField] private Button      _skipButton;     // optional "Skip Intro" button
    [SerializeField] private TMP_Text    _continuePrompt; // e.g. "Press SPACE to continue" — hidden on the last slide if you want a different "Enter the maze" label

    [Header("Timing")]
    [SerializeField] private float _fadeDuration = 0.35f;

    [Header("Scene Transition")]
    [Tooltip("Scene loaded once the last slide is dismissed. Must be added to Build Settings.")]
    [SerializeField] private string _nextSceneName = "MazeTest";

    /// <summary>Fired once, right before the next scene loads.</summary>
    public event Action OnIntroFinished;
    

    public bool IsPlaying { get; private set; }

    private int _currentIndex = -1;
    private bool _advanceRequested;
    private Coroutine _autoAdvanceRoutine;
    private AsyncOperation _preloadOp;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (_continueButton != null) _continueButton.onClick.AddListener(RequestAdvance);
        if (_skipButton != null)     _skipButton.onClick.AddListener(SkipIntro);

        if (_slides == null || _slides.Length == 0)
        {
            // Nothing configured — don't block the game, just move straight on.
            // No point preloading here since there's nothing to read; load normally.
            SceneManager.LoadScene(_nextSceneName);
            return;
        }

        StartCoroutine(PreloadNextScene());
        StartCoroutine(PlaySlideshow());
    }

    void Update()
    {
        if (!IsPlaying) return;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            RequestAdvance();
    }

    public void SkipIntro()
    {
        if (!IsPlaying) return;
        StopAllCoroutines();
        StartCoroutine(FinishIntro());
    }

    private void RequestAdvance()
    {
        _advanceRequested = true;
    }

    private IEnumerator PlaySlideshow()
    {
        IsPlaying = true;

        if (_introPanel != null) _introPanel.SetActive(true);
        if (_canvasGroup != null) { _canvasGroup.alpha = 0f; yield return Fade(0f, 1f); }

        for (_currentIndex = 0; _currentIndex < _slides.Length; _currentIndex++)
        {
            bool isLastSlide = _currentIndex == _slides.Length - 1;
            yield return ShowSlide(_slides[_currentIndex], isLastSlide);
            yield return WaitForAdvance(_slides[_currentIndex]);

            // Crossfade out before swapping content, unless this was the last slide
            // (that fade is handled by FinishIntro instead).
            if (!isLastSlide)
                yield return Fade(1f, 0f);
        }

        yield return FinishIntro();
    }

    private IEnumerator ShowSlide(IntroSlideData slide, bool isLastSlide)
    {
        if (_slideImage != null)
        {
            bool hasImage = slide.image != null;
            if (_slideImageContainer != null) _slideImageContainer.SetActive(hasImage);
            _slideImage.sprite = slide.image;
            _slideImage.enabled = hasImage;
        }

        if (_continuePrompt != null)
            _continuePrompt.text = isLastSlide ? "Press SPACE to enter the maze" : "Press SPACE to continue";

        if (_currentIndex > 0)
            yield return Fade(0f, 1f);
    }

    private IEnumerator WaitForAdvance(IntroSlideData slide)
    {
        _advanceRequested = false;
        float elapsed = 0f;
        bool hasAutoAdvance = slide.autoAdvanceAfterSeconds > 0f;

        while (!_advanceRequested)
        {
            if (hasAutoAdvance)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= slide.autoAdvanceAfterSeconds) break;
            }
            yield return null;
        }
    }

    private IEnumerator FinishIntro()
    {
        yield return Fade(1f, 0f);

        if (_introPanel != null) _introPanel.SetActive(false);

        IsPlaying = false;

        GoToNextScene();
    }

    private IEnumerator PreloadNextScene()
    {
        if (string.IsNullOrEmpty(_nextSceneName)) yield break;

        _preloadOp = SceneManager.LoadSceneAsync(_nextSceneName);
        _preloadOp.allowSceneActivation = false;

        // Unity parks progress at 0.9 until allowSceneActivation is flipped true —
        // this just waits for that point, it does NOT block anything visually,
        // it runs alongside the slideshow the player is reading through.
        while (_preloadOp.progress < 0.9f)
            yield return null;
    }

    private void GoToNextScene()
    {
        OnIntroFinished?.Invoke();

        if (string.IsNullOrEmpty(_nextSceneName))
        {
            Debug.LogWarning("IntroSlideshowManager: no _nextSceneName set — staying on this scene.");
            return;
        }

        StartCoroutine(ActivatePreloadedScene());
    }

    private IEnumerator ActivatePreloadedScene()
    {
        // Normally the player has been on the last slide for a while, so the
        // preload is already sitting at 0.9 and ready — this returns instantly.
        // If they somehow skipped through faster than the load finished (slow
        // device, huge scene), this waits the extra beat here instead of
        // freezing, so worst case is a slightly longer fade-out rather than a hitch.
        while (_preloadOp == null || _preloadOp.progress < 0.9f)
            yield return null;

        _preloadOp.allowSceneActivation = true;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (_canvasGroup == null) yield break;

        float t = 0f;
        while (t < _fadeDuration)
        {
            t += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, t / _fadeDuration);
            yield return null;
        }
        _canvasGroup.alpha = to;
    }
}