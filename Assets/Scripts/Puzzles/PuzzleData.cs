using UnityEngine;

public enum PuzzleDifficulty { Easy, Medium, Hard }
public enum PuzzleType { Visual, Text }

public abstract class PuzzleData : ScriptableObject
{
    public PuzzleDifficulty difficulty;
    public PuzzleType       puzzleType;

    [Header("GDD Page (Spot-the-Difference Reference)")]
    [Tooltip("Short label shown at the top of the GDD page, e.g. 'ASSET SPEC 04-B'.")]
    public string pageTitle;
 
    [Tooltip("The ground-truth reference image the player compares against the hallucinated in-world asset or text.")]
    public Sprite pageArt;
 
    [TextArea(2, 4)]
    [Tooltip("One short, skimmable line of ground-truth spec text — the player has ~3 seconds to compare it.")]
    public string groundTruthSummary;
}