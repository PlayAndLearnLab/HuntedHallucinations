using UnityEngine;
using TMPro;

/// <summary>
/// Put this on a panel in the CongratsScene and the GameOver scene. It reads the
/// stats left behind by AuditScoreManager.LastRun and fills in the texts.
/// Every text field is optional.
/// </summary>
public class AuditResultsPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text _caughtText;
    [SerializeField] private TMP_Text _falseAlarmsText;
    [SerializeField] private TMP_Text _accuracyText;
    [SerializeField] private TMP_Text _rankText;
    [Tooltip("Optional: how often the player opened the matching manual page before flagging.")]
    [SerializeField] private TMP_Text _checkedText;
    [Tooltip("Optional: the final state of the sense-making bar.")]
    [SerializeField] private TMP_Text _cleanedText;

    [Tooltip("Write the rank in capitals (reads like an ink stamp: SENIOR QA).")]
    [SerializeField] private bool _rankUppercase = true;

    [Tooltip("Optional. Hidden if the player never met an ambient hallucination this run.")]
    [SerializeField] private GameObject _hideIfNoData;

    void Start()
    {
        AuditRunStats s = AuditScoreManager.LastRun;
        bool hasData = s != null && (s.FakesSeen > 0 || s.RealSeen > 0 || s.Judgments > 0);

        if (_hideIfNoData != null) _hideIfNoData.SetActive(hasData);
        if (s == null) return;

        if (_caughtText != null)
            _caughtText.text = $"Hallucinations caught: {s.FakesFlagged} / {s.FakesSeen}";

        if (_falseAlarmsText != null)
            _falseAlarmsText.text = $"False alarms: {s.FalseAlarms}";

        if (_accuracyText != null)
            _accuracyText.text = s.Accuracy < 0f
                ? "Audit accuracy: -"
                : $"Audit accuracy: {Mathf.RoundToInt(s.Accuracy * 100f)}%";

        if (_cleanedText != null)
            _cleanedText.text = $"Sense-making: {Mathf.RoundToInt(s.Cleaned * 100f)}%";

        if (_checkedText != null)
            _checkedText.text = s.ChecksPossible == 0
                ? ""
                : $"You checked your manual before {s.ChecksDone} of {s.ChecksPossible} reports";

        if (_rankText != null)
            _rankText.text = _rankUppercase ? s.Rank.ToUpperInvariant() : s.Rank;
    }
}
