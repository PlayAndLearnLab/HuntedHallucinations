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

    void OnDestroy()
    {
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
        foreach (GameObject obj in _spawnedObjects)
            if (obj != null) Destroy(obj);
        _spawnedObjects.Clear();
        _zoneByCell.Clear();
        _pageInstancesByData.Clear();
    }

    public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellWidth, float cellDepth)
    {
        EnsureGDDSubscription();

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

    // Walks the single correct route from (0,0) to the exit — following
    // strictly decreasing distance-to-exit at every step, exactly like a
    // player who never takes a wrong turn. For every intersection it passes
    // through, drops one GDD page at a random cell somewhere in the corridor
    // segment since the previous intersection (or the start) — never on the
    // intersection cell itself. Skips spawning entirely if the player already
    // holds that page. Because the maze is a perfect maze (no loops), this
    // path and the "one correct next step" at each cell are always unambiguous.
    private void SpawnGDDPages(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellWidth, float cellDepth)
    {
        if (_gddPagePrefab == null) return;

        Vector2Int current = new Vector2Int(0, 0);
        Vector2Int previous = new Vector2Int(-1, -1); // sentinel, never matches a real cell

        // Cells traversed since the last intersection (or the start),
        // excluding any intersection cell — the candidate pool for random placement.
        List<MazeCell> segmentCells = new List<MazeCell> { grid[0, 0] };

        while (distanceFromExit[current.x, current.y] != 0)
        {
            MazeCell currentCell = grid[current.x, current.y];
            List<Vector2Int> exits = GetOpenExits(currentCell, width, depth);
            int currentDist = distanceFromExit[current.x, current.y];

            Vector2Int next = current;
            bool foundNext = false;

            foreach (var exit in exits)
            {
                Vector2Int candidate = current + exit;
                if (candidate == previous) continue;

                if (distanceFromExit[candidate.x, candidate.y] == currentDist - 1)
                {
                    next = candidate;
                    foundNext = true;
                    break;
                }
            }

            if (!foundNext) break; // shouldn't happen against a valid distance field, but bail out safely

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

            previous = current;
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

        // Find the correct exit
        Vector2Int bestExit = exits[0];
        int bestDist = int.MaxValue;

        foreach (var exit in exits)
        {
            int targetX = x + exit.x;
            int targetZ = z + exit.y; // Vector2Int uses .y for the second parameter

            int dist = distanceFromExit[targetX, targetZ];

            
            // Ignore dead ends/unreachable paths (-1) entirely
            if (dist == -1) continue; 

            if (dist < bestDist)
            {
                bestDist = dist;
                bestExit = exit;
            }
        }

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
    /// Re-derives the correct exit at every intersection independently from a
    /// fresh distance field and checks it against what was recorded at build
    /// time, and checks every intersection has a live GDD page. Called by
    /// MazeGenerator after OnMazeReady, before the player is allowed to start.
    /// </summary>
    public bool ValidateBuild(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit)
    {
        foreach (var kvp in _zoneByCell)
        {
            Vector2Int coord = kvp.Key;
            IntersectionZone zone = kvp.Value;
            if (zone == null) continue;

            MazeCell cell = grid[coord.x, coord.y];
            List<Vector2Int> exits = GetOpenExits(cell, width, depth);

            Vector2Int recomputedExit = Vector2Int.zero;
            int bestDist = int.MaxValue;
            bool foundValid = false;

            foreach (var exit in exits)
            {
                int tx = coord.x + exit.x;
                int tz = coord.y + exit.y;
                int dist = distanceFromExit[tx, tz];
                if (dist == -1) continue;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    recomputedExit = exit;
                    foundValid = true;
                }
            }

            if (!foundValid || !zone.HasCorrectExit || zone.CorrectExit != recomputedExit)
            {
                Debug.LogWarning($"ValidateBuild: exit mismatch at {coord} — recorded {zone.CorrectExit}, recomputed {recomputedExit}.");
                return false;
            }

            PuzzleData data = zone.GetAssignedPuzzleData();
            if (data == null)
            {
                Debug.LogWarning($"ValidateBuild: intersection at {coord} has no assigned puzzle data (AssignedData not set?).");
                return false;
            }

            bool hasLivePage = _pageInstancesByData.TryGetValue(data, out List<GameObject> instances)
                && instances.Exists(o => o != null);

            if (!hasLivePage)
            {
                Debug.LogWarning($"ValidateBuild: intersection at {coord} ('{data.pageTitle}') has no live GDD page.");
                return false;
            }
        }

        return true;
    }

    
}

