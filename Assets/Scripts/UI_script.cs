using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.InputSystem; 

public class UI_script : MonoBehaviour
{
    [SerializeField] private GameObject BookPanel;
    [SerializeField] private GameObject ControlsPanel;

    [Tooltip("On-screen book hint (the book icon button, the 'i' key badge, etc.). " +
             "Greyed out and unclickable until the player picks up the Manual Binder or any design page.")]
    [SerializeField] private GameObject[] BookHintIcons;

    [Tooltip("Opacity of the book hint icons while the book is still locked (no Binder and no pages yet).")]
    [Range(0f, 1f)]
    [SerializeField] private float LockedIconAlpha = 0.35f;

    /// <summary>Lets other UI (e.g. AuditHUD) sort itself behind the controls panel and the book.</summary>
    public GameObject ControlsPanelObject => ControlsPanel;
    public GameObject BookPanelObject => BookPanel;

    private GameObject _player;
    private PlayerController _playerController;

    void Start()
    {
        StartCoroutine(FindPlayerWhenReady());

        // Grey out the book hint until the Binder OR any page is collected, then enable it.
        SetBookHintUnlocked(CanOpenBook());
        if (GDDManager.Instance != null)
        {
            GDDManager.Instance.OnBinderCollected += HandleBookUnlocked;
            GDDManager.Instance.OnPageCollected   += HandlePageCollected;
        }
    }

    void OnDestroy()
    {
        if (GDDManager.Instance != null)
        {
            GDDManager.Instance.OnBinderCollected -= HandleBookUnlocked;
            GDDManager.Instance.OnPageCollected   -= HandlePageCollected;
        }
    }

    private void HandleBookUnlocked() => SetBookHintUnlocked(true);
    private void HandlePageCollected(PuzzleData _) => SetBookHintUnlocked(true);

    // The book opens once the player has the Binder OR has picked up at least one page,
    // so skipping the Binder at spawn never locks the player out of a page they collected.
    private static bool CanOpenBook() =>
        GDDManager.Instance == null
        || GDDManager.Instance.HasBinder
        || GDDManager.Instance.CollectedPages.Count > 0;

    private void SetBookHintUnlocked(bool unlocked)
    {
        if (BookHintIcons == null) return;
        foreach (var icon in BookHintIcons)
        {
            if (icon == null) continue;

            // A CanvasGroup fades the icon and all its children together, and
            // interactable = false also disables any Button underneath it.
            var group = icon.GetComponent<CanvasGroup>();
            if (group == null) group = icon.AddComponent<CanvasGroup>();

            group.alpha = unlocked ? 1f : LockedIconAlpha;
            group.interactable = unlocked;
            group.blocksRaycasts = unlocked;
        }
    }

    // --- UPDATED INPUT TRACKING FOR NEW INPUT SYSTEM ---
    void Update()
    {
        // Safely check if a keyboard is connected, then check if the 'I' key was pressed this frame
        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
        {
            ToggleBook();
        }

        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            ToggleControls();
        }
    }

    public void ToggleBook()
    {
        if (BookPanel == null) return;
 
        // If the book panel is active, close it. Otherwise, open it.
        if (BookPanel.activeSelf)
        {
            OnBookClosed();
        }
        else
        {
            OnBookOpened();
        }
    }

    public void ToggleControls()
    {
        if (ControlsPanel == null) return;

        // If the controls panel is active, close it. Otherwise, open it.
        if (ControlsPanel.activeSelf)
        {
            OnControlsClosed();
        }
        else
        {
            OnControlsOpened();
        }
    }

    public void OnControlsOpened()
    {
        if (_playerController != null) _playerController.ToggleMovement(false);

        ControlsPanel.SetActive(true);
        PlayerCamera.SetCursorFree(true);
    }

    public void OnControlsClosed()
    {
        ControlsPanel.SetActive(false);
        PlayerCamera.SetCursorFree(false);
        
        if (_playerController != null) 
            _playerController.ToggleMovement(true);
    }


    private IEnumerator FindPlayerWhenReady()
    {
        while (_player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null)
            {
                _player = found;
                _playerController = _player.GetComponent<PlayerController>();
            } 
            yield return null;
        }
    }

    public void OnBookOpened()
    {
        // Locked until the player has picked up the Manual Binder at (0,0) or any page.
        // Checked here (not only in ToggleBook) because the on-screen BookButton
        // calls this method directly from its OnClick.
        if (BookPanel == null || !CanOpenBook()) return;

        if (_playerController != null) _playerController.ToggleMovement(false);

        BookPanel.SetActive(true);
        PlayerCamera.SetCursorFree(true);

    }

    public void OnBookClosed()
    {
        BookPanel.SetActive(false);
        PlayerCamera.SetCursorFree(false);
        
        if (_playerController != null) 
            _playerController.ToggleMovement(true);
    }
}

// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using System.Collections;
// using UnityEngine.InputSystem; 

// public class UI_script : MonoBehaviour
// {
//     [SerializeField] private GameObject BookPanel;
//     [SerializeField] private GameObject ControlsPanel;

//     private GameObject _player;
//     private PlayerController _playerController;

//     void Start()
//     {
//         StartCoroutine(FindPlayerWhenReady());
//     }

//     // --- UPDATED INPUT TRACKING FOR NEW INPUT SYSTEM ---
//     void Update()
//     {
//         // Safely check if a keyboard is connected, then check if the 'I' key was pressed this frame
//         if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
//         {
//             ToggleBook();
//         }

//         if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
//         {
//             ToggleControls();
//         }
//     }

//     private void ToggleBook()
//     {
//         if (BookPanel == null) return;
 
//         // Locked until the player has picked up the Manual Binder at (0,0).
//         if (GDDManager.Instance != null && !GDDManager.Instance.HasBinder) return;
 
//         // If the book panel is active, close it. Otherwise, open it.
//         if (BookPanel.activeSelf)
//         {
//             OnBookClosed();
//         }
//         else
//         {
//             OnBookOpened();
//         }
//     }

//     public void ToggleControls()
//     {
//         if (ControlsPanel == null) return;

//         // If the controls panel is active, close it. Otherwise, open it.
//         if (ControlsPanel.activeSelf)
//         {
//             OnControlsClosed();
//         }
//         else
//         {
//             OnControlsOpened();
//         }
//     }

//     public void OnControlsOpened()
//     {
//         if (_playerController != null) _playerController.ToggleMovement(false);

//         ControlsPanel.SetActive(true);
//         PlayerCamera.SetCursorFree(true);
//     }

//     public void OnControlsClosed()
//     {
//         ControlsPanel.SetActive(false);
//         PlayerCamera.SetCursorFree(false);
        
//         if (_playerController != null) 
//             _playerController.ToggleMovement(true);
//     }


//     private IEnumerator FindPlayerWhenReady()
//     {
//         while (_player == null)
//         {
//             GameObject found = GameObject.FindWithTag("Player");
//             if (found != null)
//             {
//                 _player = found;
//                 _playerController = _player.GetComponent<PlayerController>();
//             } 
//             yield return null;
//         }
//     }

//     public void OnBookOpened()
//     {
//         if (_playerController != null) _playerController.ToggleMovement(false);

//         BookPanel.SetActive(true);
//         PlayerCamera.SetCursorFree(true);

//     }

//     public void OnBookClosed()
//     {
//         BookPanel.SetActive(false);
//         PlayerCamera.SetCursorFree(false);
        
//         if (_playerController != null) 
//             _playerController.ToggleMovement(true);
//     }
// }