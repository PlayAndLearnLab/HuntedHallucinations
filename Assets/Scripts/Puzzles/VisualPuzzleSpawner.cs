using UnityEngine;
using System.Collections.Generic;

public class VisualPuzzleSpawner : MonoBehaviour
{
    private List<GameObject> _assets = new List<GameObject>();
    
    private string _puzzleHint = "Find the real one — one of these has not been hallucinated.";

    public void Setup(
        VisualPuzzleData data,
        List<Vector2Int> exits,
        Vector2Int correctExit,
        Vector3 cellWorldPos,
        float cellWidth,
        float cellDepth)
    {
        
        _puzzleHint = data.playerHint;

        // Shuffle the hallucinated prefabs so we don't always pick the same ones
        var hallucinationPool = new List<GameObject>(data.hallucinatedPrefabs);
        Shuffle(hallucinationPool);

        int hallucinationIndex = 0;

        foreach (var exit in exits)
        {
            bool isCorrect = (exit == correctExit);

            // Place asset in front of its path entrance
            Vector3 assetPos = cellWorldPos
                + new Vector3(exit.x * cellWidth * 0.45f, 0, exit.y * cellDepth * 0.45f);

            GameObject prefab = isCorrect
                ? data.correctPrefab
                : hallucinationPool[hallucinationIndex++ % hallucinationPool.Count];

            Quaternion rot = Quaternion.LookRotation(new Vector3(exit.x, 0, exit.y));
            GameObject asset = Instantiate(prefab, assetPos, rot, transform); // child of this spawner for easy cleanup
            
            SetVisible(asset, false); // start invisible
            _assets.Add(asset);
        }
    }

    public void Reveal()
    {
        foreach (var asset in _assets)
            SetVisible(asset, true);

        if (PuzzleUI.Instance == null)
        {
            Debug.LogError("PuzzleUI.Instance is null — make sure PuzzleUI is in the scene and runs before spawners");
            return;
        }

        PuzzleUI.Instance.ShowPuzzlePopup(_puzzleHint);
    }

    private void SetVisible(GameObject obj, bool visible)
    {
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            renderer.enabled = visible;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}