using UnityEngine;
using System.Collections.Generic;

public class IntersectionDetector : MonoBehaviour
{
    [SerializeField] private GameObject _greenArrowPrefab;  // assign in Inspector
    [SerializeField] private GameObject _redCrossPrefab;    // assign in Inspector
    [SerializeField] private float _indicatorHeight = 1.5f; // height above floor

    private MazeGenerator _mazeGenerator;

    // Called by MazeGenerator once the maze is fully built
    public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellSize)
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                MazeCell cell = grid[x, z];
                List<Vector2Int> openExits = GetOpenExits(grid, cell, width, depth);

                // Only spawn indicators at decision points (3+ exits)
                if (openExits.Count >= 3)
                    SpawnIndicators(cell, openExits, distanceFromExit, cellSize);
            }
        }
    }

    // Returns the grid directions of all open passages from this cell
    private List<Vector2Int> GetOpenExits(MazeCell[,] grid, MazeCell cell, int width, int depth)
    {
        List<Vector2Int> exits = new List<Vector2Int>();
        int x = cell.GridX;
        int z = cell.GridZ;

        // if (x + 1 < width && !grid[x + 1, z].HasLeftWall()) exits.Add(Vector2Int.right);
        // if (x - 1 >= 0 && !grid[x - 1, z].HasRightWall()) exits.Add(Vector2Int.left);
        // if (z + 1 < depth && !grid[x, z + 1].HasBackWall()) exits.Add(new Vector2Int(0, 1));
        // if (z - 1 >= 0 && !grid[x, z - 1].HasFrontWall()) exits.Add(new Vector2Int(0, -1));

        if (x + 1 < width  && !cell.HasRightWall()) exits.Add(Vector2Int.right);
        if (x - 1 >= 0     && !cell.HasLeftWall())  exits.Add(Vector2Int.left);
        if (z + 1 < depth  && !cell.HasFrontWall()) exits.Add(new Vector2Int(0, 1));
        if (z - 1 >= 0     && !cell.HasBackWall())  exits.Add(new Vector2Int(0, -1));   

        return exits;
    }

    private void SpawnIndicators(MazeCell cell, List<Vector2Int> exits, int[,] distanceFromExit, float cellSize)
    {
        int x = cell.GridX;
        int z = cell.GridZ;

        // The correct exit is whichever neighbor is closest to the exit (lowest distance)
        Vector2Int bestExit = exits[0];
        int bestDist = int.MaxValue;

        foreach (var exit in exits)
        {
            int nx = x + exit.x;
            int nz = z + exit.y;
            int dist = distanceFromExit[nx, nz];

            if (dist < bestDist)
            {
                bestDist = dist;
                bestExit = exit;
            }
        }

        // Spawn one indicator per exit
        foreach (var exit in exits)
        {
            bool isCorrect = (exit == bestExit);

            // Place indicator halfway along the passage toward the neighbor
            Vector3 indicatorPos = cell.transform.position
                + new Vector3(exit.x, 0, exit.y) * (cellSize * 0.4f)
                + Vector3.up * _indicatorHeight;

            // Rotate to face the direction of the exit
            Quaternion indicatorRot = Quaternion.LookRotation(new Vector3(exit.x, 0, exit.y));

            GameObject prefab = isCorrect ? _greenArrowPrefab : _redCrossPrefab;
            Instantiate(prefab, indicatorPos, indicatorRot);
        }
    }
}