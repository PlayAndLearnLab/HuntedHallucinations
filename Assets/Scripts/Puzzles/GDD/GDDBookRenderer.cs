using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to BookPanel. Renders the ground-truth reference (title, art, one-line
/// spec) for whichever GDD page the player currently holds, so they can hold it
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
 
    private int _pageIndex;
    private int _totalPages;

    void OnEnable()
    {
         if (GDDManager.Instance != null && GDDManager.Instance.CollectedPages.Count > 0)
            _pageIndex = GDDManager.Instance.CollectedPages.Count - 1;
            
        Refresh();
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
    }

    private void old_Refresh()
    {
        PuzzleData page = GDDManager.Instance != null ? GDDManager.Instance.CurrentPage : null;
        bool hasPage = page != null;

        if (_noPageState != null) _noPageState.SetActive(!hasPage);
        if (!hasPage) return;

        if (_titleText != null)
            _titleText.text = string.IsNullOrEmpty(page.pageTitle) ? "UNTITLED PAGE" : page.pageTitle;

        if (_artImage != null)
        {
            _artImage.enabled = page.pageArt != null;
            _artImage.sprite = page.pageArt;
        }

        if (_summaryText != null)
            _summaryText.text = page.groundTruthSummary;
    }
}