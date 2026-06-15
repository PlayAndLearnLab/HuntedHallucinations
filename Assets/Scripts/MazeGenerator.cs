using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MazeGenerator : MonoBehaviour
{
    [SerializeField] private MazeCell _mazeCellPrefab;
    [SerializeField] private int _mazeWidth;
    [SerializeField] private int _mazeDepth;
    [SerializeField] private GameObject _playerPrefab;

    [Range(0f, 1f)]
    [SerializeField] private float _wrongPathBias = 0.7f;

    [SerializeField] private float _cellSize = 2f;

    private MazeCell[,] _mazeGrid;
    private int[,] _distanceFromExit;
    private Vector2Int _exitCoord;

    IEnumerator Start()
    {
        _exitCoord = new Vector2Int(_mazeWidth - 1, _mazeDepth - 1);

        _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];

        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
                _mazeGrid[x, z] = Instantiate(
                    _mazeCellPrefab,
                    new Vector3(x * _cellSize, 0, z * _cellSize),
                    Quaternion.identity);

                _mazeGrid[x, z].transform.localScale = Vector3.one * _cellSize;

                // Store grid coordinates on the cell so we never rely on world position for indexing
                _mazeGrid[x, z].SetGridPosition(x, z);
            }
        }

        ComputeDistancesFromExit();

        yield return GenerateMaze(null, _mazeGrid[0, 0]);

        _mazeGrid[_exitCoord.x, _exitCoord.y].ClearFrontWall();

        Vector3 startPosition = _mazeGrid[0, 0].transform.position + Vector3.up * 0.5f;
        Instantiate(_playerPrefab, startPosition, Quaternion.identity);
    }

    private void ComputeDistancesFromExit()
    {
        _distanceFromExit = new int[_mazeWidth, _mazeDepth];

        for (int x = 0; x < _mazeWidth; x++)
            for (int z = 0; z < _mazeDepth; z++)
                _distanceFromExit[x, z] = -1;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(_exitCoord);
        _distanceFromExit[_exitCoord.x, _exitCoord.y] = 0;

        Vector2Int[] directions = {
            Vector2Int.right, Vector2Int.left,
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int nextDist = _distanceFromExit[current.x, current.y] + 1;

            foreach (var dir in directions)
            {
                Vector2Int neighbor = current + dir;
                if (neighbor.x < 0 || neighbor.x >= _mazeWidth) continue;
                if (neighbor.y < 0 || neighbor.y >= _mazeDepth) continue;
                if (_distanceFromExit[neighbor.x, neighbor.y] != -1) continue;

                _distanceFromExit[neighbor.x, neighbor.y] = nextDist;
                queue.Enqueue(neighbor);
            }
        }
    }

    private IEnumerator GenerateMaze(MazeCell previousCell, MazeCell currentCell)
    {
        currentCell.Visit();
        ClearWalls(previousCell, currentCell);

        yield return new WaitForSeconds(0.05f);

        MazeCell nextCell;
        do
        {
            nextCell = GetNextUnvisitedCell(currentCell);
            if (nextCell != null)
                yield return GenerateMaze(currentCell, nextCell);
        }
        while (nextCell != null);
    }

    private MazeCell GetNextUnvisitedCell(MazeCell currentCell)
    {
        var unvisitedCells = GetUnvisitedCells(currentCell).ToList();
        if (unvisitedCells.Count == 0) return null;

        if (Random.value < _wrongPathBias)
        {
            return unvisitedCells
                .OrderByDescending(c => _distanceFromExit[c.GridX, c.GridZ])
                .First();
        }
        else
        {
            return unvisitedCells.OrderBy(_ => Random.Range(1, 10)).FirstOrDefault();
        }
    }

    private IEnumerable<MazeCell> GetUnvisitedCells(MazeCell currentCell)
    {
        // Use grid coordinates, not world position
        int x = currentCell.GridX;
        int z = currentCell.GridZ;

        if (x + 1 < _mazeWidth && !_mazeGrid[x + 1, z].IsVisited) yield return _mazeGrid[x + 1, z];
        if (x - 1 >= 0 && !_mazeGrid[x - 1, z].IsVisited) yield return _mazeGrid[x - 1, z];
        if (z + 1 < _mazeDepth && !_mazeGrid[x, z + 1].IsVisited) yield return _mazeGrid[x, z + 1];
        if (z - 1 >= 0 && !_mazeGrid[x, z - 1].IsVisited) yield return _mazeGrid[x, z - 1];
    }

    private void ClearWalls(MazeCell previousCell, MazeCell currentCell)
    {
        if (previousCell == null) return;

        if (previousCell.GridX < currentCell.GridX) { previousCell.ClearRightWall(); currentCell.ClearLeftWall(); return; }
        if (previousCell.GridX > currentCell.GridX) { previousCell.ClearLeftWall(); currentCell.ClearRightWall(); return; }
        if (previousCell.GridZ < currentCell.GridZ) { previousCell.ClearFrontWall(); currentCell.ClearBackWall(); return; }
        if (previousCell.GridZ > currentCell.GridZ) { previousCell.ClearBackWall(); currentCell.ClearFrontWall(); return; }
    }

    void Update() { }
}


//using UnityEngine;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;

//public class MazeGenerator : MonoBehaviour
//{
//    [SerializeField] private MazeCell _mazeCellPrefab;
//    [SerializeField] private int _mazeWidth;
//    [SerializeField] private int _mazeDepth;

//    [SerializeField] private GameObject _playerPrefab;

//    [Range(0f, 1f)]
//    [SerializeField] private float _wrongPathBias = 0.7f;

//    [SerializeField] private float _cellSize = 2f;

//    private MazeCell[,] _mazeGrid;
//    private int[,] _distanceFromExit;
//    private Vector2Int _exitCoord;

//    IEnumerator Start()
//    {
//        _exitCoord = new Vector2Int(_mazeWidth - 1, _mazeDepth - 1);

//        _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];


//        for (int x = 0; x < _mazeWidth; x++)
//        {
//            for (int z = 0; z < _mazeDepth; z++)
//            {
//                _mazeGrid[x, z] = Instantiate(_mazeCellPrefab, new Vector3(x * _cellSize, 0, z * _cellSize), Quaternion.identity);
//                _mazeGrid[x, z].transform.localScale = Vector3.one * _cellSize;
//            }
//        }

//        ComputeDistancesFromExit();

//        yield return GenerateMaze(null, _mazeGrid[0, 0]);

//        // Clear the exit wall — exit is at top-right, so clear its front wall
//        // (the wall facing outward along the Z axis)
//        _mazeGrid[_exitCoord.x, _exitCoord.y].ClearFrontWall();

//        // Spawn player at the entrance cell, slightly above the floor
//        Vector3 startPosition = _mazeGrid[0, 0].transform.position + Vector3.up * 0.5f;
//        Instantiate(_playerPrefab, startPosition, Quaternion.identity);
//    }

//    private void ComputeDistancesFromExit()
//    {
//        _distanceFromExit = new int[_mazeWidth, _mazeDepth];

//        for (int x = 0; x < _mazeWidth; x++)
//            for (int z = 0; z < _mazeDepth; z++)
//                _distanceFromExit[x, z] = -1;

//        Queue<Vector2Int> queue = new Queue<Vector2Int>();
//        queue.Enqueue(_exitCoord);
//        _distanceFromExit[_exitCoord.x, _exitCoord.y] = 0;

//        Vector2Int[] directions = {
//            Vector2Int.right, Vector2Int.left,
//            new Vector2Int(0, 1), new Vector2Int(0, -1)
//        };

//        while (queue.Count > 0)
//        {
//            Vector2Int current = queue.Dequeue();
//            int nextDist = _distanceFromExit[current.x, current.y] + 1;

//            foreach (var dir in directions)
//            {
//                Vector2Int neighbor = current + dir;
//                if (neighbor.x < 0 || neighbor.x >= _mazeWidth) continue;
//                if (neighbor.y < 0 || neighbor.y >= _mazeDepth) continue;
//                if (_distanceFromExit[neighbor.x, neighbor.y] != -1) continue;

//                _distanceFromExit[neighbor.x, neighbor.y] = nextDist;
//                queue.Enqueue(neighbor);
//            }
//        }
//    }

//    private IEnumerator GenerateMaze(MazeCell previousCell, MazeCell currentCell)
//    {
//        currentCell.Visit();
//        ClearWalls(previousCell, currentCell);

//        yield return new WaitForSeconds(0.05f);

//        MazeCell nextCell;
//        do
//        {
//            nextCell = GetNextUnvisitedCell(currentCell);
//            if (nextCell != null)
//                yield return GenerateMaze(currentCell, nextCell);
//        }
//        while (nextCell != null);
//    }

//    private MazeCell GetNextUnvisitedCell(MazeCell currentCell)
//    {
//        var unvisitedCells = GetUnvisitedCells(currentCell).ToList();
//        if (unvisitedCells.Count == 0) return null;

//        if (Random.value < _wrongPathBias)
//        {
//            return unvisitedCells
//                .OrderByDescending(c => _distanceFromExit[
//                    (int)c.transform.position.x,
//                    (int)c.transform.position.z])
//                .First();
//        }
//        else
//        {
//            return unvisitedCells.OrderBy(_ => Random.Range(1, 10)).FirstOrDefault();
//        }
//    }

//    private IEnumerable<MazeCell> GetUnvisitedCells(MazeCell currentCell)
//    {
//        int x = (int)currentCell.transform.position.x;
//        int z = (int)currentCell.transform.position.z;

//        if (x + 1 < _mazeWidth && !_mazeGrid[x + 1, z].IsVisited) yield return _mazeGrid[x + 1, z];
//        if (x - 1 >= 0 && !_mazeGrid[x - 1, z].IsVisited) yield return _mazeGrid[x - 1, z];
//        if (z + 1 < _mazeDepth && !_mazeGrid[x, z + 1].IsVisited) yield return _mazeGrid[x, z + 1];
//        if (z - 1 >= 0 && !_mazeGrid[x, z - 1].IsVisited) yield return _mazeGrid[x, z - 1];
//    }

//    private void ClearWalls(MazeCell previousCell, MazeCell currentCell)
//    {
//        if (previousCell == null) return;

//        if (previousCell.transform.position.x < currentCell.transform.position.x) { previousCell.ClearRightWall(); currentCell.ClearLeftWall(); return; }
//        if (previousCell.transform.position.x > currentCell.transform.position.x) { previousCell.ClearLeftWall(); currentCell.ClearRightWall(); return; }
//        if (previousCell.transform.position.z < currentCell.transform.position.z) { previousCell.ClearFrontWall(); currentCell.ClearBackWall(); return; }
//        if (previousCell.transform.position.z > currentCell.transform.position.z) { previousCell.ClearBackWall(); currentCell.ClearFrontWall(); return; }
//    }

//    void Update() { }
//}



////using UnityEngine;
////using System.Collections;
////using System.Collections.Generic;
////using System.Linq;

////public class MazeGenerator : MonoBehaviour
////{
////    [SerializeField] private MazeCell _mazeCellPrefab;
////    [SerializeField] private int _mazeWidth;
////    [SerializeField] private int _mazeDepth;

////    // How strongly wrong paths are biased away from the exit.
////    // 0 = pure random (original behaviour)
////    // 1 = wrong paths always run as far from exit as possible
////    [Range(0f, 1f)]
////    [SerializeField] private float _wrongPathBias = 0.7f;

////    private MazeCell[,] _mazeGrid;

////    // Distance of each cell from the exit, computed before generation
////    private int[,] _distanceFromExit;

////    // The exit is the bottom-right corner; change this if your exit is elsewhere
////    private Vector2Int _exitCoord;

////    IEnumerator Start()
////    {
////        _exitCoord = new Vector2Int(_mazeWidth - 1, _mazeDepth - 1);

////        _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];
////        for (int x = 0; x < _mazeWidth; x++)
////            for (int z = 0; z < _mazeDepth; z++)
////                _mazeGrid[x, z] = Instantiate(_mazeCellPrefab, new Vector3(x, 0, z), Quaternion.identity);

////        // BFS on the raw grid (all walls down) to get distances from exit.
////        // We do this before carving so every cell gets a meaningful distance.
////        ComputeDistancesFromExit();

////        yield return GenerateMaze(null, _mazeGrid[0, 0]);
////    }

////    // BFS from the exit across the full uncarved grid
////    private void ComputeDistancesFromExit()
////    {
////        _distanceFromExit = new int[_mazeWidth, _mazeDepth];

////        // Fill with -1 so we can detect unvisited cells
////        for (int x = 0; x < _mazeWidth; x++)
////            for (int z = 0; z < _mazeDepth; z++)
////                _distanceFromExit[x, z] = -1;

////        Queue<Vector2Int> queue = new Queue<Vector2Int>();
////        queue.Enqueue(_exitCoord);
////        _distanceFromExit[_exitCoord.x, _exitCoord.y] = 0;

////        Vector2Int[] directions = {
////            Vector2Int.right, Vector2Int.left,
////            new Vector2Int(0, 1), new Vector2Int(0, -1)
////        };

////        while (queue.Count > 0)
////        {
////            Vector2Int current = queue.Dequeue();
////            int nextDist = _distanceFromExit[current.x, current.y] + 1;

////            foreach (var dir in directions)
////            {
////                Vector2Int neighbor = current + dir;
////                if (neighbor.x < 0 || neighbor.x >= _mazeWidth) continue;
////                if (neighbor.y < 0 || neighbor.y >= _mazeDepth) continue;
////                if (_distanceFromExit[neighbor.x, neighbor.y] != -1) continue;

////                _distanceFromExit[neighbor.x, neighbor.y] = nextDist;
////                queue.Enqueue(neighbor);
////            }
////        }
////    }

////    private IEnumerator GenerateMaze(MazeCell previousCell, MazeCell currentCell)
////    {
////        currentCell.Visit();
////        ClearWalls(previousCell, currentCell);

////        yield return new WaitForSeconds(0.05f);

////        MazeCell nextCell;
////        do
////        {
////            nextCell = GetNextUnvisitedCell(currentCell);
////            if (nextCell != null)
////                yield return GenerateMaze(currentCell, nextCell);
////        }
////        while (nextCell != null);
////    }

////    private MazeCell GetNextUnvisitedCell(MazeCell currentCell)
////    {
////        var unvisitedCells = GetUnvisitedCells(currentCell).ToList();
////        if (unvisitedCells.Count == 0) return null;

////        // Roll against the bias value.
////        // If the roll succeeds, pick the neighbor FARTHEST from the exit
////        // (i.e. the one that leads deepest into a dead-end branch).
////        // If it fails, pick randomly as before.
////        if (Random.value < _wrongPathBias)
////        {
////            return unvisitedCells
////                .OrderByDescending(c => _distanceFromExit[
////                    (int)c.transform.position.x,
////                    (int)c.transform.position.z])
////                .First();
////        }
////        else
////        {
////            return unvisitedCells.OrderBy(_ => Random.Range(1, 10)).FirstOrDefault();
////        }
////    }

////    private IEnumerable<MazeCell> GetUnvisitedCells(MazeCell currentCell)
////    {
////        int x = (int)currentCell.transform.position.x;
////        int z = (int)currentCell.transform.position.z;

////        if (x + 1 < _mazeWidth && !_mazeGrid[x + 1, z].IsVisited) yield return _mazeGrid[x + 1, z];
////        if (x - 1 >= 0 && !_mazeGrid[x - 1, z].IsVisited) yield return _mazeGrid[x - 1, z];
////        if (z + 1 < _mazeDepth && !_mazeGrid[x, z + 1].IsVisited) yield return _mazeGrid[x, z + 1];
////        if (z - 1 >= 0 && !_mazeGrid[x, z - 1].IsVisited) yield return _mazeGrid[x, z - 1];
////    }

////    private void ClearWalls(MazeCell previousCell, MazeCell currentCell)
////    {
////        if (previousCell == null) return;

////        if (previousCell.transform.position.x < currentCell.transform.position.x) { previousCell.ClearRightWall(); currentCell.ClearLeftWall(); return; }
////        if (previousCell.transform.position.x > currentCell.transform.position.x) { previousCell.ClearLeftWall(); currentCell.ClearRightWall(); return; }
////        if (previousCell.transform.position.z < currentCell.transform.position.z) { previousCell.ClearFrontWall(); currentCell.ClearBackWall(); return; }
////        if (previousCell.transform.position.z > currentCell.transform.position.z) { previousCell.ClearBackWall(); currentCell.ClearFrontWall(); return; }
////    }

////    void Update() { }
////}