using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MazeGenerator : MonoBehaviour
{
    // --- Cell prefabs ---
    // Assign one or more cell prefabs. If more than one, they are chosen randomly.
    [SerializeField] private MazeCell[] _mazeCellPrefabs;

    [SerializeField] private int _mazeWidth;
    [SerializeField] private int _mazeDepth;
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private IntersectionDetector _intersectionDetector;

    [Range(0f, 1f)]
    [SerializeField] private float _wrongPathBias = 0.7f;

    // Explicit dimensions you set in the Unity Inspector to enforce layout size
    [SerializeField] private float _cellWidth = 2f;
    [SerializeField] private float _cellDepth = 2f;
    [SerializeField] private float _cellHeight = 3f; // <-- NEW: Enforced wall/cell height

    // Public getters to allow PenaltyMazeManager to read your custom sizes
    public float CellWidth => _cellWidth;
    public float CellDepth => _cellDepth;
    public float CellHeight => _cellHeight; // <-- NEW
    public float CellSize => (_cellWidth + _cellDepth) * 0.5f;

    private GameObject _playerInstance;
    private MazeCell[,] _mazeGrid;
    private int[,] _distanceFromExit;
    private Vector2Int _exitCoord;
    private Vector3 _mazeOrigin = Vector3.zero;

    IEnumerator Start()
    {
        yield return BuildMaze(_mazeOrigin);
        TimerManager.Instance.StartTimer();
    }

    public void DestroyMaze()
    {
        if (_mazeGrid == null) return;

        foreach (MazeCell cell in _mazeGrid)
        {
            if (cell != null) Destroy(cell.gameObject);
        }

        _mazeGrid = null;

        if (_intersectionDetector != null)
        {
            _intersectionDetector.ClearIndicators();
        }
    }

    public IEnumerator RebuildFrom(Vector3 origin)
    {
        _mazeOrigin = origin;
        yield return BuildMaze(origin);
    }

    public MazeCell[,] GetMazeGrid()
    {
        return _mazeGrid;
    }

    private IEnumerator BuildMaze(Vector3 origin)
    {
        _exitCoord = new Vector2Int(_mazeWidth - 1, _mazeDepth - 1);
        _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];

        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
                // Target anchor position in world space
                Vector3 targetCellCenter = origin + new Vector3(x * _cellWidth, 0, z * _cellDepth);

                MazeCell prefab = _mazeCellPrefabs[Random.Range(0, _mazeCellPrefabs.Length)];
                
                // Instantiate at zero first to isolate local bounds safely
                MazeCell instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
                instance.transform.SetParent(this.transform);
                instance.SetGridPosition(x, z);

                // Reset scale to default to measure native boundaries cleanly
                instance.transform.localScale = Vector3.one;

                // Look for a designated bounding child, otherwise fall back to the main group renderer
                Transform floorTransform = instance.transform.Find("Floor");
                Renderer targetRenderer = (floorTransform != null) ? floorTransform.GetComponent<Renderer>() : null;
                
                if (targetRenderer == null)
                {
                    targetRenderer = instance.GetComponent<Renderer>() ?? instance.GetComponentInChildren<Renderer>();
                }

                if (targetRenderer != null)
                {
                    // 1. COMPUTE THE SCALE FACTOR (Including Height!)
                    Vector3 currentBounds = targetRenderer.bounds.size;
                    
                    float targetScaleX = currentBounds.x > 0 ? (_cellWidth / currentBounds.x) : 1f;
                    float targetScaleZ = currentBounds.z > 0 ? (_cellDepth / currentBounds.z) : 1f;
                    
                    // NEW: Compute Y scale scaling factor based on the main renderer mesh height.
                    // If targetRenderer is just the floor, we'll check the parent hierarchy's combined meshes.
                    float nativeHeight = currentBounds.y;
                    if (floorTransform != null && nativeHeight < 0.1f) // Floor plane is likely flat, measure full asset height instead
                    {
                        Bounds totalBounds = new Bounds(instance.transform.position, Vector3.zero);
                        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
                        {
                            totalBounds.Encapsulate(r.bounds);
                        }
                        nativeHeight = totalBounds.size.y;
                    }

                    float targetScaleY = nativeHeight > 0 ? (_cellHeight / nativeHeight) : 1f;

                    // Apply the complete 3D scale adjustments
                    instance.transform.localScale = new Vector3(targetScaleX, targetScaleY, targetScaleZ);

                    // 2. COMPUTE PIVOT OFFSET CORRECTION
                    Physics.SyncTransforms(); // Ensure bounds update with new scale factors
                    Vector3 localCenterOffset = targetRenderer.bounds.center - instance.transform.position;
                    
                    // Keep the Y position completely intact so floor levels remain matched
                    localCenterOffset.y = 0f;

                    // Shift the position backward by its center discrepancy so it fits perfectly inside the slot
                    instance.transform.position = targetCellCenter - localCenterOffset;
                }
                else
                {
                    // Absolute fallback if no mesh renderers are present
                    instance.transform.localScale = new Vector3(_cellWidth, _cellHeight, _cellDepth);
                    instance.transform.position = targetCellCenter;
                }

                _mazeGrid[x, z] = instance;
            }
        }

        Physics.SyncTransforms();

        ComputeDistancesFromExit();
        yield return GenerateMaze(null, _mazeGrid[0, 0]);

        _mazeGrid[_exitCoord.x, _exitCoord.y].ClearFrontWall();

        if (_intersectionDetector != null)
        {
            _intersectionDetector.OnMazeReady(_mazeGrid, _mazeWidth, _mazeDepth, _distanceFromExit, _cellWidth, _cellDepth);
        }

        if (_playerInstance == null)
        {
            Vector3 startPosition = _mazeGrid[0, 0].transform.position + Vector3.up * 0.5f;
            _playerInstance = Instantiate(_playerPrefab, startPosition, Quaternion.identity);
        }
    }

    private void ComputeDistancesFromExit()
    {
        _distanceFromExit = new int[_mazeWidth, _mazeDepth];

        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
                _distanceFromExit[x, z] = -1;
            }
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(_exitCoord);
        _distanceFromExit[_exitCoord.x, _exitCoord.y] = 0;

        Vector2Int[] directions = {
            Vector2Int.right,
            Vector2Int.left,
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int nextDist = _distanceFromExit[current.x, current.y] + 1;

            foreach (var dir in directions)
            {
                Vector2Int neighbor = current + dir;

                if (neighbor.x < 0 || neighbor.x >= _mazeWidth || neighbor.y < 0 || neighbor.y >= _mazeDepth)
                    continue;

                if (_distanceFromExit[neighbor.x, neighbor.y] != -1)
                    continue;

                _distanceFromExit[neighbor.x, neighbor.y] = nextDist;
                queue.Enqueue(neighbor);
            }
        }
    }

    private IEnumerator GenerateMaze(MazeCell previousCell, MazeCell currentCell)
    {
        currentCell.Visit();

        if (previousCell != null)
        {
            ClearWalls(previousCell, currentCell);
        }

        yield return new WaitForSeconds(0.01f);

        MazeCell nextCell;
        do
        {
            nextCell = GetNextUnvisitedCell(currentCell);
            if (nextCell != null)
            {
                yield return GenerateMaze(currentCell, nextCell);
            }
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
        if (previousCell == null || currentCell == null) return;

        int deltaX = currentCell.GridX - previousCell.GridX;
        int deltaZ = currentCell.GridZ - previousCell.GridZ;

        if (Mathf.Abs(deltaX) + Mathf.Abs(deltaZ) != 1) return;

        if (deltaX == 1)  { previousCell.ClearRightWall(); currentCell.ClearLeftWall();  return; }
        if (deltaX == -1) { previousCell.ClearLeftWall();  currentCell.ClearRightWall(); return; }
        if (deltaZ == 1)  { previousCell.ClearFrontWall(); currentCell.ClearBackWall();  return; }
        if (deltaZ == -1) { previousCell.ClearBackWall();  currentCell.ClearFrontWall(); return; }
    }
}

// VERSION WITH CELL SCALING (THAT WORKS!!!!)

// using UnityEngine;
// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;

// public class MazeGenerator : MonoBehaviour
// {
//     // --- Cell prefabs ---
//     // Assign one or more cell prefabs. If more than one, they are chosen randomly.
//     [SerializeField] private MazeCell[] _mazeCellPrefabs;

//     [SerializeField] private int _mazeWidth;
//     [SerializeField] private int _mazeDepth;
//     [SerializeField] private GameObject _playerPrefab;
//     [SerializeField] private IntersectionDetector _intersectionDetector;

//     [Range(0f, 1f)]
//     [SerializeField] private float _wrongPathBias = 0.7f;

//     // Explicit dimensions you set in the Unity Inspector to enforce layout size
//     [SerializeField] private float _cellWidth = 2f;
//     [SerializeField] private float _cellDepth = 2f;

//     // Public getters to allow PenaltyMazeManager to read your custom sizes
//     public float CellWidth => _cellWidth;
//     public float CellDepth => _cellDepth;
//     public float CellSize => (_cellWidth + _cellDepth) * 0.5f;

//     private GameObject _playerInstance;
//     private MazeCell[,] _mazeGrid;
//     private int[,] _distanceFromExit;
//     private Vector2Int _exitCoord;
//     private Vector3 _mazeOrigin = Vector3.zero;

//     IEnumerator Start()
//     {
//         yield return BuildMaze(_mazeOrigin);
//         TimerManager.Instance.StartTimer();
//     }

//     public void DestroyMaze()
//     {
//         if (_mazeGrid == null) return;

//         foreach (MazeCell cell in _mazeGrid)
//         {
//             if (cell != null) Destroy(cell.gameObject);
//         }

//         _mazeGrid = null;

//         if (_intersectionDetector != null)
//         {
//             _intersectionDetector.ClearIndicators();
//         }
//     }

//     public IEnumerator RebuildFrom(Vector3 origin)
//     {
//         _mazeOrigin = origin;
//         yield return BuildMaze(origin);
//     }

//     public MazeCell[,] GetMazeGrid()
//     {
//         return _mazeGrid;
//     }

//     private IEnumerator BuildMaze(Vector3 origin)
//     {
//         _exitCoord = new Vector2Int(_mazeWidth - 1, _mazeDepth - 1);
//         _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];

//         for (int x = 0; x < _mazeWidth; x++)
//         {
//             for (int z = 0; z < _mazeDepth; z++)
//             {
//                 // Target anchor position in world space
//                 Vector3 targetCellCenter = origin + new Vector3(x * _cellWidth, 0, z * _cellDepth);

//                 MazeCell prefab = _mazeCellPrefabs[Random.Range(0, _mazeCellPrefabs.Length)];
                
//                 // Instantiate at zero first to isolate local bounds safely
//                 MazeCell instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
//                 instance.transform.SetParent(this.transform);
//                 instance.SetGridPosition(x, z);

//                 // Reset scale to default to measure native boundaries cleanly
//                 instance.transform.localScale = Vector3.one;

//                 Transform footprint = instance.transform.Find("Footprint");
//                 if (footprint != null && footprint.GetComponent<BoxCollider>() != null)
//                 {
//                     Vector3 nativeSize = footprint.GetComponent<BoxCollider>().size;

//                     float targetScaleX = nativeSize.x > 0 ? (_cellWidth / nativeSize.x) : 1f;
//                     float targetScaleZ = nativeSize.z > 0 ? (_cellDepth / nativeSize.z) : 1f;

//                     instance.transform.localScale = new Vector3(targetScaleX, 1f, targetScaleZ);
//                 } else
//                 {
//                     // Fallback if no footprint collider is present
//                     instance.transform.localScale = new Vector3(_cellWidth, 1f, _cellDepth);
//                 }

//                 // Look for a designated bounding child, otherwise fall back to the main group renderer
//                 Transform floorTransform = instance.transform.Find("Floor");
//                 Renderer targetRenderer = (floorTransform != null) ? floorTransform.GetComponent<Renderer>() : null;
                
//                 if (targetRenderer == null)
//                 {
//                     targetRenderer = instance.GetComponent<Renderer>() ?? instance.GetComponentInChildren<Renderer>();
//                 }

//                 if (targetRenderer != null)
//                 {
//                     // 1. COMPUTE THE SCALE FACTOR
//                     Vector3 currentBounds = targetRenderer.bounds.size;
//                     float targetScaleX = currentBounds.x > 0 ? (_cellWidth / currentBounds.x) : 1f;
//                     float targetScaleZ = currentBounds.z > 0 ? (_cellDepth / currentBounds.z) : 1f;

//                     instance.transform.localScale = new Vector3(targetScaleX, 1f, targetScaleZ);

//                     // 2. COMPUTE PIVOT OFFSET CORRECTION
//                     // After scaling, check where the renderer's center ended up relative to the root pivot
//                     Physics.SyncTransforms(); // Ensure bounds update with new scale factors
//                     Vector3 localCenterOffset = targetRenderer.bounds.center - instance.transform.position;
                    
//                     // Keep the Y position completely intact so floor levels remain matched
//                     localCenterOffset.y = 0f;

//                     // Shift the position backward by its center discrepancy so it fits perfectly inside the slot
//                     instance.transform.position = targetCellCenter - localCenterOffset;
//                 }
//                 else
//                 {
//                     // Absolute fallback if no mesh renderers are present
//                     instance.transform.position = targetCellCenter;
//                 }

//                 _mazeGrid[x, z] = instance;
//             }
//         }

//         Physics.SyncTransforms();

//         ComputeDistancesFromExit();
//         yield return GenerateMaze(null, _mazeGrid[0, 0]);

//         _mazeGrid[_exitCoord.x, _exitCoord.y].ClearFrontWall();

//         if (_intersectionDetector != null)
//         {
//             _intersectionDetector.OnMazeReady(_mazeGrid, _mazeWidth, _mazeDepth, _distanceFromExit, _cellWidth, _cellDepth);
//         }

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
//         {
//             for (int z = 0; z < _mazeDepth; z++)
//             {
//                 _distanceFromExit[x, z] = -1;
//             }
//         }

//         Queue<Vector2Int> queue = new Queue<Vector2Int>();
//         queue.Enqueue(_exitCoord);
//         _distanceFromExit[_exitCoord.x, _exitCoord.y] = 0;

//         Vector2Int[] directions = {
//             Vector2Int.right,
//             Vector2Int.left,
//             new Vector2Int(0, 1),
//             new Vector2Int(0, -1)
//         };

//         while (queue.Count > 0)
//         {
//             Vector2Int current = queue.Dequeue();
//             int nextDist = _distanceFromExit[current.x, current.y] + 1;

//             foreach (var dir in directions)
//             {
//                 Vector2Int neighbor = current + dir;

//                 if (neighbor.x < 0 || neighbor.x >= _mazeWidth || neighbor.y < 0 || neighbor.y >= _mazeDepth)
//                     continue;

//                 if (_distanceFromExit[neighbor.x, neighbor.y] != -1)
//                     continue;

//                 _distanceFromExit[neighbor.x, neighbor.y] = nextDist;
//                 queue.Enqueue(neighbor);
//             }
//         }
//     }

//     private IEnumerator GenerateMaze(MazeCell previousCell, MazeCell currentCell)
//     {
//         currentCell.Visit();

//         if (previousCell != null)
//         {
//             ClearWalls(previousCell, currentCell);
//         }

//         yield return new WaitForSeconds(0.01f);

//         MazeCell nextCell;
//         do
//         {
//             nextCell = GetNextUnvisitedCell(currentCell);
//             if (nextCell != null)
//             {
//                 yield return GenerateMaze(currentCell, nextCell);
//             }
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
//         if (previousCell == null || currentCell == null) return;

//         int deltaX = currentCell.GridX - previousCell.GridX;
//         int deltaZ = currentCell.GridZ - previousCell.GridZ;

//         if (Mathf.Abs(deltaX) + Mathf.Abs(deltaZ) != 1) return;

//         if (deltaX == 1)  { previousCell.ClearRightWall(); currentCell.ClearLeftWall();  return; }
//         if (deltaX == -1) { previousCell.ClearLeftWall();  currentCell.ClearRightWall(); return; }
//         if (deltaZ == 1)  { previousCell.ClearFrontWall(); currentCell.ClearBackWall();  return; }
//         if (deltaZ == -1) { previousCell.ClearBackWall();  currentCell.ClearFrontWall(); return; }
//     }
// }

// VERSION WITH CELL SCALING (FOR REFERENCE ONLY)

// using UnityEngine;
// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;

// public class MazeGenerator : MonoBehaviour
// {
//     // --- Cell prefabs ---
//     // Assign one or more cell prefabs. If more than one, they are chosen randomly.
//     // All prefabs must have identical outer dimensions for walls to align correctly.
//     [SerializeField] private MazeCell[] _mazeCellPrefabs;

//     [SerializeField] private int _mazeWidth;
//     [SerializeField] private int _mazeDepth;
//     [SerializeField] private GameObject _playerPrefab;
//     [SerializeField] private IntersectionDetector _intersectionDetector;

//     [Range(0f, 1f)]
//     [SerializeField] private float _wrongPathBias = 0.7f;

//     // Set these to match your cell asset's actual world-space dimensions.
//     // cellWidth  = size along the X axis (left/right)
//     // cellDepth  = size along the Z axis (forward/back)
//     [SerializeField] private float _cellWidth  = 2f;
//     [SerializeField] private float _cellDepth  = 2f;

//     // Exposed for PenaltyMazeManager snapping
//     public float CellWidth => _cellWidth;
//     public float CellDepth => _cellDepth;

//     // Keep a single CellSize property for any code that still needs one value.
//     // Uses the average — only meaningful when width == depth.
//     public float CellSize => (_cellWidth + _cellDepth) * 0.5f;

//     private GameObject _playerInstance;
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
//         _mazeGrid  = new MazeCell[_mazeWidth, _mazeDepth];

//         for (int x = 0; x < _mazeWidth; x++)
//         {
//             for (int z = 0; z < _mazeDepth; z++)
//             {
//                 // Space each cell by its real-world width/depth — no forced scaling
//                 Vector3 pos = origin + new Vector3(x * _cellWidth, 0, z * _cellDepth);

//                 // Pick a random prefab from the array
//                 MazeCell prefab = _mazeCellPrefabs[Random.Range(0, _mazeCellPrefabs.Length)];

//                 _mazeGrid[x, z] = Instantiate(prefab, pos, Quaternion.identity);
//                 _mazeGrid[x, z].SetGridPosition(x, z);
//             }
//         }

//         yield return GenerateMaze(null, _mazeGrid[0, 0]);

//         ComputeDistancesFromExit();

//         _mazeGrid[_exitCoord.x, _exitCoord.y].ClearFrontWall();

//         if (_intersectionDetector != null)
//             _intersectionDetector.OnMazeReady(
//                 _mazeGrid, _mazeWidth, _mazeDepth,
//                 _distanceFromExit, _cellWidth, _cellDepth);

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

//         while (queue.Count > 0)
//         {
//             Vector2Int current   = queue.Dequeue();
//             int         currentDist = _distanceFromExit[current.x, current.y];
//             MazeCell    currentCell = _mazeGrid[current.x, current.y];

//             if (current.x + 1 < _mazeWidth  && !currentCell.HasRightWall() && _distanceFromExit[current.x + 1, current.y] == -1)
//             { _distanceFromExit[current.x + 1, current.y] = currentDist + 1; queue.Enqueue(new Vector2Int(current.x + 1, current.y)); }

//             if (current.x - 1 >= 0          && !currentCell.HasLeftWall()  && _distanceFromExit[current.x - 1, current.y] == -1)
//             { _distanceFromExit[current.x - 1, current.y] = currentDist + 1; queue.Enqueue(new Vector2Int(current.x - 1, current.y)); }

//             if (current.y + 1 < _mazeDepth  && !currentCell.HasFrontWall() && _distanceFromExit[current.x, current.y + 1] == -1)
//             { _distanceFromExit[current.x, current.y + 1] = currentDist + 1; queue.Enqueue(new Vector2Int(current.x, current.y + 1)); }

//             if (current.y - 1 >= 0          && !currentCell.HasBackWall()  && _distanceFromExit[current.x, current.y - 1] == -1)
//             { _distanceFromExit[current.x, current.y - 1] = currentDist + 1; queue.Enqueue(new Vector2Int(current.x, current.y - 1)); }
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

//     void Update() { }
// }

// VERSION WITHOUT CELL SCALING (FOR REFERENCE ONLY)

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

//     // Exposed so PenaltyMazeManager can use it for snapping
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

//     // Destroys all current maze cells
//     public void DestroyMaze()
//     {
//         if (_intersectionDetector != null)
//             _intersectionDetector.ClearIndicators();

//         if (_mazeGrid == null) return;
//         foreach (MazeCell cell in _mazeGrid)
//             if (cell != null) Destroy(cell.gameObject);

//         _mazeGrid = null;
//     }

//     // Rebuilds a full-size maze with its origin at the player's current position
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
//                 _mazeGrid[x, z].transform.localScale = Vector3.one * _cellSize;
//                 _mazeGrid[x, z].SetGridPosition(x, z);
//             }
//         }

        
//         yield return GenerateMaze(null, _mazeGrid[0, 0]);
//         ComputeDistancesFromExit();
//         VisualizeCorrectPath();

//         _mazeGrid[_exitCoord.x, _exitCoord.y].ClearFrontWall();

//         if (_intersectionDetector != null)
//             _intersectionDetector.OnMazeReady(_mazeGrid, _mazeWidth, _mazeDepth, _distanceFromExit, _cellSize);

//         if (_playerInstance == null)
//         {
//             Vector3 startPosition = _mazeGrid[0, 0].transform.position + Vector3.up * 0.5f;
//             _playerInstance = Instantiate(_playerPrefab, startPosition, Quaternion.identity);
//         }

//         _mazeGrid[0, 0].DebugWalls();
//         _mazeGrid[1, 0].DebugWalls();
//         _mazeGrid[0, 1].DebugWalls();
//     }

//     // --- All existing methods unchanged below ---

//     private void ComputeDistancesFromExit()
//     {
//         _distanceFromExit = new int[_mazeWidth, _mazeDepth];
//         for (int x = 0; x < _mazeWidth; x++)
//             for (int z = 0; z < _mazeDepth; z++)
//                 _distanceFromExit[x, z] = -1;

//         Queue<Vector2Int> queue = new Queue<Vector2Int>();
//         queue.Enqueue(_exitCoord);
//         _distanceFromExit[_exitCoord.x, _exitCoord.y] = 0;

//         while (queue.Count > 0)
//         {
//             Vector2Int current = queue.Dequeue();
//             int currentDist = _distanceFromExit[current.x, current.y];
//             MazeCell currentCell = _mazeGrid[current.x, current.y];

//             // Check Right (+X) — Only move if there is no right wall
//             if (current.x + 1 < _mazeWidth && !currentCell.HasRightWall() && _distanceFromExit[current.x + 1, current.y] == -1)
//             {
//                 _distanceFromExit[current.x + 1, current.y] = currentDist + 1;
//                 queue.Enqueue(new Vector2Int(current.x + 1, current.y));
//             }
//             // Check Left (-X) — Only move if there is no left wall
//             if (current.x - 1 >= 0 && !currentCell.HasLeftWall() && _distanceFromExit[current.x - 1, current.y] == -1)
//             {
//                 _distanceFromExit[current.x - 1, current.y] = currentDist + 1;
//                 queue.Enqueue(new Vector2Int(current.x - 1, current.y));
//             }
//             // Check Front (+Z) — Only move if there is no front wall
//             if (current.y + 1 < _mazeDepth && !currentCell.HasFrontWall() && _distanceFromExit[current.x, current.y + 1] == -1)
//             {
//                 _distanceFromExit[current.x, current.y + 1] = currentDist + 1;
//                 queue.Enqueue(new Vector2Int(current.x, current.y + 1));
//             }
//             // Check Back (-Z) — Only move if there is no back wall
//             if (current.y - 1 >= 0 && !currentCell.HasBackWall() && _distanceFromExit[current.x, current.y - 1] == -1)
//             {
//                 _distanceFromExit[current.x, current.y - 1] = currentDist + 1;
//                 queue.Enqueue(new Vector2Int(current.x, current.y - 1));
//             }
//         }

        
//     }

//     public void VisualizeCorrectPath()
//     {
//         // Start at the exit
//         int currX = _exitCoord.x;
//         int currZ = _exitCoord.y;

//         // Safety check if BFS hasn't run or failed
//         if (_distanceFromExit == null || _distanceFromExit[currX, currZ] == -1) return;

//         int maxIterations = _mazeWidth * _mazeDepth; 
//         int safetyCounter = 0;

//         // Follow the breadcrumbs all the way down to distance 0
//         while (_distanceFromExit[currX, currZ] > 0 && safetyCounter < maxIterations)
//         {
//             safetyCounter++;

//             // Color the current cell path floor (Yellow/Gold makes a clear path indicator)
//             _mazeGrid[currX, currZ].HighlightPath(Color.yellow);

//             int currentDist = _distanceFromExit[currX, currZ];
//             MazeCell currentCell = _mazeGrid[currX, currZ];

//             // 1. Check if the path came from the Left (-X neighbor means current cell has no Left wall)
//             if (currX - 1 >= 0 && !currentCell.HasLeftWall() && _distanceFromExit[currX - 1, currZ] == currentDist - 1)
//             {
//                 currX--;
//                 continue;
//             }
//             // 2. Check if the path came from the Right (+X neighbor means current cell has no Right wall)
//             if (currX + 1 < _mazeWidth && !currentCell.HasRightWall() && _distanceFromExit[currX + 1, currZ] == currentDist - 1)
//             {
//                 currX++;
//                 continue;
//             }
//             // 3. Check if the path came from the Back (-Z neighbor means current cell has no Back wall)
//             if (currZ - 1 >= 0 && !currentCell.HasBackWall() && _distanceFromExit[currX, currZ - 1] == currentDist - 1)
//             {
//                 currZ--;
//                 continue;
//             }
//             // 4. Check if the path came from the Front (+Z neighbor means current cell has no Front wall)
//             if (currZ + 1 < _mazeDepth && !currentCell.HasFrontWall() && _distanceFromExit[currX, currZ + 1] == currentDist - 1)
//             {
//                 currZ++;
//                 continue;
//             }

//             // If it gets stuck without finding a descending neighbor, break out
//             break;
//         }

//         // Highlight the starting origin point cell too
//         _mazeGrid[currX, currZ].HighlightPath(Color.yellow);
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

//     void Update() { }
// }

