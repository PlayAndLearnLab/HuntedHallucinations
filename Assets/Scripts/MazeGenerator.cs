// using UnityEngine;
// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;

// public class MazeGenerator : MonoBehaviour
// {
//     [SerializeField] private MazeCell _mazeCellPrefab;
//     [SerializeField] private int _mazeWidth;
//     [SerializeField] private int _mazeDepth;
//     [SerializeField] private GameObject _playerPrefab;
//     [SerializeField] private IntersectionDetector _intersectionDetector;

//     [Range(0f, 1f)]
//     [SerializeField] private float _wrongPathBias = 0.7f;
//     [SerializeField] private float _cellSize = 2f;

//     private GameObject _playerInstance;

//     public float CellSize => _cellSize;

//     private MazeCell[,] _mazeGrid;
//     private int[,] _distanceFromExit;
//     private Vector2Int _exitCoord;
//     private Vector3 _mazeOrigin = Vector3.zero;

//     IEnumerator Start()
//     {
//         yield return BuildMaze(_mazeOrigin);
//         TimerManager.Instance.StartTimer();
//     }

//     public MazeCell[,] GetMazeGrid() => _mazeGrid;

//     public void DestroyMaze()
//     {
//         if (_intersectionDetector != null)
//             _intersectionDetector.ClearIndicators();

//         if (_mazeGrid == null) return;
//         foreach (MazeCell cell in _mazeGrid)
//             if (cell != null) Destroy(cell.gameObject);

//         _mazeGrid = null;
//     }

//     public IEnumerator RebuildFrom(Vector3 origin)
//     {
//         _mazeOrigin = origin;
//         yield return BuildMaze(origin);
//     }

//     private IEnumerator BuildMaze(Vector3 origin)
//     {
//         _exitCoord = new Vector2Int(_mazeWidth - 1, _mazeDepth - 1);
//         _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];

//         for (int x = 0; x < _mazeWidth; x++)
//         {
//             for (int z = 0; z < _mazeDepth; z++)
//             {
//                 Vector3 pos = origin + new Vector3(x * _cellSize, 0, z * _cellSize);
//                 _mazeGrid[x, z] = Instantiate(_mazeCellPrefab, pos, Quaternion.identity);
//                 _mazeGrid[x, z].transform.SetParent(this.transform);
//                 _mazeGrid[x, z].SetGridPosition(x, z);
//             }
//         }

//         ComputeDistancesFromExit();
//         yield return GenerateMaze(null, _mazeGrid[0, 0]);

//         _mazeGrid[_exitCoord.x, _exitCoord.y].ClearFrontWall();

//         if (_intersectionDetector != null)
//             _intersectionDetector.OnMazeReady(_mazeGrid, _mazeWidth, _mazeDepth, _distanceFromExit, _cellSize);

//         if (_playerInstance == null)
//         {
//             Vector3 startPosition = _mazeGrid[0, 0].transform.position + Vector3.up * 0.5f;
//             _playerInstance = Instantiate(_playerPrefab, startPosition, Quaternion.identity);
//         }
//     }

//     private void ComputeDistancesFromExit()
//     {
//         _distanceFromExit = new int[_mazeWidth, _mazeDepth];
//         for (int x = 0; x < _mazeWidth; x++)
//             for (int z = 0; z < _mazeDepth; z++)
//                 _distanceFromExit[x, z] = -1;

//         Queue<Vector2Int> queue = new Queue<Vector2Int>();
//         queue.Enqueue(_exitCoord);
//         _distanceFromExit[_exitCoord.x, _exitCoord.y] = 0;

//         Vector2Int[] directions = {
//             Vector2Int.right, Vector2Int.left,
//             new Vector2Int(0, 1), new Vector2Int(0, -1)
//         };

//         while (queue.Count > 0)
//         {
//             Vector2Int current = queue.Dequeue();
//             int nextDist = _distanceFromExit[current.x, current.y] + 1;

//             foreach (var dir in directions)
//             {
//                 Vector2Int neighbor = current + dir;
//                 if (neighbor.x < 0 || neighbor.x >= _mazeWidth) continue;
//                 if (neighbor.y < 0 || neighbor.y >= _mazeDepth) continue;
//                 if (_distanceFromExit[neighbor.x, neighbor.y] != -1) continue;

//                 _distanceFromExit[neighbor.x, neighbor.y] = nextDist;
//                 queue.Enqueue(neighbor);
//             }
//         }
//     }

//     private IEnumerator GenerateMaze(MazeCell previousCell, MazeCell currentCell)
//     {
//         currentCell.Visit();
//         ClearWalls(previousCell, currentCell);
//         yield return new WaitForSeconds(0.01f);

//         MazeCell nextCell;
//         do
//         {
//             nextCell = GetNextUnvisitedCell(currentCell);
//             if (nextCell != null)
//                 yield return GenerateMaze(currentCell, nextCell);
//         }
//         while (nextCell != null);
//     }

    

//     private MazeCell GetNextUnvisitedCell(MazeCell currentCell)
//     {
//         var unvisitedCells = GetUnvisitedCells(currentCell).ToList();
//         if (unvisitedCells.Count == 0) return null;

//         if (Random.value < _wrongPathBias)
//             return unvisitedCells.OrderByDescending(c => _distanceFromExit[c.GridX, c.GridZ]).First();
//         else
//             return unvisitedCells.OrderBy(_ => Random.Range(1, 10)).FirstOrDefault();
//     }

//     private IEnumerable<MazeCell> GetUnvisitedCells(MazeCell currentCell)
//     {
//         int x = currentCell.GridX;
//         int z = currentCell.GridZ;

//         if (x + 1 < _mazeWidth  && !_mazeGrid[x + 1, z].IsVisited) yield return _mazeGrid[x + 1, z];
//         if (x - 1 >= 0          && !_mazeGrid[x - 1, z].IsVisited) yield return _mazeGrid[x - 1, z];
//         if (z + 1 < _mazeDepth  && !_mazeGrid[x, z + 1].IsVisited) yield return _mazeGrid[x, z + 1];
//         if (z - 1 >= 0          && !_mazeGrid[x, z - 1].IsVisited) yield return _mazeGrid[x, z - 1];
//     }

//     private void ClearWalls(MazeCell previousCell, MazeCell currentCell)
//     {
//         if (previousCell == null) return;

//         if (previousCell.GridX < currentCell.GridX) { previousCell.ClearRightWall(); currentCell.ClearLeftWall();  return; }
//         if (previousCell.GridX > currentCell.GridX) { previousCell.ClearLeftWall();  currentCell.ClearRightWall(); return; }
//         if (previousCell.GridZ < currentCell.GridZ) { previousCell.ClearFrontWall(); currentCell.ClearBackWall();  return; }
//         if (previousCell.GridZ > currentCell.GridZ) { previousCell.ClearBackWall();  currentCell.ClearFrontWall(); return; }
//     }

//     void Update() { }
// }


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
    [SerializeField] private IntersectionDetector _intersectionDetector;

    [Range(0f, 1f)]
    [SerializeField] private float _wrongPathBias = 0.7f;
    [SerializeField] private float _cellSize = 2f;

    private GameObject _playerInstance;

    // Exposed so PenaltyMazeManager can use it for snapping
    public float CellSize => _cellSize;

    private MazeCell[,] _mazeGrid;
    private int[,] _distanceFromExit;
    private Vector2Int _exitCoord;
    private Vector3 _mazeOrigin = Vector3.zero;

    IEnumerator Start()
    {
        yield return BuildMaze(_mazeOrigin);
        TimerManager.Instance.StartTimer();
    }

    public MazeCell[,] GetMazeGrid() => _mazeGrid;

    // Destroys all current maze cells
    public void DestroyMaze()
    {
        if (_intersectionDetector != null)
            _intersectionDetector.ClearIndicators();

        if (_mazeGrid == null) return;
        foreach (MazeCell cell in _mazeGrid)
            if (cell != null) Destroy(cell.gameObject);

        _mazeGrid = null;
    }

    // Rebuilds a full-size maze with its origin at the player's current position
    public IEnumerator RebuildFrom(Vector3 origin)
    {
        _mazeOrigin = origin;
        yield return BuildMaze(origin);
    }

    private IEnumerator BuildMaze(Vector3 origin)
    {
        _exitCoord = new Vector2Int(_mazeWidth - 1, _mazeDepth - 1);
        _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];

        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
                Vector3 pos = origin + new Vector3(x * _cellSize, 0, z * _cellSize);
                _mazeGrid[x, z] = Instantiate(_mazeCellPrefab, pos, Quaternion.identity);
                _mazeGrid[x, z].transform.localScale = Vector3.one * _cellSize;
                _mazeGrid[x, z].SetGridPosition(x, z);
            }
        }

        
        yield return GenerateMaze(null, _mazeGrid[0, 0]);
        ComputeDistancesFromExit();
        VisualizeCorrectPath();

        _mazeGrid[_exitCoord.x, _exitCoord.y].ClearFrontWall();

        if (_intersectionDetector != null)
            _intersectionDetector.OnMazeReady(_mazeGrid, _mazeWidth, _mazeDepth, _distanceFromExit, _cellSize);

        if (_playerInstance == null)
        {
            Vector3 startPosition = _mazeGrid[0, 0].transform.position + Vector3.up * 0.5f;
            _playerInstance = Instantiate(_playerPrefab, startPosition, Quaternion.identity);
        }

        _mazeGrid[0, 0].DebugWalls();
        _mazeGrid[1, 0].DebugWalls();
        _mazeGrid[0, 1].DebugWalls();
    }

    // --- All existing methods unchanged below ---

    private void ComputeDistancesFromExit()
    {
        _distanceFromExit = new int[_mazeWidth, _mazeDepth];
        for (int x = 0; x < _mazeWidth; x++)
            for (int z = 0; z < _mazeDepth; z++)
                _distanceFromExit[x, z] = -1;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(_exitCoord);
        _distanceFromExit[_exitCoord.x, _exitCoord.y] = 0;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentDist = _distanceFromExit[current.x, current.y];
            MazeCell currentCell = _mazeGrid[current.x, current.y];

            // Check Right (+X) — Only move if there is no right wall
            if (current.x + 1 < _mazeWidth && !currentCell.HasRightWall() && _distanceFromExit[current.x + 1, current.y] == -1)
            {
                _distanceFromExit[current.x + 1, current.y] = currentDist + 1;
                queue.Enqueue(new Vector2Int(current.x + 1, current.y));
            }
            // Check Left (-X) — Only move if there is no left wall
            if (current.x - 1 >= 0 && !currentCell.HasLeftWall() && _distanceFromExit[current.x - 1, current.y] == -1)
            {
                _distanceFromExit[current.x - 1, current.y] = currentDist + 1;
                queue.Enqueue(new Vector2Int(current.x - 1, current.y));
            }
            // Check Front (+Z) — Only move if there is no front wall
            if (current.y + 1 < _mazeDepth && !currentCell.HasFrontWall() && _distanceFromExit[current.x, current.y + 1] == -1)
            {
                _distanceFromExit[current.x, current.y + 1] = currentDist + 1;
                queue.Enqueue(new Vector2Int(current.x, current.y + 1));
            }
            // Check Back (-Z) — Only move if there is no back wall
            if (current.y - 1 >= 0 && !currentCell.HasBackWall() && _distanceFromExit[current.x, current.y - 1] == -1)
            {
                _distanceFromExit[current.x, current.y - 1] = currentDist + 1;
                queue.Enqueue(new Vector2Int(current.x, current.y - 1));
            }
        }

        // Vector2Int[] directions = {
        //     Vector2Int.right, Vector2Int.left,
        //     new Vector2Int(0, 1), new Vector2Int(0, -1)
        // };

        // while (queue.Count > 0)
        // {
        //     Vector2Int current = queue.Dequeue();
        //     int nextDist = _distanceFromExit[current.x, current.y] + 1;

        //     foreach (var dir in directions)
        //     {
        //         Vector2Int neighbor = current + dir;
        //         if (neighbor.x < 0 || neighbor.x >= _mazeWidth) continue;
        //         if (neighbor.y < 0 || neighbor.y >= _mazeDepth) continue;
        //         if (_distanceFromExit[neighbor.x, neighbor.y] != -1) continue;

        //         _distanceFromExit[neighbor.x, neighbor.y] = nextDist;
        //         queue.Enqueue(neighbor);
        //     }
        // }
    }

    public void VisualizeCorrectPath()
    {
        // Start at the exit
        int currX = _exitCoord.x;
        int currZ = _exitCoord.y;

        // Safety check if BFS hasn't run or failed
        if (_distanceFromExit == null || _distanceFromExit[currX, currZ] == -1) return;

        int maxIterations = _mazeWidth * _mazeDepth; 
        int safetyCounter = 0;

        // Follow the breadcrumbs all the way down to distance 0
        while (_distanceFromExit[currX, currZ] > 0 && safetyCounter < maxIterations)
        {
            safetyCounter++;

            // Color the current cell path floor (Yellow/Gold makes a clear path indicator)
            _mazeGrid[currX, currZ].HighlightPath(Color.yellow);

            int currentDist = _distanceFromExit[currX, currZ];
            MazeCell currentCell = _mazeGrid[currX, currZ];

            // 1. Check if the path came from the Left (-X neighbor means current cell has no Left wall)
            if (currX - 1 >= 0 && !currentCell.HasLeftWall() && _distanceFromExit[currX - 1, currZ] == currentDist - 1)
            {
                currX--;
                continue;
            }
            // 2. Check if the path came from the Right (+X neighbor means current cell has no Right wall)
            if (currX + 1 < _mazeWidth && !currentCell.HasRightWall() && _distanceFromExit[currX + 1, currZ] == currentDist - 1)
            {
                currX++;
                continue;
            }
            // 3. Check if the path came from the Back (-Z neighbor means current cell has no Back wall)
            if (currZ - 1 >= 0 && !currentCell.HasBackWall() && _distanceFromExit[currX, currZ - 1] == currentDist - 1)
            {
                currZ--;
                continue;
            }
            // 4. Check if the path came from the Front (+Z neighbor means current cell has no Front wall)
            if (currZ + 1 < _mazeDepth && !currentCell.HasFrontWall() && _distanceFromExit[currX, currZ + 1] == currentDist - 1)
            {
                currZ++;
                continue;
            }

            // If it gets stuck without finding a descending neighbor, break out
            break;
        }

        // Highlight the starting origin point cell too
        _mazeGrid[currX, currZ].HighlightPath(Color.yellow);
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
            return unvisitedCells.OrderByDescending(c => _distanceFromExit[c.GridX, c.GridZ]).First();
        else
            return unvisitedCells.OrderBy(_ => Random.Range(1, 10)).FirstOrDefault();
    }

    private IEnumerable<MazeCell> GetUnvisitedCells(MazeCell currentCell)
    {
        int x = currentCell.GridX;
        int z = currentCell.GridZ;

        if (x + 1 < _mazeWidth  && !_mazeGrid[x + 1, z].IsVisited) yield return _mazeGrid[x + 1, z];
        if (x - 1 >= 0          && !_mazeGrid[x - 1, z].IsVisited) yield return _mazeGrid[x - 1, z];
        if (z + 1 < _mazeDepth  && !_mazeGrid[x, z + 1].IsVisited) yield return _mazeGrid[x, z + 1];
        if (z - 1 >= 0          && !_mazeGrid[x, z - 1].IsVisited) yield return _mazeGrid[x, z - 1];
    }

    private void ClearWalls(MazeCell previousCell, MazeCell currentCell)
    {
        if (previousCell == null) return;

        if (previousCell.GridX < currentCell.GridX) { previousCell.ClearRightWall(); currentCell.ClearLeftWall();  return; }
        if (previousCell.GridX > currentCell.GridX) { previousCell.ClearLeftWall();  currentCell.ClearRightWall(); return; }
        if (previousCell.GridZ < currentCell.GridZ) { previousCell.ClearFrontWall(); currentCell.ClearBackWall();  return; }
        if (previousCell.GridZ > currentCell.GridZ) { previousCell.ClearBackWall();  currentCell.ClearFrontWall(); return; }
    }

    void Update() { }
}

// using UnityEngine;
// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;

// public class MazeGenerator : MonoBehaviour
// {
//     [SerializeField] private MazeCell _mazeCellPrefab;
//     [SerializeField] private int _mazeWidth;
//     [SerializeField] private int _mazeDepth;
//     [SerializeField] private GameObject _playerPrefab;
//     [SerializeField] private IntersectionDetector _intersectionDetector;

//     [Range(0f, 1f)]
//     [SerializeField] private float _wrongPathBias = 0.7f;

//     private GameObject _playerInstance;

//     // Dynamically calculated at runtime from your mesh render bounds
//     private float _cellWidth = 2f;
//     private float _cellDepth = 2f;

//     // Exposed so PenaltyMazeManager can handle snapping relative to distinct sizes
//     public float CellWidth => _cellWidth;
//     public float CellDepth => _cellDepth;
    
//     // Fallback getter for backward-compatibility with other scripts
//     public float CellSize => _cellWidth; 

//     private MazeCell[,] _mazeGrid;
//     private int[,] _distanceFromExit;
//     private Vector2Int _exitCoord;
//     private Vector3 _mazeOrigin = Vector3.zero;

//     IEnumerator Start()
//     {
//         CalculatePrefabDimensions();
//         yield return BuildMaze(_mazeOrigin);
//         TimerManager.Instance.StartTimer();
//     }

//     private void CalculatePrefabDimensions()
//     {
//         if (_mazeCellPrefab == null) return;

//         // Automatically inspects the mesh boundaries of your cell prefab asset
//         Renderer renderer = _mazeCellPrefab.GetComponent<Renderer>() ?? _mazeCellPrefab.GetComponentInChildren<Renderer>();
//         if (renderer != null)
//         {
//             _cellWidth = renderer.bounds.size.x;
//             _cellDepth = renderer.bounds.size.z;
//         }
//     }

//     public void DestroyMaze()
//     {
//         if (_mazeGrid == null) return;
//         foreach (MazeCell cell in _mazeGrid)
//             if (cell != null) Destroy(cell.gameObject);

//         _mazeGrid = null;
//     }

//     public IEnumerator RebuildFrom(Vector3 origin)
//     {
//         _mazeOrigin = origin;
//         yield return BuildMaze(origin);
//     }

//     private IEnumerator BuildMaze(Vector3 origin)
//     {
//         _exitCoord = new Vector2Int(_mazeWidth - 1, _mazeDepth - 1);
//         _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];

//         for (int x = 0; x < _mazeWidth; x++)
//         {
//             for (int z = 0; z < _mazeDepth; z++)
//             {
//                 // Align cells edge-to-edge perfectly using structural asset dimensions
//                 Vector3 pos = origin + new Vector3(x * _cellWidth, 0, z * _cellDepth);
//                 _mazeGrid[x, z] = Instantiate(_mazeCellPrefab, pos, Quaternion.identity);
//                 _mazeGrid[x, z].transform.SetParent(this.transform);
//                 _mazeGrid[x, z].SetGridPosition(x, z);
//             }
//         }

//         ComputeDistancesFromExit();
//         yield return GenerateMaze(null, _mazeGrid[0, 0]);

//         _mazeGrid[_exitCoord.x, _exitCoord.y].ClearFrontWall();

//         // FIX: Pass all 6 arguments to match IntersectionDetector's updated signature
//         if (_intersectionDetector != null)
//             _intersectionDetector.OnMazeReady(_mazeGrid, _mazeWidth, _mazeDepth, _distanceFromExit, _cellWidth, _cellDepth);

//         if (_playerInstance == null)
//         {
//             Vector3 startPosition = _mazeGrid[0, 0].transform.position + Vector3.up * 0.5f;
//             _playerInstance = Instantiate(_playerPrefab, startPosition, Quaternion.identity);
//         }
//     }

//     private void ComputeDistancesFromExit()
//     {
//         _distanceFromExit = new int[_mazeWidth, _mazeDepth];
//         for (int x = 0; x < _mazeWidth; x++)
//             for (int z = 0; z < _mazeDepth; z++)
//                 _distanceFromExit[x, z] = -1;

//         Queue<Vector2Int> queue = new Queue<Vector2Int>();
//         queue.Enqueue(_exitCoord);
//         _distanceFromExit[_exitCoord.x, _exitCoord.y] = 0;

//         Vector2Int[] directions = {
//             Vector2Int.right, Vector2Int.left,
//             new Vector2Int(0, 1), new Vector2Int(0, -1)
//         };

//         while (queue.Count > 0)
//         {
//             Vector2Int current = queue.Dequeue();
//             int nextDist = _distanceFromExit[current.x, current.y] + 1;

//             foreach (var dir in directions)
//             {
//                 Vector2Int neighbor = current + dir;
//                 if (neighbor.x < 0 || neighbor.x >= _mazeWidth) continue;
//                 if (neighbor.y < 0 || neighbor.y >= _mazeDepth) continue;
//                 if (_distanceFromExit[neighbor.x, neighbor.y] != -1) continue;

//                 _distanceFromExit[neighbor.x, neighbor.y] = nextDist;
//                 queue.Enqueue(neighbor);
//             }
//         }
//     }

//     private IEnumerator GenerateMaze(MazeCell previousCell, MazeCell currentCell)
//     {
//         currentCell.Visit();
//         ClearWalls(previousCell, currentCell);
//         yield return new WaitForSeconds(0.05f);

//         MazeCell nextCell;
//         do
//         {
//             nextCell = GetNextUnvisitedCell(currentCell);
//             if (nextCell != null)
//                 yield return GenerateMaze(currentCell, nextCell);
//         }
//         while (nextCell != null);
//     }

//     private MazeCell GetNextUnvisitedCell(MazeCell currentCell)
//     {
//         var unvisitedCells = GetUnvisitedCells(currentCell).ToList();
//         if (unvisitedCells.Count == 0) return null;

//         if (Random.value < _wrongPathBias)
//             return unvisitedCells.OrderByDescending(c => _distanceFromExit[c.GridX, c.GridZ]).First();
//         else
//             return unvisitedCells.OrderBy(_ => Random.Range(1, 10)).FirstOrDefault();
//     }

//     private IEnumerable<MazeCell> GetUnvisitedCells(MazeCell currentCell)
//     {
//         int x = currentCell.GridX;
//         int z = currentCell.GridZ;

//         if (x + 1 < _mazeWidth  && !_mazeGrid[x + 1, z].IsVisited) yield return _mazeGrid[x + 1, z];
//         if (x - 1 >= 0          && !_mazeGrid[x - 1, z].IsVisited) yield return _mazeGrid[x - 1, z];
//         if (z + 1 < _mazeDepth  && !_mazeGrid[x, z + 1].IsVisited) yield return _mazeGrid[x, z + 1];
//         if (z - 1 >= 0          && !_mazeGrid[x, z - 1].IsVisited) yield return _mazeGrid[x, z - 1];
//     }

//     private void ClearWalls(MazeCell previousCell, MazeCell currentCell)
//     {
//         if (previousCell == null) return;

//         if (previousCell.GridX < currentCell.GridX) { previousCell.ClearRightWall(); currentCell.ClearLeftWall();  return; }
//         if (previousCell.GridX > currentCell.GridX) { previousCell.ClearLeftWall();  currentCell.ClearRightWall(); return; }
//         if (previousCell.GridZ < currentCell.GridZ) { previousCell.ClearFrontWall(); currentCell.ClearBackWall();  return; }
//         if (previousCell.GridZ > currentCell.GridZ) { previousCell.ClearBackWall();  currentCell.ClearFrontWall(); return; }
//     }
// }

// using UnityEngine;
// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;

// public class MazeGenerator : MonoBehaviour
// {
//     [SerializeField] private MazeCell _mazeCellPrefab;
//     [SerializeField] private int _mazeWidth;
//     [SerializeField] private int _mazeDepth;
//     [SerializeField] private GameObject _playerPrefab;

//     [Range(0f, 1f)] [SerializeField] private float _wrongPathBias = 0.7f;

//     [SerializeField] private float _cellSize = 2f;

//     public float CellSize => _cellSize;

//     private MazeCell[,] _mazeGrid;
//     private int[,] _distanceFromExit;
//     private Vector2Int _exitCoord;

//     [SerializeField] private IntersectionDetector _intersectionDetector;

//     private Vector3 _mazeOrigin = Vector3.zero;

//     void Start()
//     {
//         _exitCoord = new Vector2Int(_mazeWidth - 1, _mazeDepth - 1);

//         _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];

//         for (int x = 0; x < _mazeWidth; x++)
//         {
//             for (int z = 0; z < _mazeDepth; z++)
//             {
//                 _mazeGrid[x, z] = Instantiate(
//                     _mazeCellPrefab,
//                     new Vector3(x * _cellSize, 0, z * _cellSize),
//                     Quaternion.identity);

//                 _mazeGrid[x, z].transform.localScale = Vector3.one * _cellSize;

//                 // Store grid coordinates on the cell so we never rely on world position for indexing
//                 _mazeGrid[x, z].SetGridPosition(x, z);
//             }
//         }

//         ComputeDistancesFromExit();

//         GenerateMaze(null, _mazeGrid[0, 0]);

//         _mazeGrid[_exitCoord.x, _exitCoord.y].ClearFrontWall();

//         Vector3 startPosition = _mazeGrid[0, 0].transform.position + Vector3.up * 0.5f;
//         Instantiate(_playerPrefab, startPosition, Quaternion.identity);

//         if (_intersectionDetector != null)
//             _intersectionDetector.OnMazeReady(_mazeGrid, _mazeWidth, _mazeDepth, _distanceFromExit, _cellSize);
//     }

//     public void DestroyMaze()
//     {
//         if (_mazeGrid == null) return;
//         foreach (MazeCell cell in _mazeGrid)
//             if (cell != null) Destroy(cell.gameObject);

//         _mazeGrid = null;
//     }

//     public IEnumerator RebuildFrom(Vector3 origin)
//     {
//         _mazeOrigin = origin;
//         yield return BuildMaze(origin);
//     }

//     private void ComputeDistancesFromExit()
//     {
//         _distanceFromExit = new int[_mazeWidth, _mazeDepth];

//         for (int x = 0; x < _mazeWidth; x++)
//             for (int z = 0; z < _mazeDepth; z++)
//                 _distanceFromExit[x, z] = -1;

//         Queue<Vector2Int> queue = new Queue<Vector2Int>();
//         queue.Enqueue(_exitCoord);
//         _distanceFromExit[_exitCoord.x, _exitCoord.y] = 0;

//         Vector2Int[] directions = {
//             Vector2Int.right, Vector2Int.left,
//             new Vector2Int(0, 1), new Vector2Int(0, -1)
//         };

//         while (queue.Count > 0)
//         {
//             Vector2Int current = queue.Dequeue();
//             int nextDist = _distanceFromExit[current.x, current.y] + 1;

//             foreach (var dir in directions)
//             {
//                 Vector2Int neighbor = current + dir;
//                 if (neighbor.x < 0 || neighbor.x >= _mazeWidth) continue;
//                 if (neighbor.y < 0 || neighbor.y >= _mazeDepth) continue;
//                 if (_distanceFromExit[neighbor.x, neighbor.y] != -1) continue;

//                 _distanceFromExit[neighbor.x, neighbor.y] = nextDist;
//                 queue.Enqueue(neighbor);
//             }
//         }
//     }

//     private void GenerateMaze(MazeCell previousCell, MazeCell currentCell)
//     {
//         currentCell.Visit();
//         ClearWalls(previousCell, currentCell);

//         new WaitForSeconds(0.05f);

//         MazeCell nextCell;
//         do
//         {
//             nextCell = GetNextUnvisitedCell(currentCell);
//             if (nextCell != null)
//                 GenerateMaze(currentCell, nextCell);
//         }
//         while (nextCell != null);
//     }

//     private MazeCell GetNextUnvisitedCell(MazeCell currentCell)
//     {
//         var unvisitedCells = GetUnvisitedCells(currentCell).ToList();
//         if (unvisitedCells.Count == 0) return null;

//         if (Random.value < _wrongPathBias)
//         {
//             return unvisitedCells
//                 .OrderByDescending(c => _distanceFromExit[c.GridX, c.GridZ])
//                 .First();
//         }
//         else
//         {
//             return unvisitedCells.OrderBy(_ => Random.Range(1, 10)).FirstOrDefault();
//         }
//     }

//     private IEnumerable<MazeCell> GetUnvisitedCells(MazeCell currentCell)
//     {
//         // Use grid coordinates, not world position
//         int x = currentCell.GridX;
//         int z = currentCell.GridZ;

//         if (x + 1 < _mazeWidth && !_mazeGrid[x + 1, z].IsVisited) yield return _mazeGrid[x + 1, z];
//         if (x - 1 >= 0 && !_mazeGrid[x - 1, z].IsVisited) yield return _mazeGrid[x - 1, z];
//         if (z + 1 < _mazeDepth && !_mazeGrid[x, z + 1].IsVisited) yield return _mazeGrid[x, z + 1];
//         if (z - 1 >= 0 && !_mazeGrid[x, z - 1].IsVisited) yield return _mazeGrid[x, z - 1];
//     }

//     private void ClearWalls(MazeCell previousCell, MazeCell currentCell)
//     {
//         if (previousCell == null) return;

//         if (previousCell.GridX < currentCell.GridX) { previousCell.ClearRightWall(); currentCell.ClearLeftWall(); return; }
//         if (previousCell.GridX > currentCell.GridX) { previousCell.ClearLeftWall(); currentCell.ClearRightWall(); return; }
//         if (previousCell.GridZ < currentCell.GridZ) { previousCell.ClearFrontWall(); currentCell.ClearBackWall(); return; }
//         if (previousCell.GridZ > currentCell.GridZ) { previousCell.ClearBackWall(); currentCell.ClearFrontWall(); return; }
//     }

//     void Update() { }
// }

