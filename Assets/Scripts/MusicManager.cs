using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One background track that plays across every scene and loops until the
/// game is over.
///
/// Setup:
///  1. Create an empty GameObject "MusicManager" in the FIRST scene that runs
///     (IntroScene), add this component, and drag your track into _track.
///  2. Make it a prefab and also drop it into MazeTest, so the music still
///     works when you press Play directly in that scene in the Editor.
///     Duplicates destroy themselves, so the track never restarts or doubles.
///
/// The music stops (with a short fade) as soon as one of the _stopInScenes
/// scenes loads — i.e. the win screen (CongratsScene) or the lose screen
/// (GameOver). If the player later goes back to a gameplay scene (restart),
/// it starts again from the beginning.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Music")]
    [SerializeField] private AudioClip _track;
    [Range(0f, 1f)]
    [SerializeField] private float _volume = 0.5f;

    [Header("Game Over")]
    [Tooltip("Music stops when any of these scenes loads. Names must match Build Settings exactly.")]
    [SerializeField] private string[] _stopInScenes = { "CongratsScene", "GameOver" };
    [SerializeField] private float _fadeOutTime = 1.0f;

    private AudioSource _source;
    private Coroutine _fadeRoutine;

    void Awake()
    {
        // Only one MusicManager may ever exist; the first one wins.
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _source = GetComponent<AudioSource>();
        _source.clip = _track;
        _source.loop = true;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;   // 2D: same volume wherever the player is
        _source.volume = _volume;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // Covers the first scene (sceneLoaded has already fired for it).
        HandleScene(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HandleScene(scene.name);

    private void HandleScene(string sceneName)
    {
        if (IsStopScene(sceneName)) StopMusic();
        else PlayMusic();
    }

    private bool IsStopScene(string sceneName)
    {
        foreach (string s in _stopInScenes)
            if (s == sceneName) return true;
        return false;
    }

    /// <summary>Starts the track from the beginning if it isn't already playing.</summary>
    public void PlayMusic()
    {
        if (_track == null) { Debug.LogWarning("MusicManager: no track assigned."); return; }

        if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
        _source.volume = _volume;

        if (!_source.isPlaying) _source.Play();
    }

    /// <summary>Fades the music out and stops it. Safe to call more than once.</summary>
    public void StopMusic()
    {
        if (!_source.isPlaying || _fadeRoutine != null) return;
        _fadeRoutine = StartCoroutine(FadeOutAndStop());
    }

    private IEnumerator FadeOutAndStop()
    {
        float start = _source.volume;
        // Unscaled time, so it still fades if Time.timeScale is 0 on an end screen.
        for (float t = 0f; t < _fadeOutTime; t += Time.unscaledDeltaTime)
        {
            _source.volume = Mathf.Lerp(start, 0f, t / _fadeOutTime);
            yield return null;
        }
        _source.Stop();
        _source.volume = _volume;
        _fadeRoutine = null;
    }
}
