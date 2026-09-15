using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance { get; private set; }

    [SerializeField] private VisualPuzzleData[]      _visualPuzzles;
    [SerializeField] private TextPuzzleData[]        _textPuzzles;
    [SerializeField] private MazeDifficultySettings  _difficultySettings;

    // Puzzles already handed out this maze — cleared at the start of every
    // build via ResetUsedPuzzles() so repeats only happen once every owned
    // puzzle has been used at least once (i.e. more intersections than puzzles).
    private readonly HashSet<PuzzleData> _usedPuzzles = new HashSet<PuzzleData>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public MazeDifficultySettings DifficultySettings => _difficultySettings;

    /// <summary>Call once at the start of each maze build (IntersectionDetector.OnMazeReady does this).</summary>
    public void ResetUsedPuzzles() => _usedPuzzles.Clear();

    public PuzzleData PickPuzzle()
    {
        PuzzleDifficulty difficulty = _difficultySettings.PickDifficulty();
        bool useVisual = Random.value < _difficultySettings.visualPuzzleChance;

        PuzzleData[] fullSet = useVisual
            ? _visualPuzzles.Cast<PuzzleData>().ToArray()
            : _textPuzzles.Cast<PuzzleData>().ToArray();

        var preferredPool = fullSet.Where(p => p.difficulty == difficulty).ToList();
        if (preferredPool.Count == 0) preferredPool = fullSet.ToList(); // fallback to any difficulty

        PuzzleData picked = PickUnusedOrFallback(preferredPool);
        _usedPuzzles.Add(picked);
        return picked;
    }

    private PuzzleData PickUnusedOrFallback(List<PuzzleData> preferredPool)
    {
        // 1. Prefer a puzzle from the ideal (type + difficulty) pool that hasn't been used yet.
        var unused = preferredPool.Where(p => !_usedPuzzles.Contains(p)).ToList();
        if (unused.Count > 0) return unused[Random.Range(0, unused.Count)];

        // 2. That pool is exhausted — look across the whole library (both
        //    puzzle types, any difficulty) for anything still unused, so
        //    repeats only start once every puzzle you own has appeared once.
        var everyPuzzle = _visualPuzzles.Cast<PuzzleData>().Concat(_textPuzzles.Cast<PuzzleData>()).ToList();
        var anyUnused = everyPuzzle.Where(p => !_usedPuzzles.Contains(p)).ToList();
        if (anyUnused.Count > 0) return anyUnused[Random.Range(0, anyUnused.Count)];

        // 3. Every puzzle has been used at least once — there are more
        //    intersections than available puzzles, so a repeat is unavoidable.
        //    Fall back to the ideal pool and allow it.
        return preferredPool[Random.Range(0, preferredPool.Count)];
    }
}

// using UnityEngine;
// using System.Collections.Generic;
// using System.Linq;

// public class PuzzleManager : MonoBehaviour
// {
//     public static PuzzleManager Instance { get; private set; }

//     [SerializeField] private VisualPuzzleData[]      _visualPuzzles;
//     [SerializeField] private TextPuzzleData[]        _textPuzzles;
//     [SerializeField] private MazeDifficultySettings  _difficultySettings;

//     void Awake()
//     {
//         if (Instance != null && Instance != this) { Destroy(gameObject); return; }
//         Instance = this;
//     }

//     public MazeDifficultySettings DifficultySettings => _difficultySettings;

//     public PuzzleData PickPuzzle()
//     {
//         PuzzleDifficulty difficulty = _difficultySettings.PickDifficulty();
//         bool useVisual = Random.value < _difficultySettings.visualPuzzleChance;

//         if (useVisual)
//         {
//             var pool = _visualPuzzles.Where(p => p.difficulty == difficulty).ToList();
//             if (pool.Count == 0) pool = _visualPuzzles.ToList(); // fallback to any difficulty
//             return pool[Random.Range(0, pool.Count)];
//         }
//         else
//         {
//             var pool = _textPuzzles.Where(p => p.difficulty == difficulty).ToList();
//             if (pool.Count == 0) pool = _textPuzzles.ToList();
//             return pool[Random.Range(0, pool.Count)];
//         }
//     }
// }