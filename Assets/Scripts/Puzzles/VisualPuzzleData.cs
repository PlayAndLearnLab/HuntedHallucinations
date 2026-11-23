using UnityEngine;

[CreateAssetMenu(fileName = "VisualPuzzle", menuName = "Maze/Visual Puzzle")]
public class VisualPuzzleData : PuzzleData
{
    [Tooltip("The real, non-hallucinated asset")]
    public GameObject correctPrefab;

    [Tooltip("Pool of hallucinated versions — one is picked per wrong path")]
    public GameObject[] hallucinatedPrefabs;

    [Header("Ambient corridor items (optional, extra variety)")]
    [Tooltip("More GENUINE versions of this asset for corridor items. Every one must still match the manual page " +
             "(same look as the correct prefab), or players who judge by the page get punished unfairly. Ignored at intersections.")]
    public GameObject[] ambientCorrectPrefabs;

    [Tooltip("Extra hallucinated versions used only for corridor items, on top of the list above. Ignored at intersections.")]
    public GameObject[] ambientHallucinatedPrefabs;

    [TextArea]
    [Tooltip("Short hint shown in the popup when this puzzle appears")]
    public string playerHint = "One of these assets has not been hallucinated. Follow the real one.";


    void OnEnable() => puzzleType = PuzzleType.Visual;
}