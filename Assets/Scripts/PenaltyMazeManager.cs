using UnityEngine;
using System.Collections;

public class PenaltyMazeManager : MonoBehaviour
{
    public static PenaltyMazeManager Instance { get; private set; }
    public GameObject MazeRestartPanel;

    [SerializeField] private MazeGenerator _mazeGenerator;
    [SerializeField] private MazeDifficultySettings  _difficultySettings;

    private GameObject _player;
    private PlayerController _playerController;
    private Vector3 _lastSnappedPos;

    public MazeDifficultySettings DifficultySettings => _difficultySettings;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        StartCoroutine(FindPlayerWhenReady());
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

    public void old_OnWrongPathCommitted()
    {
        StartCoroutine(RebuildMaze());
    }

    public void OnWrongPathCommitted()
    {
        if (_difficultySettings == null)
        {
            Debug.LogError("PenaltyMazeManager: no DifficultySettings assigned");
            return;
        }

        switch (_difficultySettings.wrongPathConsequence)
        {
            case WrongPathConsequence.Nothing:
                // Do nothing — player can backtrack freely
                break;

            case WrongPathConsequence.ReturnToStart:
                StartCoroutine(ReturnToStart());
                break;

            case WrongPathConsequence.MazeCollapse:
                StartCoroutine(RebuildMaze());
                break;
        }
    }

    private IEnumerator ReturnToStart()
    {
        if (_playerController != null) _playerController.ToggleMovement(false);

        MazeRestartPanel.SetActive(true);
        PlayerCamera.SetCursorFree(true);

        CharacterController cc = _player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Move player out of the way during the pause
        _player.transform.position = Vector3.down * 100f;
        Physics.SyncTransforms();

        yield return new WaitForSeconds(0.3f);

        // Teleport to cell [0,0] of the existing maze — no rebuild
        Vector3 startPos = _mazeGenerator.GetMazeGrid()[0, 0].transform.position + Vector3.up * 0.5f;
        _player.transform.position = startPos;
        Physics.SyncTransforms();

        if (cc != null) cc.enabled = true;
        // Panel stays up until player clicks close
    }

    private IEnumerator RebuildMaze()
    {
        if (_playerController != null) _playerController.ToggleMovement(false);

        MazeRestartPanel.SetActive(true);
        PlayerCamera.SetCursorFree(true);

        CharacterController cc = _player.GetComponent<CharacterController>();
        Vector3 playerPos = _player.transform.position;

        // FIX: Snaps horizontal placement parameters cleanly to width and depth properties
        _lastSnappedPos = new Vector3(
            Mathf.Round(playerPos.x / _mazeGenerator.CellWidth) * _mazeGenerator.CellWidth,
            0f,
            Mathf.Round(playerPos.z / _mazeGenerator.CellDepth) * _mazeGenerator.CellDepth);

        if (cc != null) cc.enabled = false;
        _player.transform.position = Vector3.down * 100f;
        Physics.SyncTransforms();

        _mazeGenerator.DestroyMaze();
        yield return new WaitForSeconds(0.3f);

        yield return _mazeGenerator.RebuildFrom(_lastSnappedPos);
        if (cc != null) cc.enabled = true;
    }

    public void OnRestartPanelClosed()
    {
        Vector3 startPosition = _mazeGenerator.GetMazeGrid()[0, 0].transform.position + Vector3.up * 0.5f;
        _player.transform.position = startPosition;
        Physics.SyncTransforms();

        MazeRestartPanel.SetActive(false);
        PlayerCamera.SetCursorFree(false);
        
        if (_playerController != null) 
            _playerController.ToggleMovement(true);
    }
}

// using UnityEngine;
// using System.Collections;

// public class PenaltyMazeManager : MonoBehaviour
// {
//     public static PenaltyMazeManager Instance { get; private set; }
//     public GameObject MazeRestartPanel;

//     [SerializeField] private MazeGenerator _mazeGenerator;

//     private GameObject _player;
//     private PlayerController _playerController;
//     private Vector3 _lastSnappedPos;

//     void Awake()
//     {
//         if (Instance != null && Instance != this) { Destroy(gameObject); return; }
//         Instance = this;
//     }

//     void Start()
//     {
//         StartCoroutine(FindPlayerWhenReady());
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

//     public void OnWrongPathCommitted()
//     {
//         StartCoroutine(RebuildMaze());
//     }

//     private IEnumerator RebuildMaze()
//     {
//         // Freeze player input immediately
//         if (_playerController != null) _playerController.ToggleMovement(false);

//         // Show panel and free cursor RIGHT AWAY — before any teardown/rebuild
//         MazeRestartPanel.SetActive(true);
//         PlayerCamera.SetCursorFree(true);

//         CharacterController cc = _player.GetComponent<CharacterController>();

//         Vector3 playerPos = _player.transform.position;

//         _lastSnappedPos = new Vector3(
//             Mathf.Round(playerPos.x / _mazeGenerator.CellWidth) * _mazeGenerator.CellWidth,
//             0f,
//             Mathf.Round(playerPos.z / _mazeGenerator.CellDepth) * _mazeGenerator.CellDepth);
        
//         // _lastSnappedPos = new Vector3(
//         //     Mathf.Round(playerPos.x / _mazeGenerator.CellSize) * _mazeGenerator.CellSize,
//         //     0f, 
//         //     Mathf.Round(playerPos.z / _mazeGenerator.CellSize) * _mazeGenerator.CellSize);

//         if (cc != null) cc.enabled = false;

//         _player.transform.position = Vector3.down * 100f;

//         Physics.SyncTransforms();

//         _mazeGenerator.DestroyMaze();

//         yield return new WaitForSeconds(0.3f);

//         yield return _mazeGenerator.RebuildFrom(_lastSnappedPos);

//         if (cc != null) cc.enabled = true;

//         // Panel and cursor are already shown — nothing more needed here
//     }

//     // Call this from the close button's onClick
//     public void OnRestartPanelClosed()
//     {
//         // NOW place the player at cell [0,0] of the newly built maze
//         Vector3 startPosition = _mazeGenerator.GetMazeGrid()[0, 0].transform.position + Vector3.up * 0.5f;
//         _player.transform.position = startPosition;

//         Physics.SyncTransforms();

//         MazeRestartPanel.SetActive(false);
//         PlayerCamera.SetCursorFree(false);
        
//         if (_playerController != null) 
//             _playerController.ToggleMovement(true);
//     }
// }