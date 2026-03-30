using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Terrain.Config;
using Assets.Scripts.Terrain.Processing;
using Assets.Scripts.Terrain.Runtime;
using Assets.Scripts.Terrain.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Terrain
{
    public class TerrainGenerator : MonoBehaviour
    {
        [Header("Terrain Settings")]
        public TerrainSettings terrain = new();

        [Header("Noise Settings")]
        public NoiseSettings noise = new();

        [Header("Camera Settings")]
        public CameraSettings cameraConfig = new();

        [Header("LOD Settings")]
        public LODSettings lod = new();

        [Header("Debug")]
        public DebugSettings debug = new();

        [Header("Prefabs")]
        public TerrainChunk chunkPrefab;

        private readonly RuntimeState runtime = new();
        private readonly CleanupState cleanup = new();
        private readonly BuildQueueState buildState = new();

        // Geometry Cache
        private readonly Dictionary<int, int[]> triangleCache = new();

        // Data Processing
        public TerrainDataProcessor TerrainDataProcessor => _terrainDataProcessor;
        private TerrainDataProcessor _terrainDataProcessor;

        void Awake()
        {
            _terrainDataProcessor = new TerrainDataProcessor(terrain.chunkSize);
        }

        void OnDestroy()
        {
            runtime.WorldMonitoringActive = false;
        }

        void Start()
        {
            if (cameraConfig.reference == null)
            {
                Debug.LogError(
                    "[TerrainGenerator] Camera reference is not assigned. Assign it under Camera Settings > Reference in the Inspector.",
                    this
                );
                return;
            }
            BuildTerrain();
        }

        void Update()
        {
            UpdateCurrentCameraPosition();

            // WARNING: This will rebuild the whole terrain. Should only be used during development.
            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                HandleDebugRebuild();
            }
        }

        private void HandleDebugRebuild()
        {
            Debug.Log("Rebuilding terrain.");

            StopAllCoroutines();
            ResetGeneratorState();
            _terrainDataProcessor.ClearAll();
            BuildTerrain();
        }

        private void ResetGeneratorState()
        {
            buildState.Clear();
            triangleCache.Clear();
            cleanup.ResetForRebuild();
        }

        private void BuildTerrain()
        {
            TerrainNoise.Init(
                noise.seed,
                noise.scale,
                noise.octaves,
                noise.persistence,
                noise.lacunarity,
                terrain.maxElevationStepsCount
            );
            runtime.ChunkBoundSize = terrain.chunkSize * terrain.tileSize;
            UpdateCurrentCameraPosition();
            FirstPass();
            SecondPass();

            StartCoroutine(WorldMonitoringRoutine()); // The manager
            StartCoroutine(VisibilityCheckRoutine()); // The culler
        }

        private void UpdateCurrentCameraPosition()
        {
            int currentX = Mathf.FloorToInt(
                cameraConfig.reference.transform.position.x / runtime.ChunkBoundSize
            );
            int currentZ = Mathf.FloorToInt(
                cameraConfig.reference.transform.position.z / runtime.ChunkBoundSize
            );
            runtime.CurrentCameraPosition = new Vector2Int(currentX, currentZ);
        }

        private IEnumerator WorldMonitoringRoutine()
        {
            Vector2Int lastProcessedPos = new(-9999, -9999);
            while (runtime.WorldMonitoringActive && this != null && isActiveAndEnabled)
            {
                if (runtime.CurrentCameraPosition != lastProcessedPos)
                {
                    lastProcessedPos = runtime.CurrentCameraPosition;

                    CleanupRemoteChunks();

                    yield return ProcessLocalChunks();
                }
                yield return null;
            }
        }

        private IEnumerator ProcessLocalChunks()
        {
            for (
                int x = -cameraConfig.viewDistanceChunks;
                x <= cameraConfig.viewDistanceChunks;
                x++
            )
            {
                EnqueueVisibleChunksAtColumnOffset(x);

                // Yield after every 'X' column to keep framerate perfect
                yield return null; // Row-by-row time slicing
            }

            StartBuildQueueIfNeeded();
        }

        private void CleanupRemoteChunks()
        {
            if (ShouldRunOrphanSweep())
            {
                CleanupSceneChunkOrphans();
            }
            RemoveOutOfBoundsRegisteredChunks();
            LogCleanupSummary();
        }

        private bool ShouldRunOrphanSweep()
        {
            // debug.orphanSweepPeriod: 0 = always, -1 = never, N>0 = every N cycles
            if (debug.orphanSweepPeriod < 0)
            {
                return false;
            }
            if (debug.orphanSweepPeriod == 0)
            {
                return true;
            }
            // Orphan sweep period logic removed with CleanupPassCounter
            return debug.orphanSweepPeriod == 0;
        }

        private void CleanupSceneChunkOrphans()
        {
            cleanup.BeginSceneSweep();
            GetComponentsInChildren(true, cleanup.SceneChunksSnapshot);
            for (int i = 0; i < cleanup.SceneChunksSnapshot.Count; i++)
            {
                TerrainChunk sceneChunk = cleanup.SceneChunksSnapshot[i];
                if (sceneChunk == null)
                {
                    continue;
                }
                if (ShouldRemoveSceneChunk(sceneChunk, out Vector2Int coord))
                {
                    RemoveChunk(coord, sceneChunk);
                }
            }
        }

        private void RemoveOutOfBoundsRegisteredChunks()
        {
            cleanup.BeginCleanupPass();
            _terrainDataProcessor.GetActiveKeysNonAlloc(cleanup.VisibilityKeysSnapshot);
            foreach (var coord in cleanup.VisibilityKeysSnapshot)
            {
                if (IsWithinRetentionBounds(coord))
                {
                    continue;
                }
                if (_terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk chunk))
                {
                    RemoveChunk(coord, chunk);
                }
            }
        }

        private bool ShouldRemoveSceneChunk(TerrainChunk sceneChunk, out Vector2Int coord)
        {
            coord = sceneChunk.ChunkCoord;

            bool isDuplicateCoord = !cleanup.SeenCoords.Add(coord);
            bool isOutOfBounds = !IsWithinRetentionBounds(coord);
            bool isForeignChunk = sceneChunk.Generator != null && sceneChunk.Generator != this;
            return isDuplicateCoord || isOutOfBounds || isForeignChunk;
        }

        private void LogCleanupSummary()
        {
            // Logging removed with statistics fields
            if (!debug.cleanupLogs)
            {
                return;
            }
            Debug.Log(
                $"[CleanupRemoteChunks] CameraChunk={runtime.CurrentCameraPosition}, RetentionRadius={GetRetentionRadius()}"
            );
        }

        private void RemoveChunk(Vector2Int coord, TerrainChunk chunk)
        {
            if (
                _terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk activeChunk)
                && activeChunk == chunk
            )
            {
                _terrainDataProcessor.Clear(coord);
            }
            if (chunk != null)
            {
                Destroy(chunk.gameObject);
            }
        }

        private static Vector3 ChunkToWorldCenter(Vector2Int chunkCoord, float boundSize, float y)
        {
            return new Vector3(
                (chunkCoord.x + 0.5f) * boundSize,
                y,
                (chunkCoord.y + 0.5f) * boundSize
            );
        }

        private static void DrawChunkBoundsGizmo(
            Vector2Int centerChunk,
            int radius,
            float boundSize,
            float y,
            Color color
        )
        {
            float chunkCount = (radius * 2) + 1;
            Vector3 worldCenter = ChunkToWorldCenter(centerChunk, boundSize, y);
            Vector3 worldSize = new(chunkCount * boundSize, 0.05f, chunkCount * boundSize);

            Gizmos.color = color;
            Gizmos.DrawWireCube(worldCenter, worldSize);
        }

        private void FirstPass()
        {
            int dataRadius = cameraConfig.viewDistanceChunks + 1;

            double totalMs = MeasureExecution(() =>
            {
                LogMeasuredStep(
                    "GenerateFullMeshData()",
                    () => GenerateFullMeshData(runtime.CurrentCameraPosition, dataRadius)
                );
                LogMeasuredStep(
                    "SanitizeCurrentTileMeshData()",
                    () =>
                        _terrainDataProcessor.SanitizeData(
                            runtime.CurrentCameraPosition,
                            dataRadius
                        )
                );
            });

            LogExecutionTime("Total", totalMs);
        }

        private static double MeasureExecution(System.Action action)
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static void LogMeasuredStep(string label, System.Action action)
        {
            double elapsedMs = MeasureExecution(action);
            LogExecutionTime(label, elapsedMs);
        }

        private static void LogExecutionTime(string label, double elapsedMs)
        {
            if (elapsedMs <= 1.0f)
            {
                return;
            }

            Debug.Log($"<color=orange>'{label}' Execution Time: {elapsedMs:F2} ms</color>");
        }

        private void GenerateFullMeshData(Vector2Int cameraOrigin, int dataRadius)
        {
            // If radius is 0, this only runs once for the cameraOrigin.
            // If radius is 1, it runs 9 times.
            for (int xChunkOffset = -dataRadius; xChunkOffset <= dataRadius; xChunkOffset++)
            {
                for (int zChunkOffset = -dataRadius; zChunkOffset <= dataRadius; zChunkOffset++)
                {
                    Vector2Int coord = new(
                        cameraOrigin.x + xChunkOffset,
                        cameraOrigin.y + zChunkOffset
                    );
                    _terrainDataProcessor.GenerateRawData(coord);
                }
            }
        }

        private void SecondPass()
        {
            EnqueueVisibleChunksAroundCamera();
            StartBuildQueueIfNeeded();
        }

        private IEnumerator ProcessBuildQueue()
        {
            buildState.IsProcessing = true;
            while (buildState.Queue.Count > 0)
            {
                Vector2Int coord = buildState.Queue[0];
                buildState.Queue.RemoveAt(0);
                buildState.QueueHash.Remove(coord);

                yield return ProcessQueuedChunk(coord);
                yield return null;
            }
            buildState.IsProcessing = false;
        }

        private void EnqueueVisibleChunksAroundCamera()
        {
            for (
                int x = -cameraConfig.viewDistanceChunks;
                x <= cameraConfig.viewDistanceChunks;
                x++
            )
            {
                EnqueueVisibleChunksAtColumnOffset(x);
            }
        }

        private void EnqueueVisibleChunksAtColumnOffset(int xOffset)
        {
            for (
                int z = -cameraConfig.viewDistanceChunks;
                z <= cameraConfig.viewDistanceChunks;
                z++
            )
            {
                Vector2Int coord = new(
                    runtime.CurrentCameraPosition.x + xOffset,
                    runtime.CurrentCameraPosition.y + z
                );

                if (
                    !TryEnqueueChunkBuild(coord)
                    && _terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk chunk)
                )
                {
                    chunk.UpdateLOD();
                }
            }
        }

        private void StartBuildQueueIfNeeded()
        {
            if (buildState.Queue.Count <= 0)
            {
                return;
            }

            SortBuildQueue();
            if (!buildState.IsProcessing)
            {
                StartCoroutine(ProcessBuildQueue());
            }
        }

        private IEnumerator ProcessQueuedChunk(Vector2Int coord)
        {
            if (!ShouldBuildQueuedChunk(coord))
            {
                yield break;
            }

            yield return GenerateRawDataForChunk(coord);
            yield return EnsureSanitized(coord);

            if (!ShouldBuildQueuedChunk(coord))
            {
                yield break;
            }

            SpawnChunkMesh(coord);

            if (_terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk chunk))
            {
                chunk.StartFadeIn();
            }
        }

        private bool ShouldBuildQueuedChunk(Vector2Int coord)
        {
            return !_terrainDataProcessor.HasActiveChunk(coord) && IsWithinRetentionBounds(coord);
        }

        private IEnumerator IterateNeighbors3x3(
            Vector2Int center,
            System.Func<Vector2Int, bool> action
        )
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Vector2Int neighbor = center + new Vector2Int(x, z);
                    if (action(neighbor))
                    {
                        yield return null;
                    }
                }
            }
            yield return null;
        }

        private IEnumerator GenerateRawDataForChunk(Vector2Int coord)
        {
            yield return IterateNeighbors3x3(
                coord,
                n =>
                {
                    if (!_terrainDataProcessor.HasTileData(n))
                    {
                        GenerateFullMeshData(n, 0);
                        return true;
                    }
                    return false;
                }
            );
        }

        private IEnumerator EnsureSanitized(Vector2Int coord)
        {
            yield return IterateNeighbors3x3(
                coord,
                n =>
                {
                    if (!_terrainDataProcessor.IsSanitized(n))
                    {
                        _terrainDataProcessor.SanitizeGlobalChunk(n);
                        _terrainDataProcessor.MarkSanitized(n);
                        return true;
                    }
                    return false;
                }
            );
        }

        private void SpawnChunkMesh(Vector2Int coord)
        {
            if (_terrainDataProcessor.HasActiveChunk(coord))
            {
                return;
            }

            Vector3 position = new(
                coord.x * runtime.ChunkBoundSize,
                0,
                coord.y * runtime.ChunkBoundSize
            );
            TerrainChunk chunk = Instantiate(chunkPrefab, position, Quaternion.identity, transform);

            chunk.InitBuild(this, coord);
            chunk.UpdateVisibility(runtime.CameraPlanes);
            _terrainDataProcessor.RegisterChunk(coord, chunk);
        }

        private bool TryEnqueueChunkBuild(Vector2Int coord)
        {
            if (_terrainDataProcessor.HasActiveChunk(coord) || !buildState.QueueHash.Add(coord))
            {
                return false;
            }

            buildState.Queue.Add(coord);
            return true;
        }

        private bool IsWithinRetentionBounds(Vector2Int coord)
        {
            int retentionRadius = GetRetentionRadius();
            int dx = Mathf.Abs(coord.x - runtime.CurrentCameraPosition.x);
            int dz = Mathf.Abs(coord.y - runtime.CurrentCameraPosition.y);
            return dx <= retentionRadius && dz <= retentionRadius;
        }

        private int GetRetentionRadius()
        {
            return Mathf.Max(0, cameraConfig.viewDistanceChunks);
        }

        private void SortBuildQueue()
        {
            if (buildState.Queue.Count <= 1)
            {
                return;
            }

            // Capture camera pos in chunk-coordinates once to avoid repeated math
            // We use a local variable to avoid thread/sync issues during the sort
            Vector2Int camCoord = runtime.CurrentCameraPosition;

            buildState.Queue.Sort(
                (a, b) =>
                {
                    // Use "Manhattan Distance" or squared coordinate distance
                    // Manhattan: abs(x1-x2) + abs(y1-y2) is even faster than squaring
                    int distA = Mathf.Abs(a.x - camCoord.x) + Mathf.Abs(a.y - camCoord.y);
                    int distB = Mathf.Abs(b.x - camCoord.x) + Mathf.Abs(b.y - camCoord.y);

                    return distA.CompareTo(distB);
                }
            );
        }

        private IEnumerator VisibilityCheckRoutine()
        {
            while (runtime.WorldMonitoringActive && this != null && isActiveAndEnabled)
            {
                runtime.CameraPlanes = GeometryUtility.CalculateFrustumPlanes(
                    cameraConfig.reference
                );

                // Take a snapshot of the current keys
                cleanup.VisibilityKeysSnapshot.Clear();
                cleanup.VisibilityKeysSnapshot.AddRange(_terrainDataProcessor.ActiveChunkKeys);

                // Iterate through the snapshot
                for (int i = 0; i < cleanup.VisibilityKeysSnapshot.Count; i++)
                {
                    Vector2Int key = cleanup.VisibilityKeysSnapshot[i];

                    // Safety Check: Make sure the chunk wasn't purged while we were yielding
                    if (_terrainDataProcessor.TryGetActiveChunk(key, out TerrainChunk chunk))
                    {
                        chunk.UpdateVisibility(runtime.CameraPlanes);

                        if (chunk.IsVisible && chunk.CurrentStep < 0)
                        {
                            chunk.UpdateLOD(true);
                        }
                    }

                    // Time Slicing: Only process after X frames
                    if (i > 0 && i % lod.visibilityBatchSize == 0)
                        yield return null;
                }

                // Short rest before the next full world sweep
                yield return null;
            }
        }

        public int[] GetPrecalculatedTriangles(int resolution)
        {
            if (triangleCache.TryGetValue(resolution, out int[] cachedTris))
            {
                return cachedTris;
            }

            // If not in cache, calculate it once
            int[] newTris = TerrainMath.GenerateTriangleIndices(resolution);
            triangleCache.Add(resolution, newTris);
            return newTris;
        }
    }
}
