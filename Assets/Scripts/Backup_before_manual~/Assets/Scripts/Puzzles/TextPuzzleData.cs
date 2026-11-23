using UnityEngine;

[CreateAssetMenu(fileName = "TextPuzzle", menuName = "Maze/Text Puzzle")]
public class TextPuzzleData : PuzzleData
{
    [Tooltip("The true statement — goes on the correct path")]
    [TextArea] public string trueStatement;

    [Tooltip("Pool of hallucinated false statements — one picked per wrong path")]
    [TextArea] public string[] falseStatements;

    [Header("Ambient corridor items (optional, extra variety)")]
    [Tooltip("More TRUE ways to state the same fact for corridor items. Each must still agree with the GDD page. Ignored at intersections.")]
    [TextArea] public string[] ambientTrueStatements;

    [Tooltip("Extra false statements used only for corridor items, on top of the list above. Ignored at intersections.")]
    [TextArea] public string[] ambientFalseStatements;

    [TextArea]
    [Tooltip("Short hint shown in the popup when this puzzle appears")]
    public string playerHint = "One of these statements is true. Follow the correct one.";

    void OnEnable() => puzzleType = PuzzleType.Text;
}