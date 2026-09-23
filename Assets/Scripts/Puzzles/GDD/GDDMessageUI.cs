using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Dedicated popup for GDD Binder / Page messages — completely separate from
/// PuzzleUI, so it can live in a different spot on the canvas and look different.
///
/// SETUP:
///  - Put this script on an object that is ALWAYS ACTIVE (e.g. the Canvas or an
///    empty "GDDMessageRoot"), NOT on the popup panel itself.
///  - Make the popup panel a child of the canvas, give it a CanvasGroup, and
///    assign the panel's CanvasGroup + its TMP_Text below.
///  - The panel is hidden via alpha (not SetActive), so it stays "alive" and
///    Instance is always registered.
/// </summary>
public class GDDMessageUI : MonoBehaviour
{
    public static GDDMessageUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CanvasGroup _panelGroup;
    [SerializeField] private TMP_Text    _messageText;

    [Header("Timing")]
    [SerializeField] private float _displayDuration = 4f;
    [SerializeField] private float _fadeInTime      = 0.25f;
    [SerializeField] private float _fadeOutTime     = 0.5f;

    private Coroutine _routine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        SetAlpha(0f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ShowMessage(string message)
    {
        if (_panelGroup == null || _messageText == null)
        {
            Debug.LogWarning("GDDMessageUI: panel group or message text not assigned.");
            return;
        }

        _messageText.text = message;

        // A new message replaces whatever is currently showing.
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        // Fade in (unscaled time so it still works if you ever pause with timeScale = 0)
        yield return Fade(_panelGroup.alpha, 1f, _fadeInTime);

        yield return new WaitForSecondsRealtime(_displayDuration);

        yield return Fade(1f, 0f, _fadeOutTime);
        _routine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f) { SetAlpha(to); yield break; }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float a)
    {
        if (_panelGroup == null) return;
        _panelGroup.alpha = a;
        _panelGroup.blocksRaycasts = false; // it's a passive popup, never block clicks
        _panelGroup.interactable = false;
    }
}
