using System.Collections;
using Assets.Scripts.Terrain.Generation.Processing;
using Assets.Scripts.Terrain.Generation.Processing.Chunk;
using UnityEngine;

namespace Assets.Scripts.Terrain.Generation
{
    internal class ChunkBuildQueue
    {
        private readonly BuildQueueState buildState = new();
        private readonly RuntimeState runtime;
        private readonly TerrainDataProcessor terrainDataProcessor;
        private readonly TerrainChunkPool chunkPool;
        private readonly ProceduralTerrain generator;

        public ChunkBuildQueue(
            RuntimeState runtime,
            TerrainDataProcessor terrainDataProcessor,
            TerrainChunkPool chunkPool,
            ProceduralTerrain generator
        )
        {
            this.runtime = runtime;
            this.terrainDataProcessor = terrainDataProcessor;
            this.chunkPool = chunkPool;
            this.generator = generator;
        }

        public void Clear()
        {
            buildState.Clear();
        }

        public void EnqueueVisibleChunksAroundCamera()
        {
            int viewDist = generator.cameraConfig.viewDistanceChunks;
            for (int x = -viewDist; x <= viewDist; x++)
            {
                EnqueueVisibleChunksAtColumnOffset(x);
            }
        }

        public void EnqueueVisibleChunksAtColumnOffset(int xOffset)
        {
            int viewDist = generator.cameraConfig.viewDistanceChunks;
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
                generator.StartCoroutine(ProcessBuildQueue());
            }
        }

        public IEnumerator ProcessLocalChunks()
        {
            int viewDist = generator.cameraConfig.viewDistanceChunks;
            int batchSize = Mathf.Max(1, generator.lod.visibilityBatchSize);
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

        private bool TryEnqueueChunkBuild(Vector2Int coord)
        {
            if (
                coord == null
                || terrainDataProcessor.HasActiveChunk(coord)
                || !buildState.QueueHash.Add(coord)
            )
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

        private IEnumerator ProcessBuildQueue()
        {
            buildState.IsProcessing = true;

            while (buildState.Queue.Count > 0)
            {
                Vector2Int coord = buildState.Queue.Dequeue();
                yield return SafeBuildChunk(coord);

                buildState.QueueHash.Remove(coord);
            }

            buildState.IsProcessing = false;
        }

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
                catch (System.Exception e)
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
            int retentionRadius = Mathf.Max(0, generator.cameraConfig.viewDistanceChunks);
            int dx = Mathf.Abs(coord.x - runtime.CurrentCameraPosition.x);
            int dz = Mathf.Abs(coord.y - runtime.CurrentCameraPosition.y);
            return dx <= retentionRadius && dz <= retentionRadius;
        }

        private IEnumerator GenerateRawDataForChunk(Vector2Int coord)
        {
            yield return IterateNeighbors3x3(
                coord,
                n =>
                {
                    if (!terrainDataProcessor.HasTileData(n))
                    {
                        terrainDataProcessor.GenerateRawData(n);
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
        }

        private void SpawnChunkMesh(Vector2Int coord)
        {
            if (coord == null || terrainDataProcessor.HasActiveChunk(coord))
                return;
            var position = new Vector3(
                coord.x * runtime.ChunkBoundSize,
                0,
                coord.y * runtime.ChunkBoundSize
            );
            var chunk = chunkPool.Get(position);
            chunk.InitBuild(generator, coord);
            chunk.UpdateVisibility(runtime.CameraPlanes);
            terrainDataProcessor.RegisterChunk(coord, chunk);
        }
    }
}
