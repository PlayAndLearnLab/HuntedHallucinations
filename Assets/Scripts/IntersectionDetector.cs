using UnityEngine;
using System.Collections.Generic;

public class IntersectionDetector : MonoBehaviour
{
    [SerializeField] private GameObject _greenArrowPrefab;
    [SerializeField] private GameObject _redCrossPrefab;
    [SerializeField] private float _indicatorHeight = 1.5f;
    [SerializeField] private GameObject _wrongPathTriggerPrefab;
    [SerializeField] private GameObject _intersectionZonePrefab;

    private List<GameObject> _spawnedObjects = new List<GameObject>();

    public void ClearIndicators()
    {
        foreach (GameObject obj in _spawnedObjects)
            if (obj != null) Destroy(obj);
        _spawnedObjects.Clear();
    }

    public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellWidth, float cellDepth)
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                MazeCell cell = grid[x, z];
                List<Vector2Int> openExits = GetOpenExits(cell, width, depth);

                if (openExits.Count >= 3)
                    SpawnIndicators(cell, openExits, distanceFromExit, cellWidth, cellDepth);
            }
        }
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

    private void SpawnIndicators(MazeCell cell, List<Vector2Int> exits, int[,] distanceFromExit, float cellWidth, float cellDepth)
    {
        int x = cell.GridX;
        int z = cell.GridZ;

        Vector2Int bestExit = exits[0];
        int bestDist = int.MaxValue;
        foreach (var exit in exits)
        {
            int dist = distanceFromExit[x + exit.x, z + exit.y];
            if (dist < bestDist) { bestDist = dist; bestExit = exit; }
        }

        IntersectionZone zone = null;
        if (_intersectionZonePrefab != null)
        {
            GameObject zoneObj = Instantiate(_intersectionZonePrefab, cell.transform.position + Vector3.up * 0.2f, Quaternion.identity);
            _spawnedObjects.Add(zoneObj);
            
            // Scaled slightly smaller (0.9f) so the player must distinctly enter the intersection center 
            // before any arming sequences register!
            zoneObj.transform.localScale = new Vector3(cellWidth * 0.9f, 2f, cellDepth * 0.9f);
            zone = zoneObj.GetComponent<IntersectionZone>();
        }

        foreach (var exit in exits)
        {
            bool isCorrect = (exit == bestExit);

            Vector3 indicatorPos = cell.transform.position
                + new Vector3(exit.x * cellWidth, 0, exit.y * cellDepth) * 0.35f
                + Vector3.up * _indicatorHeight;

            Quaternion indicatorRot = Quaternion.LookRotation(new Vector3(exit.x, 0, exit.y));
            GameObject indicator = Instantiate(isCorrect ? _greenArrowPrefab : _redCrossPrefab, indicatorPos, indicatorRot);
            _spawnedObjects.Add(indicator);

            if (!isCorrect && _wrongPathTriggerPrefab != null && zone != null)
            {
                // Push the trigger slightly further down the path corridor (0.55f) so it sits outside the zone boundary
                Vector3 triggerPos = cell.transform.position
                    + new Vector3(exit.x * cellWidth, 0, exit.y * cellDepth) * 0.55f
                    + Vector3.up * 0.5f;

                GameObject triggerObj = Instantiate(_wrongPathTriggerPrefab, triggerPos, indicatorRot);
                _spawnedObjects.Add(triggerObj);

                float transverseWidth = (exit.x != 0) ? cellDepth : cellWidth;
                triggerObj.transform.localScale = new Vector3(transverseWidth * 0.9f, 2f, 0.2f);

                WrongPathTrigger wrongTrigger = triggerObj.GetComponent<WrongPathTrigger>();
                if (wrongTrigger != null)
                    zone.RegisterWrongPathTrigger(wrongTrigger);
            }
        }
    }
}

// VERSION WITH CELL SCALING

// using UnityEngine;
// using System.Collections.Generic;

// public class IntersectionDetector : MonoBehaviour
// {
//     [SerializeField] private GameObject _greenArrowPrefab;
//     [SerializeField] private GameObject _redCrossPrefab;
//     [SerializeField] private float _indicatorHeight = 1.5f;
//     [SerializeField] private GameObject _wrongPathTriggerPrefab;
//     [SerializeField] private GameObject _intersectionZonePrefab;

//     private List<GameObject> _spawnedObjects = new List<GameObject>();

//     public void ClearIndicators()
//     {
//         foreach (GameObject obj in _spawnedObjects)
//             if (obj != null) Destroy(obj);
//         _spawnedObjects.Clear();
//     }

//     public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellWidth, float cellDepth)
//     {
//         for (int x = 0; x < width; x++)
//             for (int z = 0; z < depth; z++)
//             {
//                 MazeCell cell = grid[x, z];
//                 List<Vector2Int> openExits = GetOpenExits(cell, width, depth);
//                 if (openExits.Count >= 3)
//                     SpawnIndicators(cell, openExits, distanceFromExit, cellWidth, cellDepth);
//             }
//     }

//     private List<Vector2Int> GetOpenExits(MazeCell cell, int width, int depth)
//     {
//         List<Vector2Int> exits = new List<Vector2Int>();
//         int x = cell.GridX;
//         int z = cell.GridZ;

//         if (x + 1 < width  && !cell.HasRightWall()) exits.Add(Vector2Int.right);
//         if (x - 1 >= 0     && !cell.HasLeftWall())  exits.Add(Vector2Int.left);
//         if (z + 1 < depth  && !cell.HasFrontWall()) exits.Add(new Vector2Int(0, 1));
//         if (z - 1 >= 0     && !cell.HasBackWall())  exits.Add(new Vector2Int(0, -1));

//         return exits;
//     }

//     private void SpawnIndicators(MazeCell cell, List<Vector2Int> exits, int[,] distanceFromExit, float cellWidth, float cellDepth)
//     {
//         int x = cell.GridX;
//         int z = cell.GridZ;

//         // Find the correct exit — the neighbor closest to the exit
//         Vector2Int bestExit = exits[0];
//         int bestDist = int.MaxValue;
//         foreach (var exit in exits)
//         {
//             int dist = distanceFromExit[x + exit.x, z + exit.y];
//             if (dist < bestDist) { bestDist = dist; bestExit = exit; }
//         }

//         // Spawn intersection zone at the cell center
//         IntersectionZone zone = null;
//         if (_intersectionZonePrefab != null)
//         {
//             GameObject zoneObj = Instantiate(
//                 _intersectionZonePrefab,
//                 cell.transform.position + Vector3.up * 0.5f,
//                 Quaternion.identity);

//             zoneObj.transform.localScale = new Vector3(cellWidth * 0.8f, 1.5f, cellDepth * 0.8f);
//             zone = zoneObj.GetComponent<IntersectionZone>();
//             _spawnedObjects.Add(zoneObj);
//         }

//         // Spawn one indicator and optionally one trigger per exit
//         foreach (var exit in exits)
//         {
//             bool isCorrect = (exit == bestExit);

//             // Visual indicator — offset along the correct axis for this exit direction
//             Vector3 indicatorPos = cell.transform.position
//                 + new Vector3(exit.x * cellWidth * 0.4f, 0, exit.y * cellDepth * 0.4f)
//                 + Vector3.up * _indicatorHeight;

//             Quaternion indicatorRot = Quaternion.LookRotation(new Vector3(exit.x, 0, exit.y));

//             GameObject indicator = Instantiate(
//                 isCorrect ? _greenArrowPrefab : _redCrossPrefab,
//                 indicatorPos,
//                 indicatorRot);
//             _spawnedObjects.Add(indicator);

//             // Wrong path trigger — placed one full cell deep so it fires after commitment
//             if (!isCorrect && _wrongPathTriggerPrefab != null && zone != null)
//             {
//                 Vector3 triggerPos = cell.transform.position
//                     + new Vector3(exit.x * cellWidth * 1.0f, 0, exit.y * cellDepth * 1.0f)
//                     + Vector3.up * 0.5f;

//                 GameObject triggerObj = Instantiate(_wrongPathTriggerPrefab, triggerPos, indicatorRot);
//                 triggerObj.transform.localScale = new Vector3(cellWidth * 0.8f, 1.5f, 0.3f);
//                 _spawnedObjects.Add(triggerObj);

//                 WrongPathTrigger wrongTrigger = triggerObj.GetComponent<WrongPathTrigger>();
//                 if (wrongTrigger != null)
//                     zone.RegisterWrongPathTrigger(wrongTrigger);
//             }
//         }
//     }
// }

// VERSION WITHOUT CELL SCALING

// using UnityEngine;
// using System.Collections.Generic;

// public class IntersectionDetector : MonoBehaviour
// {
//     [SerializeField] private GameObject _greenArrowPrefab;
//     [SerializeField] private GameObject _redCrossPrefab;
//     [SerializeField] private float _indicatorHeight = 1.5f;
//     [SerializeField] private GameObject _wrongPathTriggerPrefab;
//     [SerializeField] private GameObject _intersectionZonePrefab;

//     private List<GameObject> _spawnedObjects = new List<GameObject>();

//     public void ClearIndicators()
//     {
//         foreach (GameObject obj in _spawnedObjects)
//             if (obj != null) Destroy(obj);

//         _spawnedObjects.Clear();
//     }

//     // In IntersectionDetector.cs, change the method signature:
//     public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellWidth, float cellDepth)
//     {
//         for (int x = 0; x < width; x++)
//             for (int z = 0; z < depth; z++)
//             {
//                 MazeCell cell = grid[x, z];
//                 List<Vector2Int> openExits = GetOpenExits(cell, width, depth);
//                 if (openExits.Count >= 3)
//                     SpawnIndicators(cell, openExits, distanceFromExit, cellWidth, cellDepth);
//             }
//     }

//     // And SpawnIndicators signature:
//     private void SpawnIndicators(MazeCell cell, List<Vector2Int> exits, int[,] distanceFromExit, float cellWidth, float cellDepth)
//     {
//         // Replace all uses of cellSize with:
//         // cellSize * 0.4f along X → cellWidth * 0.4f
//         // cellSize * 0.4f along Z → cellDepth * 0.4f

//         // Indicator position — scale offset by the axis it moves along
//         Vector3 indicatorPos = cell.transform.position
//             + new Vector3(exit.x * cellWidth * 0.4f, 0, exit.y * cellDepth * 0.4f)
//             + Vector3.up * _indicatorHeight;

//         // Intersection zone scale
//         zoneObj.transform.localScale = new Vector3(cellWidth * 0.8f, 1.5f, cellDepth * 0.8f);

//         // Wrong path trigger position
//         Vector3 triggerPos = cell.transform.position
//             + new Vector3(exit.x * cellWidth * 1.0f, 0, exit.y * cellDepth * 1.0f)
//             + Vector3.up * 0.5f;

//         // Wrong path trigger scale
//         triggerObj.transform.localScale = new Vector3(cellWidth * 0.8f, 1.5f, 0.3f);
//     }

//     private List<Vector2Int> GetOpenExits(MazeCell cell, int width, int depth)
//     {
//         List<Vector2Int> exits = new List<Vector2Int>();
//         int x = cell.GridX;
//         int z = cell.GridZ;

//         if (x + 1 < width  && !cell.HasRightWall()) exits.Add(Vector2Int.right);
//         if (x - 1 >= 0     && !cell.HasLeftWall())  exits.Add(Vector2Int.left);
//         if (z + 1 < depth  && !cell.HasFrontWall()) exits.Add(new Vector2Int(0, 1));
//         if (z - 1 >= 0     && !cell.HasBackWall())  exits.Add(new Vector2Int(0, -1));

//         return exits;
//     }

//     // public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellSize)
//     // {
//     //     for (int x = 0; x < width; x++)
//     //     {
//     //         for (int z = 0; z < depth; z++)
//     //         {
//     //             MazeCell cell = grid[x, z];
//     //             List<Vector2Int> openExits = GetOpenExits(cell, width, depth);

//     //             if (openExits.Count >= 3)
//     //                 SpawnIndicators(cell, openExits, distanceFromExit, cellSize);
//     //         }
//     //     }
//     // }

//     // private void SpawnIndicators(MazeCell cell, List<Vector2Int> exits, int[,] distanceFromExit, float cellSize)
//     // {
//     //     int x = cell.GridX;
//     //     int z = cell.GridZ;

//     //     Vector2Int bestExit = exits[0];
//     //     int bestDist = int.MaxValue;
//     //     foreach (var exit in exits)
//     //     {
//     //         int dist = distanceFromExit[x + exit.x, z + exit.y];
//     //         if (dist < bestDist) { bestDist = dist; bestExit = exit; }
//     //     }

//     //     IntersectionZone zone = null;
//     //     if (_intersectionZonePrefab != null)
//     //     {
//     //         GameObject zoneObj = Instantiate(_intersectionZonePrefab, cell.transform.position + Vector3.up * 0.5f, Quaternion.identity);
//     //         // FIX: Keep the zone strictly inside the intersection center so it doesn't leak early into corridors
//     //         zoneObj.transform.localScale = new Vector3(cellSize * 0.6f, 1.5f, cellSize * 0.6f);
//     //         _spawnedObjects.Add(zoneObj);
//     //         zone = zoneObj.GetComponent<IntersectionZone>();
//     //     }

//     //     foreach (var exit in exits)
//     //     {
//     //         bool isCorrect = (exit == bestExit);

//     //         Vector3 indicatorPos = cell.transform.position
//     //             + new Vector3(exit.x, 0, exit.y) * (cellSize * 0.4f)
//     //             + Vector3.up * _indicatorHeight;

//     //         Quaternion indicatorRot = Quaternion.LookRotation(new Vector3(exit.x, 0, exit.y));
//     //         GameObject indicator = Instantiate(isCorrect ? _greenArrowPrefab : _redCrossPrefab, indicatorPos, indicatorRot);
//     //         _spawnedObjects.Add(indicator);

//     //         if (!isCorrect && _wrongPathTriggerPrefab != null && zone != null)
//     //         {
//     //             // FIX: Push the wrong path triggers slightly deeper into the corridor (0.55f instead of 0.5f)
//     //             Vector3 triggerPos = cell.transform.position
//     //                 + new Vector3(exit.x, 0, exit.y) * (cellSize * 0.55f)
//     //                 + Vector3.up * 0.5f;

//     //             GameObject triggerObj = Instantiate(_wrongPathTriggerPrefab, triggerPos, indicatorRot);
//     //             _spawnedObjects.Add(triggerObj);

//     //             // Ensure the trigger spans across the width of the path
//     //             triggerObj.transform.localScale = new Vector3(cellSize * 0.8f, 1.5f, 0.2f);

//     //             WrongPathTrigger wrongTrigger = triggerObj.GetComponent<WrongPathTrigger>();
//     //             if (wrongTrigger != null)
//     //                 zone.RegisterWrongPathTrigger(wrongTrigger);
//     //         }
//     //     }
//     // }
// }



// // using UnityEngine;
// // using System.Collections.Generic;

// // public class IntersectionDetector : MonoBehaviour
// // {
// //     [SerializeField] private GameObject _greenArrowPrefab;
// //     [SerializeField] private GameObject _redCrossPrefab;
// //     [SerializeField] private float _indicatorHeight = 1.5f;
// //     [SerializeField] private GameObject _wrongPathTriggerPrefab;

// //     // Assign a simple empty prefab with a BoxCollider (Is Trigger checked)
// //     [SerializeField] private GameObject _intersectionZonePrefab;

// //     private List<GameObject> _spawnedObjects = new List<GameObject>();

// //     public void ClearIndicators()
// //     {
// //         foreach (GameObject obj in _spawnedObjects)
// //             if (obj != null) Destroy(obj);

// //         _spawnedObjects.Clear();
// //     }

// //     public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellSize)
// //     {
// //         for (int x = 0; x < width; x++)
// //         {
// //             for (int z = 0; z < depth; z++)
// //             {
// //                 MazeCell cell = grid[x, z];
// //                 List<Vector2Int> openExits = GetOpenExits(cell, width, depth);

// //                 if (openExits.Count >= 3)
// //                     SpawnIndicators(cell, openExits, distanceFromExit, cellSize);
// //             }
// //         }
// //     }

// //     private List<Vector2Int> GetOpenExits(MazeCell cell, int width, int depth)
// //     {
// //         List<Vector2Int> exits = new List<Vector2Int>();
// //         int x = cell.GridX;
// //         int z = cell.GridZ;

// //         if (x + 1 < width  && !cell.HasRightWall()) exits.Add(Vector2Int.right);
// //         if (x - 1 >= 0     && !cell.HasLeftWall())  exits.Add(Vector2Int.left);
// //         if (z + 1 < depth  && !cell.HasFrontWall()) exits.Add(new Vector2Int(0, 1));
// //         if (z - 1 >= 0     && !cell.HasBackWall())  exits.Add(new Vector2Int(0, -1));

// //         return exits;
// //     }

// //     private void SpawnIndicators(MazeCell cell, List<Vector2Int> exits, int[,] distanceFromExit, float cellSize)
// //     {
// //         int x = cell.GridX;
// //         int z = cell.GridZ;

// //         // Find the correct exit
// //         Vector2Int bestExit = exits[0];
// //         int bestDist = int.MaxValue;
// //         foreach (var exit in exits)
// //         {
// //             int dist = distanceFromExit[x + exit.x, z + exit.y];
// //             if (dist < bestDist) { bestDist = dist; bestExit = exit; }
// //         }

// //         // Spawn the intersection zone at the cell center
// //         IntersectionZone zone = null;
// //         if (_intersectionZonePrefab != null)
// //         {
// //             GameObject zoneObj = Instantiate(
// //                 _intersectionZonePrefab,
// //                 cell.transform.position + Vector3.up * 0.5f,
// //                 Quaternion.identity);

// //             // Size the zone to fill the cell so the player reliably enters it
// //             zoneObj.transform.localScale = new Vector3(cellSize * 1.2f, 1.5f, cellSize * 1.2f);
// //             zone = zoneObj.GetComponent<IntersectionZone>();
// //             _spawnedObjects.Add(zoneObj);
// //         }

// //         // Spawn indicators and wrong path triggers
// //         foreach (var exit in exits)
// //         {
// //             bool isCorrect = (exit == bestExit);

// //             // Visual indicator
// //             Vector3 indicatorPos = cell.transform.position
// //                 + new Vector3(exit.x, 0, exit.y) * (cellSize * 0.4f)
// //                 + Vector3.up * _indicatorHeight;

// //             Quaternion indicatorRot = Quaternion.LookRotation(new Vector3(exit.x, 0, exit.y));
// //             GameObject indicator = Instantiate(isCorrect ? _greenArrowPrefab : _redCrossPrefab, indicatorPos, indicatorRot);
// //             _spawnedObjects.Add(indicator);

// //             // Wrong path trigger — only on wrong exits, only if we have both prefabs
// //             if (!isCorrect && _wrongPathTriggerPrefab != null && zone != null)
// //             {
// //                 Vector3 triggerPos = cell.transform.position
// //                     + new Vector3(exit.x, 0, exit.y) * (cellSize * 0.5f)
// //                     + Vector3.up * 0.5f;

// //                 GameObject triggerObj = Instantiate(_wrongPathTriggerPrefab, triggerPos, indicatorRot);
// //                 _spawnedObjects.Add(triggerObj);

// //                 // Size the trigger to span the corridor width so the player can't slip past
// //                 triggerObj.transform.localScale = new Vector3(cellSize * 0.8f, 1.5f, 0.3f);

// //                 WrongPathTrigger wrongTrigger = triggerObj.GetComponent<WrongPathTrigger>();
// //                 if (wrongTrigger != null)
// //                     zone.RegisterWrongPathTrigger(wrongTrigger);
// //             }
// //         }
// //     }
// // }







// // using UnityEngine;
// // using System.Collections.Generic;

// // public class IntersectionDetector : MonoBehaviour
// // {
// //     [SerializeField] private GameObject _greenArrowPrefab;
// //     [SerializeField] private GameObject _redCrossPrefab;
// //     [SerializeField] private float _indicatorHeight = 1.5f;
// //     [SerializeField] private GameObject _wrongPathTriggerPrefab;
// //     [SerializeField] private GameObject _intersectionZonePrefab;

// //     public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellWidth, float cellDepth)
// //     {
// //         for (int x = 0; x < width; x++)
// //         {
// //             for (int z = 0; z < depth; z++)
// //             {
// //                 MazeCell cell = grid[x, z];
// //                 List<Vector2Int> openExits = GetOpenExits(cell, width, depth);

// //                 if (openExits.Count >= 3)
// //                     SpawnIndicators(cell, openExits, distanceFromExit, cellWidth, cellDepth);
// //             }
// //         }
// //     }

// //     private List<Vector2Int> GetOpenExits(MazeCell cell, int width, int depth)
// //     {
// //         List<Vector2Int> exits = new List<Vector2Int>();
// //         int x = cell.GridX;
// //         int z = cell.GridZ;

// //         if (x + 1 < width  && !cell.HasRightWall()) exits.Add(Vector2Int.right);
// //         if (x - 1 >= 0     && !cell.HasLeftWall())  exits.Add(Vector2Int.left);
// //         if (z + 1 < depth  && !cell.HasFrontWall()) exits.Add(new Vector2Int(0, 1));
// //         if (z - 1 >= 0     && !cell.HasBackWall())  exits.Add(new Vector2Int(0, -1));

// //         return exits;
// //     }

// //     private void SpawnIndicators(MazeCell cell, List<Vector2Int> exits, int[,] distanceFromExit, float cellWidth, float cellDepth)
// //     {
// //         int x = cell.GridX;
// //         int z = cell.GridZ;

// //         Vector2Int bestExit = exits[0];
// //         int bestDist = int.MaxValue;
// //         foreach (var exit in exits)
// //         {
// //             int dist = distanceFromExit[x + exit.x, z + exit.y];
// //             if (dist < bestDist) { bestDist = dist; bestExit = exit; }
// //         }

// //         IntersectionZone zone = null;
// //         if (_intersectionZonePrefab != null)
// //         {
// //             GameObject zoneObj = Instantiate(_intersectionZonePrefab, cell.transform.position + Vector3.up * 0.5f, Quaternion.identity);
// //             // Sized slightly larger (1.2f) to perfectly bridge and eliminate gaps into corridors
// //             zoneObj.transform.localScale = new Vector3(cellWidth * 1.2f, 1.5f, cellDepth * 1.2f);
// //             zone = zoneObj.GetComponent<IntersectionZone>();
// //         }

// //         foreach (var exit in exits)
// //         {
// //             bool isCorrect = (exit == bestExit);

// //             float ExtentOffset = (exit.x != 0) ? cellWidth : cellDepth;

// //             Vector3 indicatorPos = cell.transform.position
// //                 + new Vector3(exit.x * cellWidth, 0, exit.y * cellDepth) * 0.4f
// //                 + Vector3.up * _indicatorHeight;

// //             Quaternion indicatorRot = Quaternion.LookRotation(new Vector3(exit.x, 0, exit.y));
// //             Instantiate(isCorrect ? _greenArrowPrefab : _redCrossPrefab, indicatorPos, indicatorRot);

// //             if (!isCorrect && _wrongPathTriggerPrefab != null && zone != null)
// //             {
// //                 Vector3 triggerPos = cell.transform.position
// //                     + new Vector3(exit.x * cellWidth, 0, exit.y * cellDepth) * 0.5f
// //                     + Vector3.up * 0.5f;

// //                 GameObject triggerObj = Instantiate(_wrongPathTriggerPrefab, triggerPos, indicatorRot);
                
// //                 // Adaptive trigger width scaling based on orientation matching cell geometry
// //                 float transverseWidth = (exit.x != 0) ? cellDepth : cellWidth;
// //                 triggerObj.transform.localScale = new Vector3(transverseWidth * 0.8f, 1.5f, 0.3f);

// //                 WrongPathTrigger wrongTrigger = triggerObj.GetComponent<WrongPathTrigger>();
// //                 if (wrongTrigger != null)
// //                     zone.RegisterWrongPathTrigger(wrongTrigger);
// //             }
// //         }
// //     }
// // }



// // using UnityEngine;
// // using System.Collections.Generic;

// // public class IntersectionDetector : MonoBehaviour
// // {
// //     [SerializeField] private GameObject _greenArrowPrefab;
// //     [SerializeField] private GameObject _redCrossPrefab;
// //     [SerializeField] private float _indicatorHeight = 1.5f;
// //     [SerializeField] private GameObject _wrongPathTriggerPrefab;
// //     [SerializeField] private GameObject _intersectionZonePrefab;

// //     private List<GameObject> _spawnedObjects = new List<GameObject>();

// //     public void ClearIndicators()
// //     {
// //         foreach (GameObject obj in _spawnedObjects)
// //             if (obj != null) Destroy(obj);
// //         _spawnedObjects.Clear();
// //     }

// //     public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellWidth, float cellDepth)
// //     {
// //         for (int x = 0; x < width; x++)
// //         {
// //             for (int z = 0; z < depth; z++)
// //             {
// //                 MazeCell cell = grid[x, z];
// //                 List<Vector2Int> openExits = GetOpenExits(cell, width, depth);

// //                 if (openExits.Count >= 3)
// //                     SpawnIndicators(cell, openExits, distanceFromExit, cellWidth, cellDepth);
// //             }
// //         }
// //     }

// //     // public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellSize)
// //     // {
// //     //     for (int x = 0; x < width; x++)
// //     //     {
// //     //         for (int z = 0; z < depth; z++)
// //     //         {
// //     //             MazeCell cell = grid[x, z];
// //     //             List<Vector2Int> openExits = GetOpenExits(cell, width, depth);

// //     //             if (openExits.Count >= 3)
// //     //                 SpawnIndicators(cell, openExits, distanceFromExit, cellSize);
// //     //         }
// //     //     }
// //     // }

// //     private List<Vector2Int> GetOpenExits(MazeCell cell, int width, int depth)
// //     {
// //         List<Vector2Int> exits = new List<Vector2Int>();
// //         int x = cell.GridX;
// //         int z = cell.GridZ;

// //         if (x + 1 < width  && !cell.HasRightWall()) exits.Add(Vector2Int.right);
// //         if (x - 1 >= 0     && !cell.HasLeftWall())  exits.Add(Vector2Int.left);
// //         if (z + 1 < depth  && !cell.HasFrontWall()) exits.Add(new Vector2Int(0, 1));
// //         if (z - 1 >= 0     && !cell.HasBackWall())  exits.Add(new Vector2Int(0, -1));

// //         return exits;
// //     }

// //     // private List<Vector2Int> GetOpenExits(MazeCell cell, int width, int depth)
// //     // {
// //     //     List<Vector2Int> exits = new List<Vector2Int>();
// //     //     int x = cell.GridX;
// //     //     int z = cell.GridZ;

// //     //     // BUG FIX: match the wall that gets cleared on the CURRENT cell when moving in each direction.
// //     //     // ClearWalls clears: moving +X → current loses LeftWall
// //     //     //                    moving -X → current loses RightWall
// //     //     //                    moving +Z → current loses BackWall
// //     //     //                    moving -Z → current loses FrontWall
// //     //     if (x + 1 < width  && !cell.HasLeftWall())  exits.Add(Vector2Int.right);
// //     //     if (x - 1 >= 0     && !cell.HasRightWall()) exits.Add(Vector2Int.left);
// //     //     if (z + 1 < depth  && !cell.HasBackWall())  exits.Add(new Vector2Int(0, 1));
// //     //     if (z - 1 >= 0     && !cell.HasFrontWall()) exits.Add(new Vector2Int(0, -1));

// //     //     return exits;
// //     // }

// //     private void SpawnIndicators(MazeCell cell, List<Vector2Int> exits, int[,] distanceFromExit, float cellSize)
// //     {
// //         int x = cell.GridX;
// //         int z = cell.GridZ;

// //         Vector2Int bestExit = exits[0];
// //         int bestDist = int.MaxValue;
// //         foreach (var exit in exits)
// //         {
// //             int dist = distanceFromExit[x + exit.x, z + exit.y];
// //             if (dist < bestDist) { bestDist = dist; bestExit = exit; }
// //         }

// //         IntersectionZone zone = null;
// //         if (_intersectionZonePrefab != null)
// //         {
// //             GameObject zoneObj = Instantiate(
// //                 _intersectionZonePrefab,
// //                 cell.transform.position + Vector3.up * 0.5f,
// //                 Quaternion.identity);

// //             zoneObj.transform.localScale = new Vector3(cellSize * 1.2f, 1.5f, cellSize * 1.2f);
// //             zone = zoneObj.GetComponent<IntersectionZone>();
// //             _spawnedObjects.Add(zoneObj);
// //         }

// //         foreach (var exit in exits)
// //         {
// //             bool isCorrect = (exit == bestExit);

// //             Vector3 indicatorPos = cell.transform.position
// //                 + new Vector3(exit.x, 0, exit.y) * (cellSize * 0.4f)
// //                 + Vector3.up * _indicatorHeight;

// //             Quaternion indicatorRot = Quaternion.LookRotation(new Vector3(exit.x, 0, exit.y));
// //             GameObject indicator = Instantiate(
// //                 isCorrect ? _greenArrowPrefab : _redCrossPrefab,
// //                 indicatorPos,
// //                 indicatorRot);
// //             _spawnedObjects.Add(indicator);

// //             if (!isCorrect && _wrongPathTriggerPrefab != null && zone != null)
// //             {
// //                 // BUG FIX: push trigger to 1.0 * cellSize so it sits past the zone boundary
// //                 // and the player can't disarm it before reaching it
// //                 Vector3 triggerPos = cell.transform.position
// //                     + new Vector3(exit.x, 0, exit.y) * (cellSize * 1.0f)
// //                     + Vector3.up * 0.5f;

// //                 GameObject triggerObj = Instantiate(_wrongPathTriggerPrefab, triggerPos, indicatorRot);
// //                 triggerObj.transform.localScale = new Vector3(cellSize * 0.8f, 1.5f, 0.3f);
// //                 _spawnedObjects.Add(triggerObj);

// //                 WrongPathTrigger wrongTrigger = triggerObj.GetComponent<WrongPathTrigger>();
// //                 if (wrongTrigger != null)
// //                     zone.RegisterWrongPathTrigger(wrongTrigger);
// //             }
// //         }
// //     }
// // }

// // using UnityEngine;
// // using System.Collections.Generic;

// // public class IntersectionDetector : MonoBehaviour
// // {
// //     [SerializeField] private GameObject _greenArrowPrefab;
// //     [SerializeField] private GameObject _redCrossPrefab;
// //     [SerializeField] private float _indicatorHeight = 1.5f;
// //     [SerializeField] private GameObject _wrongPathTriggerPrefab;

// //     // Assign a simple empty prefab with a BoxCollider (Is Trigger checked)
// //     [SerializeField] private GameObject _intersectionZonePrefab;

// //     private List<GameObject> _spawnedObjects = new List<GameObject>();

// //     public void ClearIndicators()
// //     {
// //         foreach (GameObject obj in _spawnedObjects)
// //             if (obj != null) Destroy(obj);

// //         _spawnedObjects.Clear();
// //     }

// //     public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellSize)
// //     {
// //         for (int x = 0; x < width; x++)
// //         {
// //             for (int z = 0; z < depth; z++)
// //             {
// //                 MazeCell cell = grid[x, z];
// //                 List<Vector2Int> openExits = GetOpenExits(cell, width, depth);

// //                 if (openExits.Count >= 3)
// //                     SpawnIndicators(cell, openExits, distanceFromExit, cellSize);
// //             }
// //         }
// //     }

// //     private List<Vector2Int> GetOpenExits(MazeCell cell, int width, int depth)
// //     {
// //         List<Vector2Int> exits = new List<Vector2Int>();
// //         int x = cell.GridX;
// //         int z = cell.GridZ;

// //         if (x + 1 < width  && !cell.HasRightWall()) exits.Add(Vector2Int.right);
// //         if (x - 1 >= 0     && !cell.HasLeftWall())  exits.Add(Vector2Int.left);
// //         if (z + 1 < depth  && !cell.HasFrontWall()) exits.Add(new Vector2Int(0, 1));
// //         if (z - 1 >= 0     && !cell.HasBackWall())  exits.Add(new Vector2Int(0, -1));

// //         return exits;
// //     }

// //     private void SpawnIndicators(MazeCell cell, List<Vector2Int> exits, int[,] distanceFromExit, float cellSize)
// //     {
// //         int x = cell.GridX;
// //         int z = cell.GridZ;

// //         // Find the correct exit
// //         Vector2Int bestExit = exits[0];
// //         int bestDist = int.MaxValue;
// //         foreach (var exit in exits)
// //         {
// //             int dist = distanceFromExit[x + exit.x, z + exit.y];
// //             if (dist < bestDist) { bestDist = dist; bestExit = exit; }
// //         }

// //         // Spawn the intersection zone at the cell center
// //         IntersectionZone zone = null;
// //         if (_intersectionZonePrefab != null)
// //         {
// //             GameObject zoneObj = Instantiate(
// //                 _intersectionZonePrefab,
// //                 cell.transform.position + Vector3.up * 0.5f,
// //                 Quaternion.identity);

// //             // Size the zone to fill the cell so the player reliably enters it
// //             zoneObj.transform.localScale = new Vector3(cellSize * 0.8f, 1.5f, cellSize * 0.8f);
// //             zone = zoneObj.GetComponent<IntersectionZone>();
// //             _spawnedObjects.Add(zoneObj);
// //         }

// //         // Spawn indicators and wrong path triggers
// //         foreach (var exit in exits)
// //         {
// //             bool isCorrect = (exit == bestExit);

// //             // Visual indicator
// //             Vector3 indicatorPos = cell.transform.position
// //                 + new Vector3(exit.x, 0, exit.y) * (cellSize * 0.4f)
// //                 + Vector3.up * _indicatorHeight;

// //             Quaternion indicatorRot = Quaternion.LookRotation(new Vector3(exit.x, 0, exit.y));
// //             GameObject indicator = Instantiate(isCorrect ? _greenArrowPrefab : _redCrossPrefab, indicatorPos, indicatorRot);
// //             _spawnedObjects.Add(indicator);

// //             // Wrong path trigger — only on wrong exits, only if we have both prefabs
// //             if (!isCorrect && _wrongPathTriggerPrefab != null && zone != null)
// //             {
// //                 Vector3 triggerPos = cell.transform.position
// //                     + new Vector3(exit.x, 0, exit.y) * (cellSize * 0.5f)
// //                     + Vector3.up * 0.5f;

// //                 GameObject triggerObj = Instantiate(_wrongPathTriggerPrefab, triggerPos, indicatorRot);
// //                 _spawnedObjects.Add(triggerObj);

// //                 // Size the trigger to span the corridor width so the player can't slip past
// //                 triggerObj.transform.localScale = new Vector3(cellSize * 0.8f, 1.5f, 0.3f);

// //                 WrongPathTrigger wrongTrigger = triggerObj.GetComponent<WrongPathTrigger>();
// //                 if (wrongTrigger != null)
// //                     zone.RegisterWrongPathTrigger(wrongTrigger);
// //             }
// //         }
// //     }
// // }






// // using UnityEngine;
// // using System.Collections.Generic;

// // public class IntersectionDetector : MonoBehaviour
// // {
// //     [SerializeField] private GameObject _greenArrowPrefab;
// //     [SerializeField] private GameObject _redCrossPrefab;
// //     [SerializeField] private float _indicatorHeight = 1.5f;
// //     [SerializeField] private GameObject _wrongPathTriggerPrefab;
// //     [SerializeField] private GameObject _intersectionZonePrefab;

// //     private List<GameObject> _spawnedObjects = new List<GameObject>();

// //     public void ClearIndicators()
// //     {
// //         foreach (GameObject obj in _spawnedObjects)
// //             if (obj != null) Destroy(obj);
// //         _spawnedObjects.Clear();
// //     }

// //     public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellWidth, float cellDepth)
// //     {
// //         for (int x = 0; x < width; x++)
// //         {
// //             for (int z = 0; z < depth; z++)
// //             {
// //                 MazeCell cell = grid[x, z];
// //                 List<Vector2Int> openExits = GetOpenExits(cell, width, depth);

// //                 if (openExits.Count >= 3)
// //                     SpawnIndicators(cell, openExits, distanceFromExit, cellWidth, cellDepth);
// //             }
// //         }
// //     }

// //     // public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellSize)
// //     // {
// //     //     for (int x = 0; x < width; x++)
// //     //     {
// //     //         for (int z = 0; z < depth; z++)
// //     //         {
// //     //             MazeCell cell = grid[x, z];
// //     //             List<Vector2Int> openExits = GetOpenExits(cell, width, depth);

// //     //             if (openExits.Count >= 3)
// //     //                 SpawnIndicators(cell, openExits, distanceFromExit, cellSize);
// //     //         }
// //     //     }
// //     // }

// //     private List<Vector2Int> GetOpenExits(MazeCell cell, int width, int depth)
// //     {
// //         List<Vector2Int> exits = new List<Vector2Int>();
// //         int x = cell.GridX;
// //         int z = cell.GridZ;

// //         if (x + 1 < width  && !cell.HasRightWall()) exits.Add(Vector2Int.right);
// //         if (x - 1 >= 0     && !cell.HasLeftWall())  exits.Add(Vector2Int.left);
// //         if (z + 1 < depth  && !cell.HasFrontWall()) exits.Add(new Vector2Int(0, 1));
// //         if (z - 1 >= 0     && !cell.HasBackWall())  exits.Add(new Vector2Int(0, -1));

// //         return exits;
// //     }

// //     // private List<Vector2Int> GetOpenExits(MazeCell cell, int width, int depth)
// //     // {
// //     //     List<Vector2Int> exits = new List<Vector2Int>();
// //     //     int x = cell.GridX;
// //     //     int z = cell.GridZ;

// //     //     // BUG FIX: match the wall that gets cleared on the CURRENT cell when moving in each direction.
// //     //     // ClearWalls clears: moving +X → current loses LeftWall
// //     //     //                    moving -X → current loses RightWall
// //     //     //                    moving +Z → current loses BackWall
// //     //     //                    moving -Z → current loses FrontWall
// //     //     if (x + 1 < width  && !cell.HasLeftWall())  exits.Add(Vector2Int.right);
// //     //     if (x - 1 >= 0     && !cell.HasRightWall()) exits.Add(Vector2Int.left);
// //     //     if (z + 1 < depth  && !cell.HasBackWall())  exits.Add(new Vector2Int(0, 1));
// //     //     if (z - 1 >= 0     && !cell.HasFrontWall()) exits.Add(new Vector2Int(0, -1));

// //     //     return exits;
// //     // }

// //     private void SpawnIndicators(MazeCell cell, List<Vector2Int> exits, int[,] distanceFromExit, float cellSize)
// //     {
// //         int x = cell.GridX;
// //         int z = cell.GridZ;

// //         Vector2Int bestExit = exits[0];
// //         int bestDist = int.MaxValue;
// //         foreach (var exit in exits)
// //         {
// //             int dist = distanceFromExit[x + exit.x, z + exit.y];
// //             if (dist < bestDist) { bestDist = dist; bestExit = exit; }
// //         }

// //         IntersectionZone zone = null;
// //         if (_intersectionZonePrefab != null)
// //         {
// //             GameObject zoneObj = Instantiate(
// //                 _intersectionZonePrefab,
// //                 cell.transform.position + Vector3.up * 0.5f,
// //                 Quaternion.identity);

// //             zoneObj.transform.localScale = new Vector3(cellSize * 1.2f, 1.5f, cellSize * 1.2f);
// //             zone = zoneObj.GetComponent<IntersectionZone>();
// //             _spawnedObjects.Add(zoneObj);
// //         }

// //         foreach (var exit in exits)
// //         {
// //             bool isCorrect = (exit == bestExit);

// //             Vector3 indicatorPos = cell.transform.position
// //                 + new Vector3(exit.x, 0, exit.y) * (cellSize * 0.4f)
// //                 + Vector3.up * _indicatorHeight;

// //             Quaternion indicatorRot = Quaternion.LookRotation(new Vector3(exit.x, 0, exit.y));
// //             GameObject indicator = Instantiate(
// //                 isCorrect ? _greenArrowPrefab : _redCrossPrefab,
// //                 indicatorPos,
// //                 indicatorRot);
// //             _spawnedObjects.Add(indicator);

// //             if (!isCorrect && _wrongPathTriggerPrefab != null && zone != null)
// //             {
// //                 // BUG FIX: push trigger to 1.0 * cellSize so it sits past the zone boundary
// //                 // and the player can't disarm it before reaching it
// //                 Vector3 triggerPos = cell.transform.position
// //                     + new Vector3(exit.x, 0, exit.y) * (cellSize * 1.0f)
// //                     + Vector3.up * 0.5f;

// //                 GameObject triggerObj = Instantiate(_wrongPathTriggerPrefab, triggerPos, indicatorRot);
// //                 triggerObj.transform.localScale = new Vector3(cellSize * 0.8f, 1.5f, 0.3f);
// //                 _spawnedObjects.Add(triggerObj);

// //                 WrongPathTrigger wrongTrigger = triggerObj.GetComponent<WrongPathTrigger>();
// //                 if (wrongTrigger != null)
// //                     zone.RegisterWrongPathTrigger(wrongTrigger);
// //             }
// //         }
// //     }
// // }

// // // using UnityEngine;
// // // using System.Collections.Generic;

// // // public class IntersectionDetector : MonoBehaviour
// // // {
// // //     [SerializeField] private GameObject _greenArrowPrefab;
// // //     [SerializeField] private GameObject _redCrossPrefab;
// // //     [SerializeField] private float _indicatorHeight = 1.5f;
// // //     [SerializeField] private GameObject _wrongPathTriggerPrefab;

// // //     // Assign a simple empty prefab with a BoxCollider (Is Trigger checked)
// // //     [SerializeField] private GameObject _intersectionZonePrefab;

// // //     private List<GameObject> _spawnedObjects = new List<GameObject>();

// // //     public void ClearIndicators()
// // //     {
// // //         foreach (GameObject obj in _spawnedObjects)
// // //             if (obj != null) Destroy(obj);

// // //         _spawnedObjects.Clear();
// // //     }

// // //     public void OnMazeReady(MazeCell[,] grid, int width, int depth, int[,] distanceFromExit, float cellSize)
// // //     {
// // //         for (int x = 0; x < width; x++)
// // //         {
// // //             for (int z = 0; z < depth; z++)
// // //             {
// // //                 MazeCell cell = grid[x, z];
// // //                 List<Vector2Int> openExits = GetOpenExits(cell, width, depth);

// // //                 if (openExits.Count >= 3)
// // //                     SpawnIndicators(cell, openExits, distanceFromExit, cellSize);
// // //             }
// // //         }
// // //     }

// // //     private List<Vector2Int> GetOpenExits(MazeCell cell, int width, int depth)
// // //     {
// // //         List<Vector2Int> exits = new List<Vector2Int>();
// // //         int x = cell.GridX;
// // //         int z = cell.GridZ;

// // //         if (x + 1 < width  && !cell.HasRightWall()) exits.Add(Vector2Int.right);
// // //         if (x - 1 >= 0     && !cell.HasLeftWall())  exits.Add(Vector2Int.left);
// // //         if (z + 1 < depth  && !cell.HasFrontWall()) exits.Add(new Vector2Int(0, 1));
// // //         if (z - 1 >= 0     && !cell.HasBackWall())  exits.Add(new Vector2Int(0, -1));

// // //         return exits;
// // //     }

// // //     private void SpawnIndicators(MazeCell cell, List<Vector2Int> exits, int[,] distanceFromExit, float cellSize)
// // //     {
// // //         int x = cell.GridX;
// // //         int z = cell.GridZ;

// // //         // Find the correct exit
// // //         Vector2Int bestExit = exits[0];
// // //         int bestDist = int.MaxValue;
// // //         foreach (var exit in exits)
// // //         {
// // //             int dist = distanceFromExit[x + exit.x, z + exit.y];
// // //             if (dist < bestDist) { bestDist = dist; bestExit = exit; }
// // //         }

// // //         // Spawn the intersection zone at the cell center
// // //         IntersectionZone zone = null;
// // //         if (_intersectionZonePrefab != null)
// // //         {
// // //             GameObject zoneObj = Instantiate(
// // //                 _intersectionZonePrefab,
// // //                 cell.transform.position + Vector3.up * 0.5f,
// // //                 Quaternion.identity);

// // //             // Size the zone to fill the cell so the player reliably enters it
// // //             zoneObj.transform.localScale = new Vector3(cellSize * 0.8f, 1.5f, cellSize * 0.8f);
// // //             zone = zoneObj.GetComponent<IntersectionZone>();
// // //             _spawnedObjects.Add(zoneObj);
// // //         }

// // //         // Spawn indicators and wrong path triggers
// // //         foreach (var exit in exits)
// // //         {
// // //             bool isCorrect = (exit == bestExit);

// // //             // Visual indicator
// // //             Vector3 indicatorPos = cell.transform.position
// // //                 + new Vector3(exit.x, 0, exit.y) * (cellSize * 0.4f)
// // //                 + Vector3.up * _indicatorHeight;

// // //             Quaternion indicatorRot = Quaternion.LookRotation(new Vector3(exit.x, 0, exit.y));
// // //             GameObject indicator = Instantiate(isCorrect ? _greenArrowPrefab : _redCrossPrefab, indicatorPos, indicatorRot);
// // //             _spawnedObjects.Add(indicator);

// // //             // Wrong path trigger — only on wrong exits, only if we have both prefabs
// // //             if (!isCorrect && _wrongPathTriggerPrefab != null && zone != null)
// // //             {
// // //                 Vector3 triggerPos = cell.transform.position
// // //                     + new Vector3(exit.x, 0, exit.y) * (cellSize * 0.5f)
// // //                     + Vector3.up * 0.5f;

// // //                 GameObject triggerObj = Instantiate(_wrongPathTriggerPrefab, triggerPos, indicatorRot);
// // //                 _spawnedObjects.Add(triggerObj);

// // //                 // Size the trigger to span the corridor width so the player can't slip past
// // //                 triggerObj.transform.localScale = new Vector3(cellSize * 0.8f, 1.5f, 0.3f);

// // //                 WrongPathTrigger wrongTrigger = triggerObj.GetComponent<WrongPathTrigger>();
// // //                 if (wrongTrigger != null)
// // //                     zone.RegisterWrongPathTrigger(wrongTrigger);
// // //             }
// // //         }
// // //     }
// // // }


