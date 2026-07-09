using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class TextPuzzleSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _textLabelPrefab; // prefab with a TextMeshPro component

    public void Setup(
        TextPuzzleData data,
        List<Vector2Int> exits,
        Vector2Int correctExit,
        Vector3 cellWorldPos,
        float cellWidth,
        float cellDepth,
        float labelHeight)
    {
        var falsePool = new List<string>(data.falseStatements);
        Shuffle(falsePool);
        int falseIndex = 0;

        foreach (var exit in exits)
        {
            bool isCorrect = (exit == correctExit);
            string statement = isCorrect
                ? data.trueStatement
                : falsePool[falseIndex++ % falsePool.Count];

            Vector3 labelPos = cellWorldPos
                + new Vector3(exit.x * cellWidth * 0.45f, 0, exit.y * cellDepth * 0.45f)
                + Vector3.up * labelHeight;

            Quaternion rot = Quaternion.LookRotation(new Vector3(exit.x, 0, exit.y));

            GameObject label = Instantiate(_textLabelPrefab, labelPos, rot, transform);
            TextMeshPro tmp = label.GetComponentInChildren<TextMeshPro>();
            if (tmp != null) tmp.text = statement;
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