using UnityEngine;

public enum WrongPathConsequence
{
    Nothing,        // player can freely backtrack
    ReturnToStart,  // player is teleported to cell [0,0]
    MazeCollapse    // current behaviour — maze is destroyed and rebuilt
}

[CreateAssetMenu(fileName = "DifficultySettings", menuName = "Maze/Difficulty Settings")]
public class MazeDifficultySettings : ScriptableObject
{
    [Header("Puzzle Difficulty Mix")]
    [Range(0f, 1f)] public float easyProportion   = 0.5f;
    [Range(0f, 1f)] public float mediumProportion = 0.3f;
    [Range(0f, 1f)] public float hardProportion   = 0.2f;

    [Range(0f, 1f)]
    [Tooltip("Chance any given intersection uses a visual puzzle vs text")]
    public float visualPuzzleChance = 0.5f;

    [Header("Wrong Path Consequence")]
    [Tooltip("What happens when the player commits to a wrong path")]
    public WrongPathConsequence wrongPathConsequence = WrongPathConsequence.MazeCollapse;

    public PuzzleDifficulty PickDifficulty()
    {
        float total = easyProportion + mediumProportion + hardProportion;
        float roll  = Random.value * total;

        if (roll < easyProportion)                    return PuzzleDifficulty.Easy;
        if (roll < easyProportion + mediumProportion) return PuzzleDifficulty.Medium;
        return PuzzleDifficulty.Hard;
    }
}



// OLD VERSION - WORKING
// 
// 
// 
// 
// using UnityEngine;

// [CreateAssetMenu(fileName = "DifficultySettings", menuName = "Maze/Difficulty Settings")]
// public class MazeDifficultySettings : ScriptableObject
// {
//     [Range(0f, 1f)] public float easyProportion   = 0.5f;
//     [Range(0f, 1f)] public float mediumProportion = 0.3f;
//     [Range(0f, 1f)] public float hardProportion   = 0.2f;

//     [Range(0f, 1f)] [Tooltip("Chance any given intersection uses a visual puzzle vs text")]
//     public float visualPuzzleChance = 0.5f;

//     // Normalizes proportions in case they don't add up to 1
//     public PuzzleDifficulty PickDifficulty()
//     {
//         float total = easyProportion + mediumProportion + hardProportion;
//         float roll  = Random.value * total;

//         if (roll < easyProportion)                              return PuzzleDifficulty.Easy;
//         if (roll < easyProportion + mediumProportion)           return PuzzleDifficulty.Medium;
//         return PuzzleDifficulty.Hard;
//     }
// }