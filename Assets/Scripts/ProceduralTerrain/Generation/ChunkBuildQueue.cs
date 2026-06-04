using System;
using System.Collections;
using ProceduralTerrain.Generation.Data;
using ProceduralTerrain.Processing;
using UnityEngine;

namespace ProceduralTerrain.Generation
{
    /// <summary>
    /// Maintains the chunk build queue and schedules coroutine-based processing.
    /// </summary>
    internal class ChunkBuildQueue
    {
        private readonly BuildQueueState buildState = new();
        private readonly RuntimeState runtime;
        private readonly ITerrainDataProcessor terrainDataProcessor;
        private readonly IChunkPool chunkPool;
        private readonly ITerrainHost host;
        private readonly ChunkBuildWorker buildWorker;

        public ChunkBuildQueue(
            RuntimeState runtime,
            ITerrainDataProcessor terrainDataProcessor,
            IChunkPool chunkPool,
            ITerrainHost host
        )
        {
            this.runtime = runtime;
            this.terrainDataProcessor = terrainDataProcessor;
            this.chunkPool = chunkPool;
            this.host = host;
            buildWorker = new ChunkBuildWorker(runtime, terrainDataProcessor, chunkPool, host);
        }

        public void Clear()
        {
            buildState.Clear();
        }

        // Public queue API

        public void EnqueueVisibleChunksAroundCamera()
        {
            int viewDist = host.cameraConfig.viewDistanceChunks;
            for (int x = -viewDist; x <= viewDist; x++)
            {
                EnqueueVisibleChunksAtColumnOffset(x);
            }
        }

        public void EnqueueVisibleChunksAtColumnOffset(int xOffset)
        {
            int viewDist = host.cameraConfig.viewDistanceChunks;
            int baseX = runtime.CurrentCameraPosition.x + xOffset;
            int baseY = runtime.CurrentCameraPosition.y;
            for (int z = -viewDist; z <= viewDist; z++)
            {
                var coord = new Vector2Int(baseX, baseY + z);
                if (
                    !TryEnqueueChunkBuild(coord)
                    && terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk chunk)
                )
                {
                    chunk.UpdateLOD();
                }
            }
        }

        public void StartBuildQueueIfNeeded()
        {
            if (buildState.Queue.Count == 0)
            {
                return;
            }

            SortBuildQueue();

            if (!buildState.IsProcessing)
            {
                host.StartCoroutine(buildWorker.ProcessBuildQueue(buildState));
            }
        }

        public IEnumerator ProcessLocalChunks()
        {
            int viewDist = host.cameraConfig.viewDistanceChunks;
            int batchSize = Mathf.Max(1, host.lod.visibilityBatchSize);
            int processed = 0;

            for (int x = -viewDist; x <= viewDist; x++)
            {
                EnqueueVisibleChunksAtColumnOffset(x);
                processed++;

                if (processed % batchSize == 0)
                {
                    yield return null;
                }
            }

            StartBuildQueueIfNeeded();
        }

        // Queue scheduling internals

        private bool TryEnqueueChunkBuild(Vector2Int coord)
        {
            if (terrainDataProcessor.HasActiveChunk(coord) || !buildState.QueueHash.Add(coord))
            {
                return false;
            }

            buildState.Queue.Enqueue(coord);
            return true;
        }

        private void SortBuildQueue()
        {
            if (buildState.Queue.Count <= 1)
            {
                return;
            }

            buildState.SortOrigin = runtime.CurrentCameraPosition;
            buildState.SortBuffer.Clear();

            foreach (var item in buildState.Queue)
            {
                buildState.SortBuffer.Add(item);
            }

            buildState.SortBuffer.Sort(buildState.DistanceComparison);
            buildState.Queue.Clear();
            foreach (var coord in buildState.SortBuffer)
            {
                buildState.Queue.Enqueue(coord);
            }
        }

        /// <summary>
        /// Encapsulates build coroutine steps for queued chunk coordinates.
        /// </summary>
        private sealed class ChunkBuildWorker
        {
            private readonly RuntimeState runtime;
            private readonly ITerrainDataProcessor terrainDataProcessor;
            private readonly IChunkPool chunkPool;
            private readonly ITerrainHost host;

            public ChunkBuildWorker(
                RuntimeState runtime,
                ITerrainDataProcessor terrainDataProcessor,
                IChunkPool chunkPool,
                ITerrainHost host
            )
            {
                this.runtime = runtime;
                this.terrainDataProcessor = terrainDataProcessor;
                this.chunkPool = chunkPool;
                this.host = host;
            }

            public IEnumerator ProcessBuildQueue(BuildQueueState buildState)
            {
                buildState.IsProcessing = true;

                while (buildState.Queue.Count > 0 && !runtime.IsBuildCancelled)
                {
                    Vector2Int coord = buildState.Queue.Dequeue();
                    yield return SafeBuildChunk(coord);

                    buildState.QueueHash.Remove(coord);
                }

                buildState.IsProcessing = false;
            }

            // Build pipeline internals

            private IEnumerator SafeBuildChunk(Vector2Int coord)
            {
                IEnumerator buildTask = ProcessQueuedChunk(coord);
                bool canProcess = true;

                while (canProcess)
                {
                    bool hasNextStep;

                    try
                    {
                        hasNextStep = buildTask.MoveNext();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(
                            $"<color=red>[Terrain] Crash building chunk {coord}:</color> {e.Message}\n{e.StackTrace}"
                        );
                        yield break;
                    }

                    if (hasNextStep)
                    {
                        yield return buildTask.Current;
                    }
                    else
                    {
                        canProcess = false;
                    }
                }
            }

            private IEnumerator ProcessQueuedChunk(Vector2Int coord)
            {
                if (!ShouldContinueQueuedChunkBuild(coord))
                {
                    yield break;
                }

                yield return ProcessNeighborData(coord);

                if (!ShouldContinueQueuedChunkBuild(coord))
                {
                    yield break;
                }

                SpawnChunkMesh(coord);
                StartChunkFadeIn(coord);
            }

            private bool ShouldContinueQueuedChunkBuild(Vector2Int coord)
            {
                return !terrainDataProcessor.HasActiveChunk(coord)
                    && IsWithinRetentionBounds(coord);
            }

            private IEnumerator ProcessNeighborData(Vector2Int coord)
            {
                yield return RunNeighborPass(
                    coord,
                    neighbor =>
                    {
                        if (terrainDataProcessor.HasTileData(neighbor))
                        {
                            return false;
                        }

                        terrainDataProcessor.GenerateRawData(neighbor);
                        return true;
                    }
                );

                yield return RunNeighborPass(
                    coord,
                    neighbor =>
                    {
                        if (terrainDataProcessor.IsSanitized(neighbor))
                        {
                            return false;
                        }

                        terrainDataProcessor.SanitizeGlobalChunk(neighbor);
                        terrainDataProcessor.MarkSanitized(neighbor);
                        return true;
                    }
                );
            }

            private void StartChunkFadeIn(Vector2Int coord)
            {
                if (terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk chunk))
                {
                    chunk.StartFadeIn();
                }
            }

            private bool IsWithinRetentionBounds(Vector2Int coord)
            {
                int retentionRadius = Mathf.Max(0, host.cameraConfig.viewDistanceChunks);
                int dx = Mathf.Abs(coord.x - runtime.CurrentCameraPosition.x);
                int dz = Mathf.Abs(coord.y - runtime.CurrentCameraPosition.y);
                return dx <= retentionRadius && dz <= retentionRadius;
            }

            private IEnumerator RunNeighborPass(Vector2Int coord, Func<Vector2Int, bool> action)
            {
                yield return IterateNeighbors3x3(coord, action);
            }

            private IEnumerator IterateNeighbors3x3(
                Vector2Int center,
                Func<Vector2Int, bool> action
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
            }

            private void SpawnChunkMesh(Vector2Int coord)
            {
                if (terrainDataProcessor.HasActiveChunk(coord))
                {
                    return;
                }

                var position = new Vector3(
                    coord.x * runtime.ChunkBoundSize,
                    0,
                    coord.y * runtime.ChunkBoundSize
                );
                var chunk = chunkPool.Get(position);
                chunk.InitBuild(host, coord);
                chunk.UpdateVisibility(runtime.CameraPlanes);
                terrainDataProcessor.RegisterChunk(coord, chunk);
            }
        }
    }
}
