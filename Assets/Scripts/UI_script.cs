using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
// 1. ADD THIS IMPORT LINE FOR THE NEW INPUT SYSTEM
using UnityEngine.InputSystem; 

public class UI_script : MonoBehaviour
{
    [SerializeField] private GameObject BookPanel;

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
    }

    private void ToggleBook()
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