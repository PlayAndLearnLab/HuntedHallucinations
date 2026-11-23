using UnityEngine;
using System.Collections.Generic;

public class IntersectionDetector : MonoBehaviour
{
    [Header("Puzzle Spawner Prefabs")]
    [SerializeField] private GameObject _visualPuzzleSpawnerPrefab;
    [SerializeField] private GameObject _textPuzzleSpawnerPrefab;

    [Header("Trigger Prefabs")]
    [SerializeField] private GameObject _wrongPathTriggerPrefab;
    [SerializeField] private GameObject _intersectionZonePrefab;

    [Header("Layout")]
    [SerializeField] private float _labelHeight = 1.5f;

    [Header("GDD Page Mechanic")]
    [Tooltip("Prefab with a GDDPage component. Spawned on the floor exactly one step before each intersection on the true solution path.")]
    [SerializeField] private GameObject _gddPagePrefab;

    private List<GameObject> _spawnedObjects = new List<GameObject>();

    // Cell coordinate -> the IntersectionZone built there, so the solution-path
    // walk below can look up which PuzzleData belongs to an upcoming junction.
    private Dictionary<Vector2Int, IntersectionZone> _zoneByCell = new Dictionary<Vector2Int, IntersectionZone>();

    // PuzzleData -> every GDDPage instance spawned for it. A given PuzzleData
    // can end up on more than one instance (a puzzle got reused since there
    // were more intersections than puzzles, or — in the future — the same
    // intersection is reachable via more than one path). Collecting any one
    // instance despawns all of them, via the GDDManager.OnPageCollected subscription below.
    private Dictionary<PuzzleData, List<GameObject>> _pageInstancesByData = new Dictionary<PuzzleData, List<GameObject>>();

    private bool _subscribedToGDD = false;

    [Header("Loops")]
    [Tooltip("When two or more exits of a junction are equally short (possible once the maze has loops), " +
             "pick the 'correct' one at random. Off = always the first one (right, left, front, back).")]
    [SerializeField] private bool _randomTieBreak = true;

    /// <summary>Lets other systems (AmbientHallucinationSpawner) follow exactly the same route as the pages and puzzles.</summary>
    public static IntersectionDetector Instance { get; private set; }

    // The maze the zones/pages were last built for — used by NextStepTowardExit().
    private MazeCell[,] _grid;
    private int _width, _depth;
    private int[,] _distanceFromExit;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_subscribedToGDD && GDDManager.Instance != null)
            GDDManager.Instance.OnPageCollected -= HandlePageCollected;
    }

    private void EnsureGDDSubscription()
    {
        if (_subscribedToGDD || GDDManager.Instance == null) return;
        GDDManager.Instance.OnPageCollected += HandlePageCollected;
        _subscribedToGDD = true;
    }

    private void HandlePageCollected(PuzzleData data)
    {
        if (!_pageInstancesByData.TryGetValue(data, out List<GameObject> instances)) return;

        foreach (GameObject obj in instances)
        {
            if (obj == null) continue;
            _spawnedObjects.Remove(obj);
            Destroy(obj);
        }
        _pageInstancesByData.Remove(data);
    }

    public void ClearIndicators()
    {
        AmbientHallucinationSpawner.ClearAll(); // ambient corridor items belong to the maze being torn down
        foreach (GameObject obj in _spawnedObjects)
            if (obj != null) Destroy(obj);
        _spawnedObjects.Clear();
        _zoneByCell.Clear();
        _pageInstancesByData.Clear();
        _grid = null;              // cells are being destroyed; NextStepTowardExit() returns false until the next OnMazeReady
        _distanceFromExit = null;
    }

    public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellWidth, float cellDepth)
    {
        EnsureGDDSubscription();

        _grid = grid;
        _width = width;
        _depth = depth;
        _distanceFromExit = distanceFromExit;

        // Fresh puzzle pool for this build — PickPuzzle() won't repeat a
        // puzzle until every owned puzzle has been used at least once.
        if (PuzzleManager.Instance != null)
            PuzzleManager.Instance.ResetUsedPuzzles();

        for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
            {
                MazeCell cell = grid[x, z];
                List<Vector2Int> openExits = GetOpenExits(cell, width, depth);

                if (openExits.Count >= 3)
                    SpawnPuzzle(cell, openExits, distanceFromExit, cellWidth, cellDepth);
            }

        SpawnGDDPages(grid, width, depth, distanceFromExit, cellWidth, cellDepth);
    }

    /// <summary>
    /// The next cell on the route toward the exit from <paramref name="cell"/>, as a player who never takes a
    /// wrong turn would walk it. At a junction this is the exit its puzzle calls correct (which may be a random
    /// pick between equally short exits); elsewhere it's the first open neighbour one step closer to the exit.
    /// Pages, validation and ambient items all use this, so they always agree on which way is "right".
    /// Returns false at the exit, or if there is no valid step.
    /// </summary>
    public bool NextStepTowardExit(Vector2Int cell, out Vector2Int next)
    {
        next = cell;
        if (_grid == null || _distanceFromExit == null) return false;

        int dist = _distanceFromExit[cell.x, cell.y];
        if (dist <= 0) return false;

        if (_zoneByCell.TryGetValue(cell, out IntersectionZone zone) && zone != null && zone.HasCorrectExit)
        {
            next = cell + zone.CorrectExit;
            return true;
        }

        foreach (Vector2Int exit in GetOpenExits(_grid[cell.x, cell.y], _width, _depth))
        {
            Vector2Int candidate = cell + exit;
            if (_distanceFromExit[candidate.x, candidate.y] == dist - 1)
            {
                next = candidate;
                return true;
            }
        }
        return false;
    }

    // Walks the correct route from (0,0) to the exit (see NextStepTowardExit).
    // For every intersection it passes
    // through, drops one GDD page at a random cell somewhere in the corridor
    // segment since the previous intersection (or the start) — never on the
    // intersection cell itself. Skips spawning entirely if the player already
    // holds that page. With loops there can be several equally short routes;
    // following the junctions' recorded CorrectExit keeps pages on the route the puzzles call right.
    private void SpawnGDDPages(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellWidth, float cellDepth)
    {
        if (_gddPagePrefab == null) return;

        Vector2Int current = new Vector2Int(0, 0);
        int safety = width * depth;

        // Cells traversed since the last intersection (or the start),
        // excluding any intersection cell — the candidate pool for random placement.
        // The start cell is NOT a candidate: the player spawns there, so a page on it was picked up instantly,
        // before the Binder (the book then opened with a page already inside).
        List<MazeCell> segmentCells = new List<MazeCell>();

        while (distanceFromExit[current.x, current.y] != 0 && safety-- > 0)
        {
            MazeCell currentCell = grid[current.x, current.y];

            if (!NextStepTowardExit(current, out Vector2Int next)) break; // shouldn't happen against a valid distance field, but bail out safely

            if (_zoneByCell.TryGetValue(next, out IntersectionZone nextZone))
            {
                PuzzleData data = nextZone.GetAssignedPuzzleData();
                bool alreadyCollected = GDDManager.Instance != null && GDDManager.Instance.HasCollected(data);

                if (data != null && !alreadyCollected)
                {
                    // Random spot anywhere in this segment; fall back to the
                    // current cell if the segment is empty (e.g. two intersections
                    // sit right next to each other with no corridor between them).
                    MazeCell spawnCell = segmentCells.Count > 0
                        ? segmentCells[Random.Range(0, segmentCells.Count)]
                        : currentCell;

                    SpawnPageAt(spawnCell, cellWidth, cellDepth, data);
                }

                segmentCells.Clear(); // start a fresh segment past this intersection
            }

            current = next;

            // Don't offer an intersection cell itself as a future spawn point.
            if (!_zoneByCell.ContainsKey(current))
                segmentCells.Add(grid[current.x, current.y]);
        }
    }

    private void SpawnPageAt(MazeCell cell, float cellWidth, float cellDepth, PuzzleData data)
    {
        Vector3 pos = cell.transform.position + Vector3.up * 0.15f;
        GameObject pageObj = Instantiate(_gddPagePrefab, pos, Quaternion.identity);
        // pageObj.transform.localScale = new Vector3(cellWidth * 0.3f, 1f, cellDepth * 0.3f);
        pageObj.transform.localScale = Vector3.one;

        GDDPage page = pageObj.GetComponent<GDDPage>();
        if (page != null)
            page.Setup(data);
        else
            Debug.LogWarning("GDD page prefab has no GDDPage component.");

        _spawnedObjects.Add(pageObj);

        if (!_pageInstancesByData.TryGetValue(data, out List<GameObject> instances))
        {
            instances = new List<GameObject>();
            _pageInstancesByData[data] = instances;
        }
        instances.Add(pageObj);
    }

    private List<Vector2Int> GetOpenExits(MazeCell cell, int width, int depth)
    {
        List<Vector2Int> exits = new List<Vector2Int>();
        int x = cell.GridX;
        int z = cell.GridZ;

        if (x + 1 < width  && !cell.HasRightWall()) exits.Add(Vector2Int.right);
        if (x - 1 >= 0     && !cell.HasLeftWall())  exits.Add(Vector2Int.left);
        if (z + 1 < depth  && !cell.HasFrontWall()) exits.Add(new Vector2Int(0, 1));
        if (z - 1 >= 0     && !cell.HasBackWall())  exits.Add(new Vector2Int(0, -1));

        return exits;
    }

    private void SpawnPuzzle(MazeCell cell, List<Vector2Int> exits, int[,] distanceFromExit, float cellWidth, float cellDepth)
    {
        int x = cell.GridX;
        int z = cell.GridZ;

        // Find the correct exit: the shortest way to the portal. With loops, several exits can be equally
        // short — collect them all and pick one (at random if _randomTieBreak). Whatever is picked here is
        // recorded on the zone, and pages / validation / ambient items follow that record.
        int bestDist = int.MaxValue;
        List<Vector2Int> tied = new List<Vector2Int>();

        foreach (var exit in exits)
        {
            int targetX = x + exit.x;
            int targetZ = z + exit.y; // Vector2Int uses .y for the second parameter

            int dist = distanceFromExit[targetX, targetZ];

            // Ignore unreachable cells (-1) entirely
            if (dist == -1) continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                tied.Clear();
                tied.Add(exit);
            }
            else if (dist == bestDist)
            {
                tied.Add(exit);
            }
        }

        Vector2Int bestExit = tied.Count == 0 ? exits[0]
                            : _randomTieBreak ? tied[Random.Range(0, tied.Count)]
                            : tied[0];

        // Spawn intersection zone (arms wrong path triggers when player enters)
        IntersectionZone zone = null;
        if (_intersectionZonePrefab != null)
        {
            GameObject zoneObj = Instantiate(
                _intersectionZonePrefab,
                cell.transform.position + Vector3.up * 0.5f,
                Quaternion.identity);
            zoneObj.transform.localScale = new Vector3(cellWidth * 0.8f, 1.5f, cellDepth * 0.8f);
            zone = zoneObj.GetComponent<IntersectionZone>();
            _spawnedObjects.Add(zoneObj);
            _zoneByCell[new Vector2Int(x, z)] = zone;
            if (zone != null) zone.SetCorrectExit(bestExit); 
        }

        // Pick a puzzle from PuzzleManager
        PuzzleData puzzle = PuzzleManager.Instance.PickPuzzle();

        if (puzzle is VisualPuzzleData visualData && _visualPuzzleSpawnerPrefab != null)
        {
            GameObject spawnerObj = Instantiate(
                _visualPuzzleSpawnerPrefab,
                cell.transform.position,
                Quaternion.identity);

            VisualPuzzleSpawner spawner = spawnerObj.GetComponent<VisualPuzzleSpawner>();
            spawner.Setup(visualData, exits, bestExit, cell.transform.position, cellWidth, cellDepth);
            _spawnedObjects.Add(spawnerObj);
            // zone.RegisterPuzzleSpawner(spawnerObj);
            if (zone != null) zone.RegisterPuzzleSpawner(spawnerObj);
        }
        else if (puzzle is TextPuzzleData textData && _textPuzzleSpawnerPrefab != null)
        {
            GameObject spawnerObj = Instantiate(
                _textPuzzleSpawnerPrefab,
                cell.transform.position,
                Quaternion.identity);

            TextPuzzleSpawner spawner = spawnerObj.GetComponent<TextPuzzleSpawner>();
            spawner.Setup(textData, exits, bestExit, cell.transform.position, cellWidth, cellDepth, _labelHeight);
            _spawnedObjects.Add(spawnerObj);
            if (zone != null) zone.RegisterPuzzleSpawner(spawnerObj);
        }

        // Wrong path triggers — unchanged from before
        foreach (var exit in exits)
        {
            if (exit == bestExit) continue;
            if (_wrongPathTriggerPrefab == null || zone == null) continue;

            Vector3 triggerPos = cell.transform.position
                + new Vector3(exit.x * cellWidth, 0, exit.y * cellDepth)
                + Vector3.up * 0.5f;

            Quaternion triggerRot = Quaternion.LookRotation(new Vector3(exit.x, 0, exit.y));
            GameObject triggerObj = Instantiate(_wrongPathTriggerPrefab, triggerPos, triggerRot);
            triggerObj.transform.localScale = new Vector3(cellWidth * 0.8f, 1.5f, 0.3f);
            _spawnedObjects.Add(triggerObj);

            WrongPathTrigger wrongTrigger = triggerObj.GetComponent<WrongPathTrigger>();
            if (wrongTrigger != null)
                zone.RegisterWrongPathTrigger(wrongTrigger);
        }
    }

    /// <summary>
    /// Checks the build before the player is allowed to start (called by MazeGenerator after OnMazeReady):
    /// 1. Every junction's recorded correct exit is physically open (both halves of the wall down) and is
    ///    one of its shortest ways to the portal (ties are fine, since they're picked at random).
    /// 2. Following the correct exits from the start actually reaches the portal, through passable walls only.
    /// 3. Every junction ON that route has a live GDD page, or the player already holds the page.
    ///    Junctions off the route never get a page by design, so they aren't checked for one.
    /// </summary>
    public bool ValidateBuild(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit)
    {
        // 1. Every junction's recorded exit.
        foreach (var kvp in _zoneByCell)
        {
            Vector2Int coord = kvp.Key;
            IntersectionZone zone = kvp.Value;
            if (zone == null) continue;

            if (!zone.HasCorrectExit)
            {
                Debug.LogWarning($"ValidateBuild: junction at {coord} has no recorded correct exit.");
                return false;
            }

            int bestDist = int.MaxValue;
            foreach (var exit in GetOpenExits(grid[coord.x, coord.y], width, depth))
            {
                if (!IsPassable(grid, width, depth, coord, exit)) continue;
                int dist = distanceFromExit[coord.x + exit.x, coord.y + exit.y];
                if (dist >= 0 && dist < bestDist) bestDist = dist;
            }

            Vector2Int target = coord + zone.CorrectExit;
            bool recordedOk = IsPassable(grid, width, depth, coord, zone.CorrectExit)
                && distanceFromExit[target.x, target.y] == bestDist;

            if (!recordedOk)
            {
                Debug.LogWarning($"ValidateBuild: junction at {coord} points to {zone.CorrectExit}, which is blocked or not a shortest way out.");
                return false;
            }
        }

        // 2 + 3. Walk the route the pages were placed along.
        Vector2Int current = Vector2Int.zero;
        int safety = width * depth;
        while (distanceFromExit[current.x, current.y] != 0)
        {
            if (safety-- <= 0 || !NextStepTowardExit(current, out Vector2Int next)
                || !IsPassable(grid, width, depth, current, next - current))
            {
                Debug.LogWarning($"ValidateBuild: the correct route from the start gets stuck at {current} and never reaches the exit.");
                return false;
            }

            if (_zoneByCell.TryGetValue(next, out IntersectionZone zone) && zone != null)
            {
                PuzzleData data = zone.GetAssignedPuzzleData();
                if (data == null)
                {
                    Debug.LogWarning($"ValidateBuild: junction at {next} has no assigned puzzle data (AssignedData not set?).");
                    return false;
                }

                bool alreadyHeld = GDDManager.Instance != null && GDDManager.Instance.HasCollected(data);
                bool hasLivePage = _pageInstancesByData.TryGetValue(data, out List<GameObject> instances)
                    && instances.Exists(o => o != null);

                if (!alreadyHeld && !hasLivePage)
                {
                    Debug.LogWarning($"ValidateBuild: junction at {next} ('{data.pageTitle}') is on the route but has no GDD page.");
                    return false;
                }
            }

            current = next;
        }

        return true;
    }

    // A step only counts as open if BOTH cells have their half of the wall down.
    private bool IsPassable(MazeCell[,] grid, int width, int depth, Vector2Int from, Vector2Int dir)
    {
        Vector2Int to = from + dir;
        if (to.x < 0 || to.y < 0 || to.x >= width || to.y >= depth) return false;
        MazeCell a = grid[from.x, from.y];
        MazeCell b = grid[to.x, to.y];

        if (dir == Vector2Int.right)       return !a.HasRightWall() && !b.HasLeftWall();
        if (dir == Vector2Int.left)        return !a.HasLeftWall()  && !b.HasRightWall();
        if (dir == new Vector2Int(0, 1))   return !a.HasFrontWall() && !b.HasBackWall();
        if (dir == new Vector2Int(0, -1))  return !a.HasBackWall()  && !b.HasFrontWall();
        return false;
    }

    
}

