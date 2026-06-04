using ProceduralTerrain.Generation.Data;
using ProceduralTerrain.Processing;
using UnityEngine;

namespace ProceduralTerrain.Generation
{
    /// <summary>
    /// Removes stale chunks and tile data outside retention bounds.
    /// </summary>
    internal class CleanupManager
    {
        private readonly CleanupState cleanup = new();
        private readonly RuntimeState runtime;
        private readonly ITerrainDataProcessor terrainDataProcessor;
        private readonly IChunkPool chunkPool;
        private readonly ITerrainHost host;

        public CleanupManager(
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

        public void ResetForRebuild()
        {
            cleanup.ResetForRebuild();
        }

        // Public cleanup API

        public void CleanupRemoteChunks()
        {
            if (ShouldRunOrphanSweep())
            {
                CleanupSceneChunkOrphans();
            }
            RemoveOutOfBoundsRegisteredChunks();
            EvictStaleTileData();
            LogCleanupSummary();
        }

        // Cleanup policy

        private bool ShouldRunOrphanSweep()
        {
            if (host.Debug.orphanSweepPeriod < 0)
            {
                return false;
            }
            if (host.Debug.orphanSweepPeriod == 0)
            {
                return true;
            }
            cleanup.CleanupPassCounter++;
            if (cleanup.CleanupPassCounter >= host.Debug.orphanSweepPeriod)
            {
                cleanup.CleanupPassCounter = 0;
                return true;
            }
            return false;
        }

        // Scene and registry cleanup

        private void CleanupSceneChunkOrphans()
        {
            cleanup.BeginSceneSweep();
            host.GetChunkChildren(cleanup.SceneChunksSnapshot);
            for (int i = 0; i < cleanup.SceneChunksSnapshot.Count; i++)
            {
                TerrainChunk sceneChunk = cleanup.SceneChunksSnapshot[i];
                if (sceneChunk == null || !sceneChunk.gameObject.activeSelf)
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
            bool isForeignChunk =
                sceneChunk.Generator != null && !ReferenceEquals(sceneChunk.Generator, host);
            return isDuplicateCoord || isOutOfBounds || isForeignChunk;
        }

        // Tile-data eviction and bounds helpers

        private void LogCleanupSummary()
        {
            if (!host.Debug.cleanupLogs)
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
                chunkPool.Return(chunk);
            }
        }

        private void EvictStaleTileData()
        {
            int dataRetentionRadius = host.CameraConfig.viewDistanceChunks + 2;
            terrainDataProcessor.GetTileDataKeysNonAlloc(cleanup.TileDataKeysSnapshot);

            for (int i = 0; i < cleanup.TileDataKeysSnapshot.Count; i++)
            {
                Vector2Int coord = cleanup.TileDataKeysSnapshot[i];
                int dx = Mathf.Abs(coord.x - runtime.CurrentCameraPosition.x);
                int dz = Mathf.Abs(coord.y - runtime.CurrentCameraPosition.y);

                if (dx > dataRetentionRadius || dz > dataRetentionRadius)
                {
                    terrainDataProcessor.EvictTileData(coord);
                }
            }
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
            return Mathf.Max(0, host.CameraConfig.viewDistanceChunks);
        }

        // Diagnostics
    }
}
