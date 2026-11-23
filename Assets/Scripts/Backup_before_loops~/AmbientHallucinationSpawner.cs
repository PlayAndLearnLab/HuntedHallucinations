using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Drops "ambient" items into maze corridors (never on intersections).
///
/// 1) Puzzle-based items - appear each time the player FINISHES a junction (walks out of it).
///    Built from the puzzles of every junction already passed whose GDD page the player holds,
///    so they can always judge the item against their book, and never from the junction ahead:
///      real item = the puzzle's correct asset / true statement (bait: flagging it is a mistake)
///      fake item = a hallucinated asset / false statement
///    Nothing of this kind appears before the first junction is done.
///
/// 2) Obvious fakes - assets that clearly belong to another game. Placed once per maze build,
///    on the route near the start, and need no GDD page to spot. Always fake.
///
/// Items are only placed on the route toward the exit so a correct path never misses them.
/// </summary>
public class AmbientHallucinationSpawner : MonoBehaviour
{
    [SerializeField] private MazeGenerator _mazeGenerator;

    [Tooltip("Same world-space TMP label prefab the TextPuzzleSpawner uses (needs a TextMeshProUGUI child).")]
    [SerializeField] private GameObject _textLabelPrefab;
    [SerializeField] private float _labelHeight = 1.5f;
    [Tooltip("Height of the floor surface relative to the cell's position. Raise it if items still look buried.")]
    [SerializeField] private float _floorOffset = 0f;

    [Header("Spawn rules")]
    [Range(0f, 1f)] [SerializeField] private float _spawnChancePerPage = 0.85f;
    [Range(0f, 1f)] [SerializeField] private float _fakeChance = 0.6f;
    [Tooltip("How many puzzle-based items each completed junction can spawn (random between min and max, inclusive).")]
    [SerializeField] private int _minItemsPerPage = 1;
    [SerializeField] private int _maxItemsPerPage = 3;
    [Tooltip("Cap on unresolved items alive at once.")]
    [SerializeField] private int _maxActiveV2 = 8;
    [Tooltip("How many steps ahead of the player ALONG THE ROUTE TO THE EXIT the item may appear. " +
             "Items are only placed on that route, so the player can't miss them by taking the correct path.")]
    [SerializeField] private int _minPathDistance = 3;
    [SerializeField] private int _maxPathDistance = 8;

    [Header("Path top-up (keeps the corridors populated between junctions)")]
    [Tooltip("After the first finished junction, the spawner keeps this many unseen puzzle-based items waiting AHEAD of the player " +
             "on the route, so every stretch between junctions has something to check.")]
    [SerializeField] private bool _topUpEnabled = true;
    [SerializeField] private int _itemsAhead = 3;
    [Tooltip("In the verification phase the target grows by this much.")]
    [SerializeField] private int _verifyPhaseExtraAhead = 1;
    [SerializeField] private float _topUpCheckInterval = 1.5f;
    [SerializeField] private int _topUpMinStep = 4;
    [SerializeField] private int _topUpMaxStep = 10;

    [Header("Obvious fakes (from the start of the run)")]
    [Tooltip("Assets that clearly come from another game. Placed once per maze build near the start; always fake; no GDD page needed.")]
    [SerializeField] private GameObject[] _obviousFakePrefabs;
    [SerializeField] private int _obviousFakeCount = 2;
    [Tooltip("Steps along the route from the start cell.")]
    [SerializeField] private int _obviousMinStep = 3;
    [SerializeField] private int _obviousMaxStep = 12;

    [Header("Difficulty ramp (make players verify instead of trusting their gut)")]
    [Tooltip("After this many finished junctions the game enters its 'verification phase'.")]
    [SerializeField] private int _verifyPhaseAfterJunctions = 3;
    [Tooltip("Extra puzzle-based items per junction in that phase (and the live-item cap grows by the same amount).")]
    [SerializeField] private int _verifyPhaseExtraItems = 2;
    [Tooltip("Real/fake split in that phase. 0.5 = half the items are genuine, so 'it looks fine' or 'it looks odd' stops being enough; only the book settles it.")]
    [Range(0f, 1f)] [SerializeField] private float _verifyPhaseFakeChance = 0.5f;
    [Tooltip("Genuine decoys arrive this much faster in that phase (0.6 = 40% sooner).")]
    [SerializeField] private float _verifyPhaseDecoyScale = 0.6f;
    [Tooltip("Obvious fakes only appear before this many junctions are finished (including after a maze rebuild).")]
    [SerializeField] private int _obviousStopAfterJunctions = 2;

    [Header("Genuine decoys (so the F prompt is not always a warning)")]
    [Tooltip("Every so often a GENUINE item (real asset / true statement from a puzzle already passed) is dropped on the route, " +
             "so seeing the F prompt does not mean something is wrong. Flagging one costs the normal penalty.")]
    [SerializeField] private bool _decoysEnabled = true;
    [SerializeField] private float _decoyIntervalMin = 20f;
    [SerializeField] private float _decoyIntervalMax = 40f;

    [Header("Debug")]
    [SerializeField] private bool _debugLogs = true;

    private static AmbientHallucinationSpawner _instance;
    private GameObject _player;

    // Puzzles of junctions the player has walked through. Their pages are the "past pages".
    private readonly HashSet<PuzzleData> _completed = new HashSet<PuzzleData>();

    private float _nextDecoyTime = float.MaxValue; // armed when the first junction is finished
    private int _junctionsFinished;

    private bool VerifyPhase => _junctionsFinished >= _verifyPhaseAfterJunctions;
    private int MaxActiveNow => _maxActiveV2 + (VerifyPhase ? _verifyPhaseExtraItems : 0);

    private float NextDecoyDelay()
    {
        float delay = Random.Range(_decoyIntervalMin, Mathf.Max(_decoyIntervalMin, _decoyIntervalMax));
        return VerifyPhase ? delay * _verifyPhaseDecoyScale : delay;
    }

    void Awake() { _instance = this; }

    void OnEnable()  { IntersectionZone.PlayerLeft += HandlePuzzleCompleted; }
    void OnDisable() { IntersectionZone.PlayerLeft -= HandlePuzzleCompleted; }

    void Start() { ValidateLabelPrefab(); }

    void Update()
    {
        TopUpTick();
        DecoyTick();
    }

    // Keeps a few unseen items waiting ahead of the player on the route, so the stretch between junctions is never empty.
    private float _nextTopUpTime;
    private void TopUpTick()
    {
        if (!_topUpEnabled || _junctionsFinished == 0 || Time.time < _nextTopUpTime) return;
        _nextTopUpTime = Time.time + _topUpCheckInterval;

        if (_mazeGenerator == null || GDDManager.Instance == null) return;
        if (TimerManager.Instance != null && !TimerManager.Instance.IsRunning) return;
        if (_player == null) _player = GameObject.FindWithTag("Player");
        if (_player == null) return;

        int target = _itemsAhead + (VerifyPhase ? _verifyPhaseExtraAhead : 0);
        float radius = (_topUpMaxStep + 2) * Mathf.Max(_mazeGenerator.CellWidth, _mazeGenerator.CellDepth);
        if (CountWaitingNear(_player.transform.position, radius) >= target) return;
        if (CountAlive() >= MaxActiveNow + 4) return; // hard safety cap

        List<PuzzleData> candidates = GetPassedPagePuzzles();
        if (candidates.Count == 0) return;

        MazeCell cell = PickCell(null, _topUpMinStep, _topUpMaxStep);
        if (cell == null) return;

        PuzzleData data = candidates[Random.Range(0, candidates.Count)];
        bool fake = Random.value < (VerifyPhase ? _verifyPhaseFakeChance : _fakeChance);
        if (TrySpawn(data, cell, fake)) Log($"top-up: spawned a {(fake ? "FAKE" : "REAL")} '{data.pageTitle}' item ahead of the player.");
    }

    // Unresolved items the player has not yet come close to, within `radius` of `pos`.
    private static int CountWaitingNear(Vector3 pos, float radius)
    {
        int n = 0;
        float r2 = radius * radius;
        foreach (AmbientHallucination a in AmbientHallucination.Active)
        {
            if (a == null || a.Resolved || a.Encountered) continue;
            Vector3 d = a.transform.position - pos;
            d.y = 0f;
            if (d.sqrMagnitude <= r2) n++;
        }
        return n;
    }

    private void DecoyTick()
    {
        if (!_decoysEnabled || Time.time < _nextDecoyTime) return;
        _nextDecoyTime = Time.time + NextDecoyDelay();

        if (_mazeGenerator == null || GDDManager.Instance == null) return;
        if (TimerManager.Instance != null && !TimerManager.Instance.IsRunning) return;
        if (CountAlive() >= MaxActiveNow) return;

        List<PuzzleData> candidates = GetPassedPagePuzzles();
        if (candidates.Count == 0) return;

        MazeCell cell = PickCell(null, _minPathDistance, _maxPathDistance);
        if (cell == null) { Log("decoy skipped: no free corridor cell on the route."); return; }

        PuzzleData data = candidates[Random.Range(0, candidates.Count)];
        if (TrySpawn(data, cell, false)) Log($"decoy: spawned a GENUINE '{data.pageTitle}' item.");
    }

    // Every puzzle already passed AND whose page the player holds (so they can verify it).
    private List<PuzzleData> GetPassedPagePuzzles()
    {
        var list = new List<PuzzleData>();
        if (GDDManager.Instance == null) return list;
        foreach (PuzzleData p in _completed)
            if (p != null && GDDManager.Instance.HasCollected(p)) list.Add(p);
        return list;
    }

    private static int CountAlive()
    {
        int alive = 0;
        foreach (AmbientHallucination a in AmbientHallucination.Active)
            if (a != null && !a.Resolved) alive++;
        return alive;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // Catches the classic mistake of dragging a UI object from the Hierarchy instead of the prefab asset.
    private void ValidateLabelPrefab()
    {
        if (_textLabelPrefab == null) return; // fine if the player only ever gets visual items

        if (_textLabelPrefab.scene.IsValid())
        {
            Debug.LogError("AmbientHallucinationSpawner: 'Text Label Prefab' is a SCENE object ('" + _textLabelPrefab.name +
                           "'), not a prefab asset. Assign the world-space text label PREFAB from the Project window " +
                           "(the one used by TextPuzzleSpawner, e.g. Prefab/Puzzles/TextPuzzle).", this);
            return;
        }

        Canvas canvas = _textLabelPrefab.GetComponentInChildren<Canvas>(true);
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
            Debug.LogWarning("AmbientHallucinationSpawner: the text label prefab has no World Space Canvas, " +
                             "so its text will not appear in the corridor.", this);
    }

    /// <summary>Called from IntersectionDetector.ClearIndicators() whenever the maze is torn down.</summary>
    public static void ClearAll()
    {
        AuditFlagController.ClearPhantoms(); // phantom spots belong to the maze being torn down too
        var copy = new List<AmbientHallucination>(AmbientHallucination.Active);
        foreach (AmbientHallucination item in copy)
        {
            if (item == null) continue;
            Destroy(item.gameObject);
        }
    }

    /// <summary>
    /// Picks a random corridor spot on the route (same rules as item placement) for AuditFlagController's
    /// phantom prompts. Nothing is spawned there: it is just a place where the F prompt will lie.
    /// </summary>
    public static bool TryPickPhantomSpot(out Vector3 position)
    {
        position = Vector3.zero;
        if (_instance == null || _instance._mazeGenerator == null) return false;

        MazeCell cell = _instance.PickCell(null, _instance._minPathDistance, _instance._maxPathDistance);
        if (cell == null) return false;

        position = cell.transform.position;
        return true;
    }

    /// <summary>Called by MazeGenerator once a maze build has been validated and kept.</summary>
    public static void OnMazeBuilt()
    {
        if (_instance != null) _instance.SpawnObviousFakes();
    }

    // ---------- obvious fakes ----------

    private void SpawnObviousFakes()
    {
        if (_mazeGenerator == null || _obviousFakeCount <= 0) return;
        if (_junctionsFinished >= _obviousStopAfterJunctions)
        {
            Log("obvious fakes skipped: the player is past the intro stretch.");
            return;
        }
        if (_obviousFakePrefabs == null || _obviousFakePrefabs.Length == 0) return;

        MazeCell[,] grid = _mazeGenerator.GetMazeGrid();
        if (grid == null) return;

        int spawned = 0;
        for (int i = 0; i < _obviousFakeCount; i++)
        {
            MazeCell cell = PickCell(grid[0, 0], _obviousMinStep, _obviousMaxStep);
            GameObject prefab = RandomNonNull(_obviousFakePrefabs);
            if (cell == null || prefab == null) break;

            Vector3 pos = cell.transform.position + new Vector3(
                Random.Range(-0.2f, 0.2f) * _mazeGenerator.CellWidth, 0f,
                Random.Range(-0.2f, 0.2f) * _mazeGenerator.CellDepth);

            GameObject obj = Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            GroundSnap.LiftAboveFloor(obj, cell.transform.position.y + _floorOffset);

            AmbientHallucination item = obj.AddComponent<AmbientHallucination>();
            item.Setup(true, "an asset from another game", false);
            item.Obvious = true;
            spawned++;
        }
        Log($"maze built: placed {spawned} obvious fake(s) near the start.");
    }

    // ---------- puzzle-based items ----------

    private void HandlePuzzleCompleted(IntersectionZone zone)
    {
        if (_mazeGenerator == null || GDDManager.Instance == null)
        {
            Log("junction finished but MazeGenerator is not assigned on the spawner.");
            return;
        }

        _junctionsFinished++;
        PuzzleData done = zone != null ? zone.GetAssignedPuzzleData() : null;
        if (done != null) _completed.Add(done);

        // The decoy clock starts the first time a junction is finished.
        if (_nextDecoyTime == float.MaxValue)
            _nextDecoyTime = Time.time + NextDecoyDelay();

        if (Random.value > _spawnChancePerPage) { Log("junction finished, spawn roll failed (normal)."); return; }

        List<PuzzleData> candidates = GetPassedPagePuzzles();
        if (candidates.Count == 0) { Log("skipped: no finished junction with a collected page yet."); return; }

        int lo = Mathf.Max(1, _minItemsPerPage);
        int hi = Mathf.Max(lo, _maxItemsPerPage);
        bool verify = VerifyPhase;
        int wanted = Random.Range(lo, hi + 1) + (verify ? _verifyPhaseExtraItems : 0);
        float fakeChance = verify ? _verifyPhaseFakeChance : _fakeChance;
        int spawned = 0;

        for (int i = 0; i < wanted; i++)
        {
            int alive = CountAlive();
            if (alive >= MaxActiveNow) { Log($"stopped: {alive} items already alive (max {MaxActiveNow})."); break; }

            // Each new item takes a different cell: IsEligible() rejects cells that already hold one.
            MazeCell cell = PickCell(null, _minPathDistance, _maxPathDistance);
            if (cell == null) { Log("stopped: no more free corridor cells on the route to the exit within the min/max step range."); break; }

            PuzzleData data = candidates[Random.Range(0, candidates.Count)];
            bool fake = Random.value < fakeChance;
            if (TrySpawn(data, cell, fake)) spawned++;
            else Log($"skipped one: '{data.pageTitle}' has no usable {(fake ? "hallucinated" : "real")} prefab/statement (or the text label prefab is missing).");
        }

        Log($"junction #{_junctionsFinished} finished{(verify ? " [verification phase]" : "")}: spawned {spawned} of {wanted} wanted items (from {candidates.Count} past page(s)).");
    }

    private void Log(string message, Object context = null)
    {
        if (_debugLogs) Debug.Log($"[AmbientHallucination] {message}", context);
    }

    // ---------- where ----------

    // Walks the single correct route toward the exit, starting at `origin` (or at the player's cell
    // when origin is null), and returns a random eligible corridor cell between minStep and maxStep.
    private MazeCell PickCell(MazeCell origin, int minStep, int maxStep)
    {
        MazeCell[,] grid = _mazeGenerator.GetMazeGrid();
        int[,] distToExit = _mazeGenerator.GetDistanceFromExit();
        if (grid == null || distToExit == null) return null;

        int w = grid.GetLength(0);
        int d = grid.GetLength(1);

        MazeCell current = origin;
        if (current == null)
        {
            if (_player == null) _player = GameObject.FindWithTag("Player");
            if (_player == null) return null;
            current = NearestCell(grid, _player.transform.position);
        }
        if (current == null) return null;

        // Strictly decreasing distance-to-exit, like IntersectionDetector does for pages.
        // Only cells on this route are candidates, so a player who takes the right turns
        // always walks past every item that was placed.
        var candidates = new List<MazeCell>();

        for (int step = 0; step <= maxStep; step++)
        {
            if (step >= minStep && IsEligible(current, w, d))
                candidates.Add(current);

            int currentDist = distToExit[current.GridX, current.GridZ];
            if (currentDist <= 0) break; // reached the exit

            MazeCell next = null;
            foreach (Vector2Int n in OpenNeighbors(current, w, d))
            {
                if (distToExit[n.x, n.y] == currentDist - 1) { next = grid[n.x, n.y]; break; }
            }
            if (next == null) break;
            current = next;
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    private bool IsEligible(MazeCell cell, int w, int d)
    {
        if (cell.GridX == 0 && cell.GridZ == 0) return false;            // start cell
        if (OpenNeighbors(cell, w, d).Count >= 3) return false;          // intersections belong to the puzzles
        if (cell.GetComponentInChildren<ExitPortal>() != null) return false;

        foreach (AmbientHallucination a in AmbientHallucination.Active)
        {
            if (a == null) continue;
            Vector3 delta = a.transform.position - cell.transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 1.5f * 1.5f) return false;          // don't stack items
        }
        return true;
    }

    private static List<Vector2Int> OpenNeighbors(MazeCell cell, int w, int d)
    {
        var result = new List<Vector2Int>(4);
        int x = cell.GridX;
        int z = cell.GridZ;
        if (x + 1 < w && !cell.HasRightWall()) result.Add(new Vector2Int(x + 1, z));
        if (x - 1 >= 0 && !cell.HasLeftWall()) result.Add(new Vector2Int(x - 1, z));
        if (z + 1 < d && !cell.HasFrontWall()) result.Add(new Vector2Int(x, z + 1));
        if (z - 1 >= 0 && !cell.HasBackWall()) result.Add(new Vector2Int(x, z - 1));
        return result;
    }

    private static MazeCell NearestCell(MazeCell[,] grid, Vector3 worldPos)
    {
        MazeCell best = null;
        float bestSqr = float.MaxValue;
        foreach (MazeCell c in grid)
        {
            if (c == null) continue;
            Vector3 delta = c.transform.position - worldPos;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = c; }
        }
        return best;
    }

    // ---------- what ----------

    private bool TrySpawn(PuzzleData data, MazeCell cell, bool fake)
    {
        float jx = Random.Range(-0.2f, 0.2f) * _mazeGenerator.CellWidth;
        float jz = Random.Range(-0.2f, 0.2f) * _mazeGenerator.CellDepth;
        Vector3 basePos = cell.transform.position + new Vector3(jx, 0f, jz);

        GameObject obj;
        bool billboard = false;

        if (data is VisualPuzzleData visual)
        {
            GameObject prefab = fake
                ? RandomNonNull(visual.hallucinatedPrefabs, visual.ambientHallucinatedPrefabs)
                : RandomNonNull(new[] { visual.correctPrefab }, visual.ambientCorrectPrefabs);
            if (prefab == null) return false;
            obj = Instantiate(prefab, basePos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            GroundSnap.LiftAboveFloor(obj, cell.transform.position.y + _floorOffset); // never leave an asset buried in the floor
        }
        else if (data is TextPuzzleData text)
        {
            string statement = fake
                ? RandomNonEmpty(text.falseStatements, text.ambientFalseStatements)
                : RandomNonEmpty(new[] { text.trueStatement }, text.ambientTrueStatements);
            if (string.IsNullOrEmpty(statement) || _textLabelPrefab == null) return false;

            obj = Instantiate(_textLabelPrefab, basePos + Vector3.up * _labelHeight, Quaternion.identity);
            TextMeshProUGUI tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp == null)
            {
                Debug.LogError("AmbientHallucinationSpawner: TextMeshProUGUI not found in label prefab");
                Destroy(obj);
                return false;
            }
            tmp.text = statement;
            billboard = true;
        }
        else return false;

        AmbientHallucination item = obj.AddComponent<AmbientHallucination>();
        item.Setup(fake, data.pageTitle, billboard);
        item.Source = data;

        Log($"SPAWNED {(fake ? "FAKE" : "REAL")} '{data.pageTitle}' at cell [{cell.GridX},{cell.GridZ}]", obj);
        return true;
    }

    // Picks one non-empty entry from the combined pools (any pool may be null).
    private static GameObject RandomNonNull(params GameObject[][] pools)
    {
        var valid = new List<GameObject>();
        foreach (GameObject[] pool in pools)
        {
            if (pool == null) continue;
            foreach (GameObject g in pool) if (g != null) valid.Add(g);
        }
        return valid.Count == 0 ? null : valid[Random.Range(0, valid.Count)];
    }

    private static string RandomNonEmpty(params string[][] pools)
    {
        var valid = new List<string>();
        foreach (string[] pool in pools)
        {
            if (pool == null) continue;
            foreach (string s in pool) if (!string.IsNullOrEmpty(s)) valid.Add(s);
        }
        return valid.Count == 0 ? null : valid[Random.Range(0, valid.Count)];
    }
}
