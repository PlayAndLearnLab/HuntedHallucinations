using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance { get; private set; }

    [SerializeField] private VisualPuzzleData[]      _visualPuzzles;
    [SerializeField] private TextPuzzleData[]        _textPuzzles;
    [SerializeField] private MazeDifficultySettings  _difficultySettings;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public MazeDifficultySettings DifficultySettings => _difficultySettings;

    public PuzzleData PickPuzzle()
    {
        PuzzleDifficulty difficulty = _difficultySettings.PickDifficulty();
        bool useVisual = Random.value < _difficultySettings.visualPuzzleChance;

        if (useVisual)
        {
            var pool = _visualPuzzles.Where(p => p.difficulty == difficulty).ToList();
            if (pool.Count == 0) pool = _visualPuzzles.ToList(); // fallback to any difficulty
            return pool[Random.Range(0, pool.Count)];
        }
        else
        {
            var pool = _textPuzzles.Where(p => p.difficulty == difficulty).ToList();
            if (pool.Count == 0) pool = _textPuzzles.ToList();
            return pool[Random.Range(0, pool.Count)];
        }
    }
}