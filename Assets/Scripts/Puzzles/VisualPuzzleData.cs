using UnityEngine;

[CreateAssetMenu(fileName = "VisualPuzzle", menuName = "Maze/Visual Puzzle")]
public class VisualPuzzleData : PuzzleData
{
    [Tooltip("The real, non-hallucinated asset")]
    public GameObject correctPrefab;

    [Tooltip("Pool of hallucinated versions — one is picked per wrong path")]
    public GameObject[] hallucinatedPrefabs;

    [TextArea]
    [Tooltip("Short hint shown in the popup when this puzzle appears")]
    public string playerHint = "One of these assets has not been hallucinated. Follow the real one.";


    void OnEnable() => puzzleType = PuzzleType.Visual;
}