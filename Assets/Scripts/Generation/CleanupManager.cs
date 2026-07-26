using System.Linq;
using ProceduralTerrain.Generation.Data;
using ProceduralTerrain.Processing;
using ProceduralTerrain.Builder;
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
        private readonly ITerrainDataCoordinator terrainDataProcessor;
        private readonly IChunkPool chunkPool;
        private readonly ITerrainHost host;

        public CleanupManager(
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
            switch (host.Debug.orphanSweepPeriod)
            {
                case < 0:
                    return false;
                case 0:
                    return true;
            }

            cleanup.CleanupPassCounter++;
            
            if (cleanup.CleanupPassCounter < host.Debug.orphanSweepPeriod)
            {
                return false;
            }
            
            cleanup.CleanupPassCounter = 0;
            return true;
        }

        // Scene and registry cleanup

        private void CleanupSceneChunkOrphans()
        {
            cleanup.BeginSceneSweep();
            host.GetChunkChildren(cleanup.SceneChunksSnapshot);
            foreach (var sceneChunk in 
                     cleanup.SceneChunksSnapshot.Where(sceneChunk => sceneChunk && sceneChunk.gameObject.activeSelf))
            {
                if (ShouldRemoveSceneChunk(sceneChunk, out var coord))
                {
                    RemoveChunk(coord, sceneChunk);
                }
            }
        }

        private void RemoveOutOfBoundsRegisteredChunks()
        {
            cleanup.BeginCleanupPass();
            terrainDataProcessor.GetActiveKeysNonAlloc(cleanup.VisibilityKeysSnapshot);
            foreach (var coord in cleanup.VisibilityKeysSnapshot.Where(
                         coord => !IsWithinRetentionBounds(coord)))
            {
                if (terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk chunk))
                {
                    RemoveChunk(coord, chunk);
                }
            }
        }

        private bool ShouldRemoveSceneChunk(TerrainChunk sceneChunk, out Vector2Int coord)
        {
            coord = sceneChunk.ChunkCoord;

            var isDuplicateCoord = !cleanup.SeenCoords.Add(coord);
            var isOutOfBounds = !IsWithinRetentionBounds(coord);
            var isForeignChunk =
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
                terrainDataProcessor.TryGetActiveChunk(coord, out var activeChunk)
                && activeChunk == chunk
            )
            {
                terrainDataProcessor.Clear(coord);
            }
            if (chunk)
            {
                chunkPool.Return(chunk);
            }
        }

        private void EvictStaleTileData()
        {
            var dataRetentionRadius = host.CameraConfig.viewDistanceChunks + 2;
            terrainDataProcessor.GetTileDataKeysNonAlloc(cleanup.TileDataKeysSnapshot);

            foreach (var coord in from coord in 
                         cleanup.TileDataKeysSnapshot let dx = Mathf.Abs(coord.x - runtime.CurrentCameraPosition.x) 
                     let dz = Mathf.Abs(coord.y - runtime.CurrentCameraPosition.y) 
                     where dx > dataRetentionRadius || dz > dataRetentionRadius select coord)
            {
                terrainDataProcessor.EvictTileData(coord);
            }
        }

        private bool IsWithinRetentionBounds(Vector2Int coord)
        {
            var retentionRadius = GetRetentionRadius();
            var dx = Mathf.Abs(coord.x - runtime.CurrentCameraPosition.x);
            var dz = Mathf.Abs(coord.y - runtime.CurrentCameraPosition.y);
            return dx <= retentionRadius && dz <= retentionRadius;
        }

        private int GetRetentionRadius()
        {
            return Mathf.Max(0, host.CameraConfig.viewDistanceChunks);
        }
    }
}
