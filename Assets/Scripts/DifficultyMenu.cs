using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameSession
{
    public static MazeDifficultySettings SelectedDifficulty;
}

public class DifficultyMenu : MonoBehaviour
{
    [SerializeField] private string _mazeSceneName = "MazeTest"; // exact scene name

    // Hook this to each button's OnClick
    public void LaunchMaze(MazeDifficultySettings settings)
    {
        GameSession.SelectedDifficulty = settings;
        SceneManager.LoadScene(_mazeSceneName);
    }
}