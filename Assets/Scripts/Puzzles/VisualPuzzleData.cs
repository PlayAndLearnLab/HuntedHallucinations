using UnityEngine;

[CreateAssetMenu(fileName = "VisualPuzzle", menuName = "Maze/Visual Puzzle")]
public class VisualPuzzleData : PuzzleData
{
    [Tooltip("The real, non-hallucinated asset")]
    public GameObject correctPrefab;

    [Tooltip("Pool of hallucinated versions — one is picked per wrong path")]
    public GameObject[] hallucinatedPrefabs;

    void OnEnable() => puzzleType = PuzzleType.Visual;
}