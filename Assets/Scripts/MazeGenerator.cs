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
    [SerializeField] private float _cellHeight = 3f;

    [Header("Exit Configuration")]
    [SerializeField] private GameObject _exitPortalPrefab; // Drag your Portal/Door prefab here in Inspector

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
        // _exitCoord = new Vector2Int(_mazeWidth - 1, _mazeDepth - 1);
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

        // ComputeDistancesFromExit();
        yield return GenerateMaze(null, _mazeGrid[0, 0]);

        _exitCoord = FindFarthestIntersectionExit();

        _mazeGrid[_exitCoord.x, _exitCoord.y].ClearFrontWall();

        ComputeDistancesFromExit();

        SpawnExitPortal(_exitCoord);

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

    private void SpawnExitPortal(Vector2Int exitCoord)
    {
        if (_exitPortalPrefab == null) return;

        MazeCell exitCell = _mazeGrid[exitCoord.x, exitCoord.y];
        Vector3 portalPos = exitCell.transform.position + Vector3.up * 0.5f;

        GameObject portalObj = Instantiate(_exitPortalPrefab, portalPos, Quaternion.identity);
        portalObj.transform.SetParent(exitCell.transform);
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

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int nextDist = _distanceFromExit[current.x, current.y] + 1;
            MazeCell currentCell = _mazeGrid[current.x, current.y];

            // 1. Check RIGHT Neighbor (Only if there's no right wall blocking it)
            if (current.x + 1 < _mazeWidth && !currentCell.HasRightWall())
            {
                if (_distanceFromExit[current.x + 1, current.y] == -1)
                {
                    _distanceFromExit[current.x + 1, current.y] = nextDist;
                    queue.Enqueue(new Vector2Int(current.x + 1, current.y));
                }
            }

            // 2. Check LEFT Neighbor (Only if there's no left wall blocking it)
            if (current.x - 1 >= 0 && !currentCell.HasLeftWall())
            {
                if (_distanceFromExit[current.x - 1, current.y] == -1)
                {
                    _distanceFromExit[current.x - 1, current.y] = nextDist;
                    queue.Enqueue(new Vector2Int(current.x - 1, current.y));
                }
            }

            // 3. Check FRONT (Up) Neighbor (Only if there's no front wall blocking it)
            if (current.y + 1 < _mazeDepth && !currentCell.HasFrontWall())
            {
                if (_distanceFromExit[current.x, current.y + 1] == -1)
                {
                    _distanceFromExit[current.x, current.y + 1] = nextDist;
                    queue.Enqueue(new Vector2Int(current.x, current.y + 1));
                }
            }

            // 4. Check BACK (Down) Neighbor (Only if there's no back wall blocking it)
            if (current.y - 1 >= 0 && !currentCell.HasBackWall())
            {
                if (_distanceFromExit[current.x, current.y - 1] == -1)
                {
                    _distanceFromExit[current.x, current.y - 1] = nextDist;
                    queue.Enqueue(new Vector2Int(current.x, current.y - 1));
                }
            }
        }
    }

    private Vector2Int FindFarthestIntersectionExit()
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> intersectionCounts = new Dictionary<Vector2Int, int>();
        Dictionary<Vector2Int, Vector2Int> parentMap = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        Vector2Int start = new Vector2Int(0, 0);
        queue.Enqueue(start);
        visited.Add(start);
        intersectionCounts[start] = 0;

        Vector2Int bestIntersection = start;
        int maxIntersections = -1;

        // Phase 1: Traverse the maze to locate the junction with the highest intersection depth
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentCount = intersectionCounts[current];

            MazeCell cell = _mazeGrid[current.x, current.y];
            List<Vector2Int> openExits = GetOpenExits(cell);

            if (openExits.Count >= 3)
            {
                currentCount++;
            }

            if (currentCount > maxIntersections)
            {
                maxIntersections = currentCount;
                bestIntersection = current;
            }

            foreach (var dir in openExits)
            {
                Vector2Int neighbor = current + dir;
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    parentMap[neighbor] = current;
                    intersectionCounts[neighbor] = currentCount;
                    queue.Enqueue(neighbor);
                }
            }
        }

        // Phase 2: Identify the path coming into the final intersection from the start
        Vector2Int incomingDirFromStart = Vector2Int.zero;
        if (parentMap.ContainsKey(bestIntersection))
        {
            incomingDirFromStart = bestIntersection - parentMap[bestIntersection];
        }

        // 3. Find all outgoing exits that are NOT the entrance path
        MazeCell finalJunctionCell = _mazeGrid[bestIntersection.x, bestIntersection.y];
        List<Vector2Int> junctionExits = GetOpenExits(finalJunctionCell);

        Vector2Int bestTerminalCell = bestIntersection;
        int longestBranchLength = -1;

        foreach (var dir in junctionExits)
        {
            // Skip the path coming from the start!
            if (dir == -incomingDirFromStart) continue;

            // Walk this corridor to measure its length to the end
            Vector2Int currentStep = bestIntersection + dir;
            Vector2Int prevStep = bestIntersection;
            int branchLength = 1;

            while (true)
            {
                MazeCell stepCell = _mazeGrid[currentStep.x, currentStep.y];
                List<Vector2Int> stepExits = GetOpenExits(stepCell);

                Vector2Int nextStep = currentStep;
                bool foundForward = false;

                foreach (var stepDir in stepExits)
                {
                    Vector2Int neighbor = currentStep + stepDir;
                    if (neighbor != prevStep)
                    {
                        nextStep = neighbor;
                        foundForward = true;
                        break;
                    }
                }

                if (!foundForward) break;

                prevStep = currentStep;
                currentStep = nextStep;
                branchLength++;
            }

            // Pick the longest available corridor extending past the intersection
            if (branchLength > longestBranchLength)
            {
                longestBranchLength = branchLength;
                bestTerminalCell = currentStep;
            }
        }

        return bestTerminalCell;

        // Phase 3: Pick an outgoing branch from the intersection that is NOT the incoming start path
        // MazeCell finalJunctionCell = _mazeGrid[bestIntersection.x, bestIntersection.y];
        // List<Vector2Int> junctionExits = GetOpenExits(finalJunctionCell);

        // Vector2Int selectedExitDir = junctionExits[0];
        // foreach (var dir in junctionExits)
        // {
        //     // Avoid going backward toward the entrance path
        //     if (dir != -incomingDirFromStart)
        //     {
        //         selectedExitDir = dir;
        //         break;
        //     }
        // }

        // // Phase 4: Walk down the chosen branch until reaching the terminal end of that corridor
        // Vector2Int currentTerminalCell = bestIntersection + selectedExitDir;
        // Vector2Int previousCell = bestIntersection;

        // while (true)
        // {
        //     MazeCell termCell = _mazeGrid[currentTerminalCell.x, currentTerminalCell.y];
        //     List<Vector2Int> termExits = GetOpenExits(termCell);

        //     // Find next open path step that doesn't loop back to previousCell
        //     Vector2Int nextStep = currentTerminalCell;
        //     bool foundForwardStep = false;

        //     foreach (var dir in termExits)
        //     {
        //         Vector2Int neighbor = currentTerminalCell + dir;
        //         if (neighbor != previousCell)
        //         {
        //             nextStep = neighbor;
        //             foundForwardStep = true;
        //             break;
        //         }
        //     }

        //     // Reached dead end or corridor exit
        //     if (!foundForwardStep) break;

        //     previousCell = currentTerminalCell;
        //     currentTerminalCell = nextStep;
        // }

        // return currentTerminalCell;
    }

    private Vector2Int old_FindFarthestIntersectionExit()
    {
        // Breadth-First Search (BFS) tracking path history and intersection counts
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> intersectionCounts = new Dictionary<Vector2Int, int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        Vector2Int start = new Vector2Int(0, 0);
        queue.Enqueue(start);
        visited.Add(start);
        intersectionCounts[start] = 0;

        Vector2Int bestExit = start;
        int maxIntersections = -1;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentCount = intersectionCounts[current];

            // Count open exits for current cell
            MazeCell cell = _mazeGrid[current.x, current.y];
            List<Vector2Int> openExits = GetOpenExits(cell);

            // If this is an intersection (3+ paths), increment the counter
            if (openExits.Count >= 3)
            {
                currentCount++;
            }

            // Track max intersections found so far
            if (currentCount > maxIntersections)
            {
                maxIntersections = currentCount;
                bestExit = current;
            }

            foreach (var dir in openExits)
            {
                Vector2Int neighbor = current + dir;
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    intersectionCounts[neighbor] = currentCount;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return bestExit;
    }

    private List<Vector2Int> GetOpenExits(MazeCell cell)
    {
        List<Vector2Int> exits = new List<Vector2Int>();
        int x = cell.GridX;
        int z = cell.GridZ;

        if (x + 1 < _mazeWidth  && !cell.HasRightWall()) exits.Add(Vector2Int.right);
        if (x - 1 >= 0         && !cell.HasLeftWall())  exits.Add(Vector2Int.left);
        if (z + 1 < _mazeDepth  && !cell.HasFrontWall()) exits.Add(new Vector2Int(0, 1));
        if (z - 1 >= 0         && !cell.HasBackWall())  exits.Add(new Vector2Int(0, -1));

        return exits;
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
