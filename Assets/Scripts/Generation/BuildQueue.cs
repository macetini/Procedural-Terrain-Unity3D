using System;
using System.Collections;
using ProceduralTerrain.Generation.Data;
using ProceduralTerrain.Processing;
using ProceduralTerrain.Builder;
using UnityEngine;

namespace ProceduralTerrain.Generation
{
    /// <summary>
    /// Maintains the chunk build queue and schedules coroutine-based processing.
    /// </summary>
    internal class BuildQueue
    {
        private readonly BuildQueueState buildState = new();
        private readonly RuntimeState runtime;
        private readonly ITerrainDataCoordinator terrainDataProcessor;
        private readonly ITerrainHost host;
        private readonly BuildWorker buildWorker;

        public BuildQueue(
            RuntimeState runtime,
            ITerrainDataCoordinator terrainDataProcessor,
            IChunkPool chunkPool,
            ITerrainHost host
        )
        {
            this.runtime = runtime;
            this.terrainDataProcessor = terrainDataProcessor;
            this.host = host;
            buildWorker = new BuildWorker(runtime, terrainDataProcessor, chunkPool, host);
        }

        public void Clear()
        {
            buildState.Clear();
        }

        // Public queue API
        public void EnqueueVisibleChunksAroundCamera()
        {
            var viewDist = host.CameraConfig.viewDistanceChunks;
            for (var x = -viewDist; x <= viewDist; x++)
            {
                EnqueueVisibleChunksAtColumnOffset(x);
            }
        }

        private void EnqueueVisibleChunksAtColumnOffset(int xOffset)
        {
            var viewDist = host.CameraConfig.viewDistanceChunks;
            var baseX = runtime.CurrentCameraPosition.x + xOffset;
            var baseY = runtime.CurrentCameraPosition.y;
            for (var z = -viewDist; z <= viewDist; z++)
            {
                var coord = new Vector2Int(baseX, baseY + z);
                if (
                    !TryEnqueueChunkBuild(coord)
                    && terrainDataProcessor.TryGetActiveChunk(coord, out var chunk)
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
            var viewDist = host.CameraConfig.viewDistanceChunks;
            var batchSize = Mathf.Max(1, host.LOD.visibilityBatchSize);
            var processed = 0;

            for (var x = -viewDist; x <= viewDist; x++)
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
        private sealed class BuildWorker
        {
            private readonly RuntimeState runtime;
            private readonly ITerrainDataCoordinator terrainDataProcessor;
            private readonly IChunkPool chunkPool;
            private readonly ITerrainHost host;

            public BuildWorker(
                RuntimeState runtime,
                ITerrainDataCoordinator terrainDataProcessor,
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
                    var coord = buildState.Queue.Dequeue();
                    yield return SafeBuildChunk(coord);

                    buildState.QueueHash.Remove(coord);
                }

                buildState.IsProcessing = false;
            }

            // Build pipeline internals

            private IEnumerator SafeBuildChunk(Vector2Int coord)
            {
                var buildTask = ProcessQueuedChunk(coord);
                var canProcess = true;

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
                if (terrainDataProcessor.TryGetActiveChunk(coord, out var chunk))
                {
                    chunk.StartFadeIn();
                }
            }

            private bool IsWithinRetentionBounds(Vector2Int coord)
            {
                var retentionRadius = Mathf.Max(0, host.CameraConfig.viewDistanceChunks);
                var dx = Mathf.Abs(coord.x - runtime.CurrentCameraPosition.x);
                var dz = Mathf.Abs(coord.y - runtime.CurrentCameraPosition.y);
                
                return dx <= retentionRadius && dz <= retentionRadius;
            }

            private static IEnumerator RunNeighborPass(Vector2Int coord, Func<Vector2Int, bool> action)
            {
                yield return IterateNeighbors3X3(coord, action);
            }

            private static IEnumerator IterateNeighbors3X3(
                Vector2Int center,
                Func<Vector2Int, bool> action
            )
            {
                for (var x = -1; x <= 1; x++)
                {
                    for (var z = -1; z <= 1; z++)
                    {
                        var neighbor = center + new Vector2Int(x, z);
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
