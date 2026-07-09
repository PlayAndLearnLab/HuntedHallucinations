using UnityEngine;

public enum PuzzleDifficulty { Easy, Medium, Hard }
public enum PuzzleType { Visual, Text }

public abstract class PuzzleData : ScriptableObject
{
    public PuzzleDifficulty difficulty;
    public PuzzleType       puzzleType;
}