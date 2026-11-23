using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to BookPanel. Renders the ground-truth reference (title, art, one-line
/// spec) for whichever manual page the player currently holds, so they can hold it
/// up against the AI's hallucinated version at the intersection — spot the
/// difference, close the book, pick the real path.
///
/// Refreshes on OnEnable, which fires whenever UI_script calls
/// BookPanel.SetActive(true) in OnBookOpened().
/// </summary>
public class GDDBookRenderer : MonoBehaviour
{
    [SerializeField] private TMP_Text   _titleText;
    [SerializeField] private Image      _artImage;
    [SerializeField] private TMP_Text   _summaryText;
    [SerializeField] private GameObject _noPageState; // shown before the player has collected anything

    [Header("Navigation")]
    [SerializeField] private Button  _prevButton;
    [SerializeField] private Button  _nextButton;
    [SerializeField] private TMP_Text _pageCounterText; // e.g. "3 / 5", optional
 
    [Header("Text-only pages (Zombie Behaviour Spec)")]
    [Tooltip("The book text is normally invisible (pages are shown through their art). Pages that are text only, " +
             "like the zombie rules, are drawn with this colour and size instead.")]
    [SerializeField] private Color _textPageColor = Color.black;
    [SerializeField] private float _textPageFontSize = 26f;

    private int _pageIndex;
    private int _totalPages;

    // Word images stacked on top of the art for the zombie rules page (created on demand).
    private readonly System.Collections.Generic.List<Image> _overlays = new System.Collections.Generic.List<Image>();

    // The summary text's own look, restored for every normal (art) page.
    private bool _styleCaptured;
    private Color _baseColor;
    private float _baseFontSize;

    void OnEnable()
    {
         if (GDDManager.Instance != null && GDDManager.Instance.CollectedPages.Count > 0)
            _pageIndex = GDDManager.Instance.CollectedPages.Count - 1;
            
        Refresh();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            NextPage();
        }
        if (Keyboard.current != null && Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            PreviousPage();
        }
    }

    public void NextPage()
    {
        if (!HasPages()) return;
        _pageIndex = Mathf.Min(_pageIndex + 1, GDDManager.Instance.CollectedPages.Count - 1);
        Refresh();
    }
 
    public void PreviousPage()
    {
        if (!HasPages()) return;
        _pageIndex = Mathf.Max(_pageIndex - 1, 0);
        Refresh();
    }

    private bool HasPages() => GDDManager.Instance != null && GDDManager.Instance.CollectedPages.Count > 0;

    private void Refresh()
    {
        bool hasPages = HasPages();
 
        if (_noPageState != null) _noPageState.SetActive(!hasPages);
        if (_titleText != null)   _titleText.gameObject.SetActive(hasPages);
        if (_artImage != null)    _artImage.gameObject.SetActive(hasPages);
        if (_summaryText != null) _summaryText.gameObject.SetActive(hasPages);
 
        if (!hasPages)
        {
            if (_prevButton != null) _prevButton.interactable = false;
            if (_nextButton != null) _nextButton.interactable = false;
            if (_pageCounterText != null) _pageCounterText.text = "";
            return;
        }
 
        var pages = GDDManager.Instance.CollectedPages;
        _pageIndex = Mathf.Clamp(_pageIndex, 0, pages.Count - 1);
        PuzzleData page = pages[_pageIndex];
        BookCheckTracker.RecordView(page); // lets the audit tell "checked the source" from "went on gut feeling"
 
        if (_titleText != null)
            _titleText.text = string.IsNullOrEmpty(page.pageTitle) ? "UNTITLED PAGE" : page.pageTitle;
 
        if (_artImage != null)
        {
            _artImage.enabled = page.pageArt != null;
            _artImage.sprite = page.pageArt;
        }
 
        if (_summaryText != null)
            _summaryText.text = page.groundTruthSummary;
 
        if (_prevButton != null) _prevButton.interactable = _pageIndex > 0;
        if (_nextButton != null) _nextButton.interactable = _pageIndex < pages.Count - 1;
 
        if (_pageCounterText != null)
            _pageCounterText.text = $"{_pageIndex + 1} / {pages.Count}";

        ApplyTextPageStyle(page);
        ApplyOverlays(page);
    }

    // Stacks the zombie rules word images on the page art. Each is a full-page transparent image,
    // stretched exactly like the art, so the words land in the template's columns.
    private void ApplyOverlays(PuzzleData page)
    {
        var rules = page as ZombieRulesPageData;
        int needed = rules != null && rules.UsesImages && _artImage != null ? rules.Overlays.Count : 0;

        while (_overlays.Count < needed)
        {
            var go = new GameObject("RulesOverlay" + _overlays.Count, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_artImage.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            _overlays.Add(img);
        }

        for (int i = 0; i < _overlays.Count; i++)
        {
            bool on = i < needed && rules.Overlays[i] != null;
            _overlays[i].gameObject.SetActive(on);
            if (!on) continue;
            _overlays[i].sprite = rules.Overlays[i];
            _overlays[i].preserveAspect = _artImage.preserveAspect;
        }
    }

    // Text-only pages (the zombie rules) need visible text; art pages keep the scene's original text style.
    private void ApplyTextPageStyle(PuzzleData page)
    {
        TMP_Text text = _summaryText != null ? _summaryText : _titleText;
        if (text == null) return;

        if (!_styleCaptured)
        {
            _baseColor = text.color;
            _baseFontSize = text.fontSize;
            _styleCaptured = true;
        }

        if (page is ZombieRulesPageData rulesPage && !rulesPage.UsesImages)
        {
            text.color = _textPageColor;
            text.fontSize = _textPageFontSize;
            // Title and summary share one text object in the scene, so put the title on top of the rules.
            string title = string.IsNullOrEmpty(page.pageTitle) ? "" : $"<b>{page.pageTitle}</b>\n\n";
            text.text = title + page.groundTruthSummary;
        }
        else
        {
            text.color = _baseColor;
            text.fontSize = _baseFontSize;
        }
    }
}