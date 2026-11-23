using System.Collections;
using UnityEngine;

/// <summary>
/// Temporary diagnostic for the "no ground in the web build" problem.
/// Drop it on any active GameObject in MazeTest (e.g. "Maze Generator"),
/// make a Development Build, play, and copy the lines starting with [GroundDebug]
/// from the browser's JavaScript console. Delete the script afterwards.
/// </summary>
public class GroundDebug : MonoBehaviour
{
    IEnumerator Start()
    {
        // 1. What terrains exist in the build?
        Terrain[] terrains = Terrain.activeTerrains;
        Debug.Log($"[GroundDebug] Active terrains: {terrains.Length}");

        foreach (Terrain t in terrains)
        {
            TerrainData d = t.terrainData;
            TerrainCollider tc = t.GetComponent<TerrainCollider>();

            Debug.Log($"[GroundDebug] Terrain '{t.name}' pos={t.transform.position} " +
                      $"enabled={t.enabled} drawHeightmap={t.drawHeightmap} " +
                      $"material={(t.materialTemplate ? t.materialTemplate.name + " / " + t.materialTemplate.shader.name : "NULL")} " +
                      $"shaderSupported={(t.materialTemplate ? t.materialTemplate.shader.isSupported : false)}");

            if (d == null)
            {
                Debug.LogError("[GroundDebug] TerrainData is NULL");
            }
            else
            {
                Debug.Log($"[GroundDebug] TerrainData '{d.name}' size={d.size} heightmapRes={d.heightmapResolution} " +
                          $"covers X {t.transform.position.x:F1}..{t.transform.position.x + d.size.x:F1}, " +
                          $"Z {t.transform.position.z:F1}..{t.transform.position.z + d.size.z:F1}");
                Debug.Log($"[GroundDebug] Ground height at maze origin (0,0) = " +
                          $"{t.SampleHeight(Vector3.zero) + t.transform.position.y:F2}");
            }

            Debug.Log($"[GroundDebug] TerrainCollider: {(tc == null ? "MISSING" : $"enabled={tc.enabled} data={(tc.terrainData ? tc.terrainData.name : "NULL")}")}");
        }

        // 2. Wait for the maze generator to spawn the player, then track it.
        GameObject player = null;
        while (player == null)
        {
            player = GameObject.FindWithTag("Player");
            yield return null;
        }
        Debug.Log($"[GroundDebug] Player spawned at {player.transform.position}");

        for (int i = 0; i < 10; i++)
        {
            Vector3 p = player.transform.position;
            bool hitBelow = Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 500f);
            Debug.Log($"[GroundDebug] t={i * 0.5f:F1}s player={p} groundBelow=" +
                      (hitBelow ? $"'{hit.collider.name}' at y={hit.point.y:F2}" : "NOTHING"));
            yield return new WaitForSeconds(0.5f);
        }
    }
}