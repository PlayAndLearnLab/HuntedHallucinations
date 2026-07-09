using UnityEngine;
using System.Collections.Generic;

public class VisualPuzzleSpawner : MonoBehaviour
{
    // Called by IntersectionDetector after instantiation
    public void Setup(
        VisualPuzzleData data,
        List<Vector2Int> exits,
        Vector2Int correctExit,
        Vector3 cellWorldPos,
        float cellWidth,
        float cellDepth)
    {
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
            Instantiate(prefab, assetPos, rot, transform); // child of this spawner for easy cleanup
        }
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