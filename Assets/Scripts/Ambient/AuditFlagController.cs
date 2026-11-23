using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The "report" action. The game uses a third-person orbit camera, so instead of a
/// crosshair raycast this uses proximity only: when the player is within range of a
/// corridor item, from any side, a prompt appears. Pressing F flags the closest one.
/// The prompt looks identical for real and fake items.
/// </summary>
public class AuditFlagController : MonoBehaviour
{
    [Tooltip("Distance (in units) from the object, in any direction, at which the prompt appears.")]
    [SerializeField] private float _interactRange = 3f;
    [SerializeField] private string _promptMessage = "Real or AI-made?\nCheck your manual (I), then flag (F)";

    [Tooltip("An item counts toward the end-of-run stats only once the player has come this close to it.")]
    [SerializeField] private float _encounterRadius = 4f;

    [Tooltip("Optional. A panel GameObject (background Image + a TMP text as a child). " +
             "Leave empty and a simple one is built at runtime.")]
    [SerializeField] private GameObject _promptObject;

    private TMP_Text _promptText;

    private GameObject _player;
    private GameObject _promptRoot;
    private AmbientHallucination _target;
    private float _nextNothingTime;

    [Tooltip("Minimum seconds between 'nothing here' answers so holding or mashing F isn't noisy.")]
    [SerializeField] private float _nothingCooldown = 0.6f;

    [Header("Phantom prompts")]
    [Tooltip("Now and then a random corridor spot is marked where the F prompt will appear with nothing wrong there. " +
             "Pressing F at one is a false alarm, even if the player walks back to it later.")]
    [SerializeField] private bool _phantomEnabled = true;
    [SerializeField] private float _phantomIntervalMin = 18f;
    [SerializeField] private float _phantomIntervalMax = 35f;
    [Tooltip("How long an unused phantom spot stays active (seconds).")]
    [SerializeField] private float _phantomSpotLifetime = 60f;
    [SerializeField] private int _maxPhantomSpots = 2;

    private class PhantomSpot { public Vector3 Position; public float Expires; }
    private static readonly List<PhantomSpot> _spots = new List<PhantomSpot>();

    private bool _phantomArmed;
    private float _nextPhantomTime;

    /// <summary>Called when the maze is torn down: the spots were positions in the old maze.</summary>
    public static void ClearPhantoms() => _spots.Clear();

    void Start()
    {
        _spots.Clear(); // fresh scene = no leftover phantom spots (static list survives scene reloads)
        // A prompt panel assigned in the Inspector starts hidden.
        if (_promptObject != null)
        {
            _promptRoot = _promptObject;
            _promptText = _promptObject.GetComponentInChildren<TMP_Text>(true);
            if (_promptText != null) _promptText.text = _promptMessage;
            else Debug.LogWarning("AuditFlagController: the prompt object has no TMP text child.", _promptObject);
            _promptRoot.SetActive(false);
        }
    }

    void OnEnable()  { IntersectionZone.PlayerLeft += HandleJunctionFinished; }
    void OnDisable() { IntersectionZone.PlayerLeft -= HandleJunctionFinished; }

    // Phantom prompts start once the player has finished a first junction (after the intro stretch).
    private void HandleJunctionFinished(IntersectionZone zone)
    {
        if (_phantomArmed) return;
        _phantomArmed = true;
        ScheduleNextPhantom();
    }

    private void ScheduleNextPhantom()
    {
        _nextPhantomTime = Time.time + Random.Range(_phantomIntervalMin, Mathf.Max(_phantomIntervalMin, _phantomIntervalMax));
    }

    void Update()
    {
        TrackEncounters();
        _target = FindTarget();

        // A real target in range always wins over a phantom spot.
        PhantomSpot phantom = null;
        if (_target == null)
        {
            UpdatePhantomSpawning();
            phantom = FindPhantom();
        }

        SetPromptVisible(_target != null || phantom != null);

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame && CanUseFlagKey())
        {
            if (AuditScoreManager.Instance != null)
            {
                if (_target != null)
                {
                    AuditScoreManager.Instance.ReportFlag(_target);
                }
                else if (phantom != null)
                {
                    // The prompt was lying: reporting it is a false alarm.
                    AuditScoreManager.Instance.ReportPhantom();
                    _spots.Remove(phantom);
                }
                else if (Time.unscaledTime >= _nextNothingTime)
                {
                    // F always answers: nothing in range and no prompt = a neutral "nothing here", no penalty.
                    AuditScoreManager.Instance.ReportNothing();
                    _nextNothingTime = Time.unscaledTime + _nothingCooldown;
                }
            }
            _target = null;
            SetPromptVisible(false);
        }
    }

    // Marks a random corridor spot on the route. The prompt appears whenever the player is near it,
    // and keeps appearing until it is used or expires, so walking back to it still counts as a false alarm.
    private void UpdatePhantomSpawning()
    {
        if (!_phantomEnabled || !_phantomArmed || Time.time < _nextPhantomTime) return;

        ScheduleNextPhantom();

        if (_spots.Count >= _maxPhantomSpots) return;
        if (TimerManager.Instance != null && !TimerManager.Instance.IsRunning) return;
        if (!AmbientHallucinationSpawner.TryPickPhantomSpot(out Vector3 pos)) return;

        foreach (PhantomSpot existing in _spots)
        {
            Vector3 delta = existing.Position - pos;
            delta.y = 0f;
            if (delta.sqrMagnitude < 3f * 3f) return; // don't stack two spots on top of each other
        }

        _spots.Add(new PhantomSpot { Position = pos, Expires = Time.time + _phantomSpotLifetime });
    }

    // The phantom spot the player is currently standing near (corridors only, never inside a junction).
    private PhantomSpot FindPhantom()
    {
        _spots.RemoveAll(x => Time.time >= x.Expires);
        if (_spots.Count == 0) return null;
        if (!CanUseFlagKey() || IntersectionZone.PlayerInsideAny) return null;

        if (_player == null) _player = GameObject.FindWithTag("Player");
        if (_player == null) return null;

        PhantomSpot best = null;
        float bestSqr = _interactRange * _interactRange;
        foreach (PhantomSpot spot in _spots)
        {
            Vector3 delta = spot.Position - _player.transform.position;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (sqr <= bestSqr) { bestSqr = sqr; best = spot; }
        }
        return best;
    }

    // Same rule as the prompt: not while the book / a panel has the cursor.
    private bool CanUseFlagKey() => Cursor.lockState == CursorLockMode.Locked;

    // First time the player gets near an item it becomes part of their stats ("caught X of Y").
    // Items on branches they never visit are never counted.
    private void TrackEncounters()
    {
        if (_player == null) _player = GameObject.FindWithTag("Player");
        if (_player == null || AuditScoreManager.Instance == null) return;

        float radiusSqr = _encounterRadius * _encounterRadius;
        foreach (AmbientHallucination item in AmbientHallucination.Active)
        {
            if (item == null || item.Encountered || !item.IsVisibleNow) continue;

            Vector3 delta = item.transform.position - _player.transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= radiusSqr && item.MarkEncountered())
                AuditScoreManager.Instance.RegisterEncountered(item.IsFake);
        }
    }

    private AmbientHallucination FindTarget()
    {
        // Book / controls panel / penalty panel open -> cursor is free -> no interaction.
        if (Cursor.lockState != CursorLockMode.Locked) return null;

        if (_player == null) _player = GameObject.FindWithTag("Player");
        if (_player == null) return null;

        AmbientHallucination best = null;
        float bestSqr = _interactRange * _interactRange;

        foreach (AmbientHallucination item in AmbientHallucination.Active)
        {
            if (item == null || item.Resolved || !item.IsVisibleNow) continue;

            Vector3 toItem = item.transform.position - _player.transform.position;
            toItem.y = 0f;
            float sqr = toItem.sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = item;
        }
        return best;
    }

    // ---------- prompt UI ----------

    private void SetPromptVisible(bool visible)
    {
        if (_promptRoot == null)
        {
            if (!visible) return;
            BuildPrompt();
        }
        if (_promptRoot.activeSelf != visible) _promptRoot.SetActive(visible);
    }

    private void BuildPrompt()
    {
        var canvasGO = new GameObject("AuditPromptCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panelGO = new GameObject("Prompt", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelRT = (RectTransform)panelGO.transform;
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.2f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(760f, 70f);
        var bg = panelGO.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);
        bg.raycastTarget = false;

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(panelGO.transform, false);
        var textRT = (RectTransform)textGO.transform;
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = textRT.offsetMax = Vector2.zero;

        var tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = _promptMessage;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 32f;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        _promptText = tmp;
        _promptRoot = panelGO;
    }
}
