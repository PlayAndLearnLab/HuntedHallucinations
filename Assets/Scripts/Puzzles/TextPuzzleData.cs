using UnityEngine;

[CreateAssetMenu(fileName = "TextPuzzle", menuName = "Maze/Text Puzzle")]
public class TextPuzzleData : PuzzleData
{
    [Tooltip("The true statement — goes on the correct path")]
    [TextArea] public string trueStatement;

    [Tooltip("Pool of hallucinated false statements — one picked per wrong path")]
    [TextArea] public string[] falseStatements;

    [TextArea]
    [Tooltip("Short hint shown in the popup when this puzzle appears")]
    public string playerHint = "One of these statements is true. Follow the correct one.";

    void OnEnable() => puzzleType = PuzzleType.Text;
}