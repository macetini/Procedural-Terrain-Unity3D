using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Terrain.Chunk;
using Assets.Scripts.Terrain.Chunk.Data;
using Assets.Scripts.Terrain.Processing;
using Assets.Scripts.Terrain.Settings;
using Assets.Scripts.Terrain.Utils;
using UnityEngine;

namespace Assets.Scripts.Terrain
{
    public class TerrainController
    {
        public ChunkNeighborStruct GetNeighborGrids(Vector2Int coord) =>
            terrainDataProcessor.GetNeighborGrids(coord);

        private readonly RuntimeState runtime = new();
        private readonly CleanupState cleanup = new();
        private readonly BuildQueueState buildState = new();
        private readonly TerrainGenerator generator;
        private readonly TerrainDataProcessor terrainDataProcessor;
        private readonly Dictionary<int, int[]> triangleCache = new();

        public void Destroy()
        {
            runtime.WorldMonitoringActive = false;
        }

        public TerrainController(TerrainGenerator generator)
        {
            this.generator = generator;
            terrainDataProcessor = new TerrainDataProcessor(generator.terrain.chunkSize);
        }

        public void ResetGeneratorState()
        {
            generator.StopAllCoroutines();
            buildState.Clear();
            triangleCache.Clear();
            cleanup.ResetForRebuild();
            terrainDataProcessor.ClearAll();
        }

        public void BuildTerrain()
        {
            NoiseSettings noise = generator.noise;
            TerrainNoise.Init(
                noise.seed,
                noise.scale,
                noise.octaves,
                noise.persistence,
                noise.lacunarity,
                generator.terrain.maxElevationStepsCount
            );
            runtime.ChunkBoundSize = generator.terrain.chunkSize * generator.terrain.tileSize;

            UpdateCurrentCameraPosition();

            FirstPass();
            SecondPass();

            generator.StartCoroutine(WorldMonitoringRoutine()); // The manager
            generator.StartCoroutine(VisibilityCheckRoutine()); // The culler
        }

        public void UpdateCurrentCameraPosition()
        {
            int currentX = Mathf.FloorToInt(
                generator.cameraConfig.reference.transform.position.x / runtime.ChunkBoundSize
            );
            int currentZ = Mathf.FloorToInt(
                generator.cameraConfig.reference.transform.position.z / runtime.ChunkBoundSize
            );
            runtime.CurrentCameraPosition = new Vector2Int(currentX, currentZ);
        }

        // --------------- FIRST PASS START : GENERATE RAW DATA FOR ALL CHUNKS IN VIEW DISTANCE ---------------
        private void FirstPass()
        {
            int dataRadius = generator.cameraConfig.viewDistanceChunks + 1;

            double totalMs = MeasureExecution(() =>
            {
                LogMeasuredStep(
                    "GenerateFullMeshData()",
                    () => GenerateFullMeshData(runtime.CurrentCameraPosition, dataRadius)
                );
                LogMeasuredStep(
                    "SanitizeCurrentTileMeshData()",
                    () =>
                        terrainDataProcessor.SanitizeData(runtime.CurrentCameraPosition, dataRadius)
                );
            });

            LogExecutionTime("Total", totalMs);
        }

        // ---------------------- MEASUREMENT UTILITIES -------------------------------------------------
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

        // -----------------------------------------------------------------------
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
                    terrainDataProcessor.GenerateRawData(coord);
                }
            }
        }

        // --------------- FIRST PASS END ------------------------------------------------------------------------------------

        // ---------------- SECOND PASS START : BUILD MESHES FOR VISIBLE CHUNKS AND ENQUEUE THEM FOR RENDERING ---------------
        private void SecondPass()
        {
            EnqueueVisibleChunksAroundCamera();
            StartBuildQueueIfNeeded();
        }

        private void EnqueueVisibleChunksAroundCamera()
        {
            for (
                int x = -generator.cameraConfig.viewDistanceChunks;
                x <= generator.cameraConfig.viewDistanceChunks;
                x++
            )
            {
                EnqueueVisibleChunksAtColumnOffset(x);
            }
        }

        private void EnqueueVisibleChunksAtColumnOffset(int xOffset)
        {
            for (
                int z = -generator.cameraConfig.viewDistanceChunks;
                z <= generator.cameraConfig.viewDistanceChunks;
                z++
            )
            {
                Vector2Int coord = new(
                    runtime.CurrentCameraPosition.x + xOffset,
                    runtime.CurrentCameraPosition.y + z
                );

                if (
                    !TryEnqueueChunkBuild(coord)
                    && terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk chunk)
                )
                {
                    chunk.UpdateLOD();
                }
            }
        }

        private bool TryEnqueueChunkBuild(Vector2Int coord)
        {
            if (terrainDataProcessor.HasActiveChunk(coord) || !buildState.QueueHash.Add(coord))
            {
                return false;
            }

            buildState.Queue.Add(coord);
            return true;
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
                generator.StartCoroutine(ProcessBuildQueue());
            }
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

            if (terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk chunk))
            {
                chunk.StartFadeIn();
            }
        }

        private bool ShouldBuildQueuedChunk(Vector2Int coord)
        {
            return !terrainDataProcessor.HasActiveChunk(coord) && IsWithinRetentionBounds(coord);
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
            return Mathf.Max(0, generator.cameraConfig.viewDistanceChunks);
        }

        private IEnumerator GenerateRawDataForChunk(Vector2Int coord)
        {
            yield return IterateNeighbors3x3(
                coord,
                n =>
                {
                    if (!terrainDataProcessor.HasTileData(n))
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
                    if (!terrainDataProcessor.IsSanitized(n))
                    {
                        terrainDataProcessor.SanitizeGlobalChunk(n);
                        terrainDataProcessor.MarkSanitized(n);
                        return true;
                    }
                    return false;
                }
            );
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

        private void SpawnChunkMesh(Vector2Int coord)
        {
            if (terrainDataProcessor.HasActiveChunk(coord))
            {
                return;
            }

            Vector3 position = new(
                coord.x * runtime.ChunkBoundSize,
                0,
                coord.y * runtime.ChunkBoundSize
            );
            TerrainChunk chunk = Object.Instantiate(
                generator.chunkPrefab,
                position,
                Quaternion.identity,
                generator.transform
            );

            chunk.InitBuild(generator, coord);
            chunk.UpdateVisibility(runtime.CameraPlanes);
            terrainDataProcessor.RegisterChunk(coord, chunk);
        }

        // --------------- SECOND PASS END ------------------------------------------------------------------------------------

        // --------------- MONITOR WORLD AND CLEAN UP REMOTE CHUNKS ------------------------------------------------------------

        private IEnumerator WorldMonitoringRoutine()
        {
            Vector2Int lastProcessedPos = new(-9999, -9999);
            while (runtime.WorldMonitoringActive && this != null && generator.isActiveAndEnabled)
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
            if (generator.debug.orphanSweepPeriod < 0)
            {
                return false;
            }
            if (generator.debug.orphanSweepPeriod == 0)
            {
                return true;
            }
            // Orphan sweep period logic removed with CleanupPassCounter
            return generator.debug.orphanSweepPeriod == 0;
        }

        private void CleanupSceneChunkOrphans()
        {
            cleanup.BeginSceneSweep();
            generator.GetComponentsInChildren(true, cleanup.SceneChunksSnapshot);
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
            terrainDataProcessor.GetActiveKeysNonAlloc(cleanup.VisibilityKeysSnapshot);
            foreach (var coord in cleanup.VisibilityKeysSnapshot)
            {
                if (IsWithinRetentionBounds(coord))
                {
                    continue;
                }
                if (terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk chunk))
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
            bool isForeignChunk = sceneChunk.Generator != null && sceneChunk.Generator != generator;
            return isDuplicateCoord || isOutOfBounds || isForeignChunk;
        }

        private void LogCleanupSummary()
        {
            // Logging removed with statistics fields
            if (!generator.debug.cleanupLogs)
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
                terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk activeChunk)
                && activeChunk == chunk
            )
            {
                terrainDataProcessor.Clear(coord);
            }
            if (chunk != null)
            {
                Object.Destroy(chunk.gameObject);
            }
        }

        private IEnumerator ProcessLocalChunks()
        {
            for (
                int x = -generator.cameraConfig.viewDistanceChunks;
                x <= generator.cameraConfig.viewDistanceChunks;
                x++
            )
            {
                EnqueueVisibleChunksAtColumnOffset(x);

                // Yield after every 'X' column to keep framerate perfect
                yield return null; // Row-by-row time slicing
            }

            StartBuildQueueIfNeeded();
        }

        // --------------- VISIBILITY CHECK ROUTINE : CULL CHUNKS BASED ON CAMERA FRUSTUM EACH FRAME ---------------

        private IEnumerator VisibilityCheckRoutine()
        {
            while (runtime.WorldMonitoringActive && this != null && generator.isActiveAndEnabled)
            {
                runtime.CameraPlanes = GeometryUtility.CalculateFrustumPlanes(
                    generator.cameraConfig.reference
                );

                // Take a snapshot of the current keys
                cleanup.VisibilityKeysSnapshot.Clear();
                cleanup.VisibilityKeysSnapshot.AddRange(terrainDataProcessor.ActiveChunkKeys);

                // Iterate through the snapshot
                for (int i = 0; i < cleanup.VisibilityKeysSnapshot.Count; i++)
                {
                    Vector2Int key = cleanup.VisibilityKeysSnapshot[i];

                    // Safety Check: Make sure the chunk wasn't purged while we were yielding
                    if (terrainDataProcessor.TryGetActiveChunk(key, out TerrainChunk chunk))
                    {
                        chunk.UpdateVisibility(runtime.CameraPlanes);

                        if (chunk.IsVisible && chunk.CurrentStep < 0)
                        {
                            chunk.UpdateLOD(true);
                        }
                    }

                    // Time Slicing: Only process after X frames
                    if (i > 0 && i % generator.lod.visibilityBatchSize == 0)
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
