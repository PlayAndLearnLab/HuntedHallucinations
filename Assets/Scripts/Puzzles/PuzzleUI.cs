using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PuzzleUI : MonoBehaviour
{
    public static PuzzleUI Instance { get; private set; }

    [SerializeField] private CanvasGroup _popupGroup;      // the panel CanvasGroup
    [SerializeField] private TextMeshProUGUI _popupText;   // text inside the panel
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _puzzleRevealSound;

    [SerializeField] private float _fadeInDuration  = 0.3f;
    [SerializeField] private float _holdDuration    = 2.5f;
    [SerializeField] private float _fadeOutDuration = 0.8f;

    private Coroutine _currentFade;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Start hidden
        _popupGroup.alpha          = 0f;
        _popupGroup.interactable   = false;
        _popupGroup.blocksRaycasts = false;
    }

    // Called by IntersectionZone when the player enters
    public void ShowPuzzlePopup(string message)
    {
        if (_currentFade != null) StopCoroutine(_currentFade);
        _currentFade = StartCoroutine(FadeSequence(message));

        if (_audioSource != null && _puzzleRevealSound != null)
            _audioSource.PlayOneShot(_puzzleRevealSound);
    }

    private IEnumerator FadeSequence(string message)
    {
        _popupText.text = message;

        // Fade in
        yield return Fade(0f, 1f, _fadeInDuration);

        // Hold
        yield return new WaitForSeconds(_holdDuration);

        // Fade out
        yield return Fade(1f, 0f, _fadeOutDuration);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _popupGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        _popupGroup.alpha = to;
    }
}