using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.InputSystem; 

public class UI_script : MonoBehaviour
{
    [SerializeField] private GameObject BookPanel;
    [SerializeField] private GameObject ControlsPanel;

    private GameObject _player;
    private PlayerController _playerController;

    void Start()
    {
        StartCoroutine(FindPlayerWhenReady());
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

    private void ToggleBook()
    {
        if (BookPanel == null) return;
 
        // Locked until the player has picked up the GDD Binder at (0,0).
        if (GDDManager.Instance != null && !GDDManager.Instance.HasBinder) return;
 
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