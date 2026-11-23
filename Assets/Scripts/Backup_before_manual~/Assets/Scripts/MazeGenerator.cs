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

    [Header("GDD Binder")]
    [Tooltip("Prefab with a GDDBinder component. Spawned once at cell (0,0), not re-spawned on maze rebuild.")]
    [SerializeField] private GameObject _gddBinderPrefab;

    [Range(0f, 1f)]
    [SerializeField] private float _wrongPathBias = 0.7f;

    // Explicit dimensions you set in the Unity Inspector to enforce layout size
    [SerializeField] private float _cellWidth = 2f;
    [SerializeField] private float _cellDepth = 2f;
    [SerializeField] private float _cellHeight = 3f;

    [Header("Exit Configuration")]
    [SerializeField] private GameObject _exitPortalPrefab; // Drag your Portal/Door prefab here in Inspector

    [Header("Validation")]
    [SerializeField] private int _maxRegenerationAttempts = 5;

    [Header("Extra Connections (loops)")]
    [Tooltip("How many extra openings to knock through after the exit has been chosen. Each one joins two cells that " +
             "weren't connected, creating a loop and usually a new junction. 0 = perfect maze (old behaviour).")]
    [SerializeField] private int _extraConnections = 4;

    [Range(0f, 1f)]
    [Tooltip("An opening is only kept if the shortest start-to-exit route still passes at least this fraction of the " +
             "junctions it had before any opening was added. Stops an opening from becoming a shortcut that skips " +
             "the junction-heavy route the exit was chosen for.")]
    [SerializeField] private float _minRouteJunctionRatio = 0.8f;

    [Range(0f, 1f)]
    [Tooltip("An opening is only kept if the shortest start-to-exit route is still at least this fraction of its " +
             "original length (in cells). 0 = no limit; loops then shorten the run a lot (about a third with 4 openings on 8x8).")]
    [SerializeField] private float _minRouteLengthRatio = 0.75f;

    [Tooltip("Try openings from dead ends first (classic 'braiding'). Off = any wall in the maze is a candidate.")]
    [SerializeField] private bool _preferDeadEnds = true;

    // Public getters to allow PenaltyMazeManager to read your custom sizes
    public float CellWidth => _cellWidth;
    public float CellDepth => _cellDepth;
    public float CellHeight => _cellHeight; // <-- NEW
    public float CellSize => (_cellWidth + _cellDepth) * 0.5f;

    private GameObject _playerInstance;
    private GameObject _binderInstance;
    private MazeCell[,] _mazeGrid;
    private int[,] _distanceFromExit;
    private Vector2Int _exitCoord;
    private Vector3 _mazeOrigin = Vector3.zero;

    IEnumerator Start()
    {
        yield return BuildMaze(_mazeOrigin);
        TimerManager.Instance.StartTimer();
        // if (IntroSlideshowManager.Instance != null)
        //     IntroSlideshowManager.Instance.OnIntroFinished += () => TimerManager.Instance.StartTimer();
        // else
        //     TimerManager.Instance.StartTimer();
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

    /// <summary>BFS steps to the exit for every cell (0 at the exit). Used by the ambient hallucination spawner.</summary>
    public int[,] GetDistanceFromExit() => _distanceFromExit;

    public MazeCell[,] GetMazeGrid()
    {
        return _mazeGrid;
    }

    private IEnumerator BuildMaze(Vector3 origin)
{
    bool valid = false;
    int attempt = 0;

    while (!valid)
    {
        attempt++;
        yield return CarveMaze(origin);

        valid = _intersectionDetector == null
            || _intersectionDetector.ValidateBuild(_mazeGrid, _mazeWidth, _mazeDepth, _distanceFromExit);

        if (!valid)
        {
            if (attempt >= _maxRegenerationAttempts)
            {
                Debug.LogError($"MazeGenerator: failed validation {attempt} times in a row; starting anyway.");
                valid = true; // stop retrying, keep this build
            }
            else
            {
                Debug.LogWarning($"MazeGenerator: validation failed (attempt {attempt}), regenerating.");
                DestroyMaze(); // tears down cells + calls ClearIndicators()
            }
        }
    }

    AmbientHallucinationSpawner.OnMazeBuilt(); // build validated and kept: place the "obviously wrong" corridor items
    ZombieManager.OnMazeBuilt(); // zombies: clear the old maze's zombies, place the Zombie Behaviour Spec page

    if (_playerInstance == null)
    {
        Vector3 startPosition = _mazeGrid[0, 0].transform.position + Vector3.up * 0.5f;
        _playerInstance = Instantiate(_playerPrefab, startPosition, Quaternion.identity);
    }

    if (_gddBinderPrefab != null && _binderInstance == null)
    {
        Vector3 binderPosition = _mazeGrid[0, 0].transform.position + Vector3.up * 0.5f + Vector3.forward * 2f + Vector3.right * 1f;
        _binderInstance = Instantiate(_gddBinderPrefab, binderPosition, Quaternion.identity);
    }
}

    private IEnumerator CarveMaze(Vector3 origin)
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
                    // Physics.SyncTransforms(); // Ensure bounds update with new scale factors
                    // Vector3 localCenterOffset = targetRenderer.bounds.center - instance.transform.position;
                    
                    // // Keep the Y position completely intact so floor levels remain matched
                    // localCenterOffset.y = 0f;

                    // // Shift the position backward by its center discrepancy so it fits perfectly inside the slot
                    // instance.transform.position = targetCellCenter - localCenterOffset;
                    // 2. COMPUTE PIVOT OFFSET CORRECTION
                    Physics.SyncTransforms();
                    Vector3 localCenterOffset = targetRenderer.bounds.center - instance.transform.position;
                    localCenterOffset.y = 0f;

                    // Keep the cell ROOT exactly on its grid slot and move the children instead,
                    // so cell.transform.position is the real centre of the cell for every script.
                    instance.transform.position = targetCellCenter;
                    foreach (Transform child in instance.transform)
                        child.position -= localCenterOffset;
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

        // Used to be an unconditional ClearFrontWall(). When the exit cell is NOT on the top row, that removed
        // only the exit cell's half of the wall: the neighbour's back wall stayed up, so the wall looked closed
        // but ComputeDistancesFromExit() treated it as open. The BFS then "reached" the neighbour through a wall,
        // and every junction whose correct exit pointed that way led the player to a dead end next to the portal
        // (and SpawnGDDPages stopped there, so later junctions got no page). Only open it when it's the outer edge.
        if (_exitCoord.y == _mazeDepth - 1)
            _mazeGrid[_exitCoord.x, _exitCoord.y].ClearFrontWall();

        // Knock through a few extra walls to create loops / new junctions. Must run AFTER the exit is chosen
        // (so the exit still sits at the end of the junction-heavy route) and BEFORE the distance field is
        // computed (so every junction's "correct" exit is the shortest way out of the new, looped maze).
        AddExtraConnections();

        ComputeDistancesFromExit();

        SpawnExitPortal(_exitCoord);

        if (_intersectionDetector != null)
        {
            _intersectionDetector.OnMazeReady(_mazeGrid, _mazeWidth, _mazeDepth, _distanceFromExit, _cellWidth, _cellDepth);
        }

        // if (_playerInstance == null)
        // {
        //     Vector3 startPosition = _mazeGrid[0, 0].transform.position + Vector3.up * 0.5f;
        //     _playerInstance = Instantiate(_playerPrefab, startPosition, Quaternion.identity);
        // }

        // if (_gddBinderPrefab != null && _binderInstance == null)
        // {
        //     Vector3 binderPosition = _mazeGrid[0, 0].transform.position + Vector3.up * 0.05f + Vector3.forward * 2f 
        //     + Vector3.right * 1f; // + Vector3.up * 0.5f 
        //     _binderInstance = Instantiate(_gddBinderPrefab, binderPosition, Quaternion.identity*Quaternion.Euler(0, 0, 90f));
        // }
    }

    // ---------- Extra connections (loops) ----------

    // Openings are planned on a logical copy of the passages, so a candidate can be tried and undone without
    // touching any GameObject (MazeCell can clear a wall but not put one back). Only accepted openings are
    // applied to the real cells, with ClearWalls() so BOTH halves of the wall come down.
    //   eastOpen[x,z]  : passage (x,z) <-> (x+1,z)
    //   northOpen[x,z] : passage (x,z) <-> (x,z+1)
    private struct WallCandidate
    {
        public Vector2Int a, b;
        public bool east; // true: a-b is an east/west wall, false: north/south
    }

    private void AddExtraConnections()
    {
        if (_extraConnections <= 0) return;

        int w = _mazeWidth, d = _mazeDepth;
        bool[,] eastOpen = new bool[w, d];
        bool[,] northOpen = new bool[w, d];

        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                if (x + 1 < w) eastOpen[x, z]  = !_mazeGrid[x, z].HasRightWall() && !_mazeGrid[x + 1, z].HasLeftWall();
                if (z + 1 < d) northOpen[x, z] = !_mazeGrid[x, z].HasFrontWall() && !_mazeGrid[x, z + 1].HasBackWall();
            }

        Vector2Int start = Vector2Int.zero;
        int baseline = CountRouteJunctions(eastOpen, northOpen, start, _exitCoord, out int baselineLength);
        int minJunctions = Mathf.CeilToInt(baseline * _minRouteJunctionRatio);
        int minLength = Mathf.CeilToInt(baselineLength * _minRouteLengthRatio);

        // Every closed interior wall that doesn't touch the start cell or the exit cell
        // (the exit must stay a dead end at the end of its corridor).
        var candidates = new List<WallCandidate>();
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                Vector2Int a = new Vector2Int(x, z);
                if (x + 1 < w && !eastOpen[x, z])  candidates.Add(new WallCandidate { a = a, b = new Vector2Int(x + 1, z), east = true });
                if (z + 1 < d && !northOpen[x, z]) candidates.Add(new WallCandidate { a = a, b = new Vector2Int(x, z + 1), east = false });
            }
        candidates.RemoveAll(c => c.a == start || c.b == start || c.a == _exitCoord || c.b == _exitCoord);

        // Shuffle, then (optionally) move walls next to a dead end to the front. OrderBy is a stable sort,
        // so the random order is kept within each group.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        if (_preferDeadEnds)
        {
            candidates = candidates
                .OrderBy(c => (Degree(eastOpen, northOpen, c.a) == 1 || Degree(eastOpen, northOpen, c.b) == 1) ? 0 : 1)
                .ToList();
        }

        int added = 0;
        foreach (WallCandidate c in candidates)
        {
            if (added >= _extraConnections) break;

            SetPassage(eastOpen, northOpen, c, true);

            bool keep = false;
            if (!CreatesOpenSquare(eastOpen, northOpen, c)) // no 2x2 open "rooms"
            {
                int junctions = CountRouteJunctions(eastOpen, northOpen, start, _exitCoord, out int length);
                keep = junctions >= minJunctions && length >= minLength;
            }

            if (keep)
            {
                ClearWalls(_mazeGrid[c.a.x, c.a.y], _mazeGrid[c.b.x, c.b.y]);
                added++;
            }
            else
            {
                SetPassage(eastOpen, northOpen, c, false);
            }
        }

        int finalJunctions = CountRouteJunctions(eastOpen, northOpen, start, _exitCoord, out int finalLength);
        Debug.Log($"MazeGenerator: added {added}/{_extraConnections} extra connections. " +
                  $"Route junctions {baseline} -> {finalJunctions} (min {minJunctions}), route length {baselineLength} -> {finalLength} (min {minLength}).");
    }

    private static void SetPassage(bool[,] eastOpen, bool[,] northOpen, WallCandidate c, bool open)
    {
        if (c.east) eastOpen[c.a.x, c.a.y] = open;
        else        northOpen[c.a.x, c.a.y] = open;
    }

    // True if the passage just opened completes a 2x2 block with all four inner passages open.
    private bool CreatesOpenSquare(bool[,] eastOpen, bool[,] northOpen, WallCandidate c)
    {
        int x = c.a.x, z = c.a.y;
        if (c.east)
            return IsSquareOpen(eastOpen, northOpen, x, z) || IsSquareOpen(eastOpen, northOpen, x, z - 1);
        else
            return IsSquareOpen(eastOpen, northOpen, x, z) || IsSquareOpen(eastOpen, northOpen, x - 1, z);
    }

    // Square whose bottom-left cell is (bx,bz).
    private bool IsSquareOpen(bool[,] eastOpen, bool[,] northOpen, int bx, int bz)
    {
        if (bx < 0 || bz < 0 || bx + 1 >= _mazeWidth || bz + 1 >= _mazeDepth) return false;
        return eastOpen[bx, bz] && eastOpen[bx, bz + 1] && northOpen[bx, bz] && northOpen[bx + 1, bz];
    }

    // Same neighbour order as GetOpenExits (right, left, front, back) so tie-breaking matches the rest of the game.
    private List<Vector2Int> LogicalNeighbours(bool[,] eastOpen, bool[,] northOpen, Vector2Int c)
    {
        var result = new List<Vector2Int>(4);
        if (c.x + 1 < _mazeWidth && eastOpen[c.x, c.y])      result.Add(new Vector2Int(c.x + 1, c.y));
        if (c.x - 1 >= 0         && eastOpen[c.x - 1, c.y])  result.Add(new Vector2Int(c.x - 1, c.y));
        if (c.y + 1 < _mazeDepth && northOpen[c.x, c.y])     result.Add(new Vector2Int(c.x, c.y + 1));
        if (c.y - 1 >= 0         && northOpen[c.x, c.y - 1]) result.Add(new Vector2Int(c.x, c.y - 1));
        return result;
    }

    private int Degree(bool[,] eastOpen, bool[,] northOpen, Vector2Int c) => LogicalNeighbours(eastOpen, northOpen, c).Count;

    // Number of junctions (3+ open sides) the shortest start-to-exit route passes through,
    // counted the same way SpawnGDDPages hands out pages (every junction stepped onto after the start).
    private int CountRouteJunctions(bool[,] eastOpen, bool[,] northOpen, Vector2Int start, Vector2Int exit, out int routeLength)
    {
        routeLength = 0;
        int w = _mazeWidth, d = _mazeDepth;
        int[,] dist = new int[w, d];
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
                dist[x, z] = -1;

        var queue = new Queue<Vector2Int>();
        queue.Enqueue(exit);
        dist[exit.x, exit.y] = 0;
        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            foreach (Vector2Int n in LogicalNeighbours(eastOpen, northOpen, cur))
            {
                if (dist[n.x, n.y] != -1) continue;
                dist[n.x, n.y] = dist[cur.x, cur.y] + 1;
                queue.Enqueue(n);
            }
        }

        if (dist[start.x, start.y] < 0) return 0;
        routeLength = dist[start.x, start.y];

        int junctions = 0;
        Vector2Int c = start;
        while (dist[c.x, c.y] > 0)
        {
            Vector2Int next = c;
            foreach (Vector2Int n in LogicalNeighbours(eastOpen, northOpen, c))
                if (dist[n.x, n.y] == dist[c.x, c.y] - 1) { next = n; break; }
            if (next == c) break;
            c = next;
            if (Degree(eastOpen, northOpen, c) >= 3) junctions++;
        }
        return junctions;
    }

    private void SpawnExitPortal(Vector2Int exitCoord)
    {
        if (_exitPortalPrefab == null) return;

        MazeCell exitCell = _mazeGrid[exitCoord.x, exitCoord.y];
        Vector3 portalPos = exitCell.transform.position + Vector3.up * 0.1f;

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

        // _distanceFromExit is null on the very first build (the exit isn't chosen until generation ends),
        // and indexing it threw a NullReferenceException that stopped generation part-way. On rebuilds it
        // holds the PREVIOUS maze's distances, so this bias is approximate at best (known issue #3).
        if (_distanceFromExit != null && Random.value < _wrongPathBias)
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
