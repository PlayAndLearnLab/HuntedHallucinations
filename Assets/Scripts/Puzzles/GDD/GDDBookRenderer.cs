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

    void OnEnable()
    {
        Refresh();
    }

    private void Refresh()
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