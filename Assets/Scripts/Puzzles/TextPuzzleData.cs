using UnityEngine;

[CreateAssetMenu(fileName = "TextPuzzle", menuName = "Maze/Text Puzzle")]
public class TextPuzzleData : PuzzleData
{
    [Tooltip("The true statement — goes on the correct path")]
    [TextArea] public string trueStatement;

    [Tooltip("Pool of hallucinated false statements — one picked per wrong path")]
    [TextArea] public string[] falseStatements;

    void OnEnable() => puzzleType = PuzzleType.Text;
}