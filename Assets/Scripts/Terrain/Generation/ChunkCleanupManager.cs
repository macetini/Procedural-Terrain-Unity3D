using Assets.Scripts.ProceduralTerrain;
using Assets.Scripts.ProceduralTerrain.Processing;
using SSHexMap.Terrain.Processing;
using UnityEngine;

namespace SSHexMap.Terrain.Generation
{
    internal class ChunkCleanupManager
    {
        private readonly CleanupState cleanup = new();
        private readonly RuntimeState runtime;
        private readonly TerrainDataProcessor terrainDataProcessor;
        private readonly TerrainChunkPool chunkPool;
        private readonly ProceduralTerrain generator;

        public ChunkCleanupManager(
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

        public void ResetForRebuild()
        {
            cleanup.ResetForRebuild();
        }

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

        private bool ShouldRunOrphanSweep()
        {
            if (generator.debug.orphanSweepPeriod < 0)
            {
                return false;
            }
            if (generator.debug.orphanSweepPeriod == 0)
            {
                return true;
            }
            cleanup.CleanupPassCounter++;
            if (cleanup.CleanupPassCounter >= generator.debug.orphanSweepPeriod)
            {
                cleanup.CleanupPassCounter = 0;
                return true;
            }
            return false;
        }

        private void CleanupSceneChunkOrphans()
        {
            cleanup.BeginSceneSweep();
            generator.GetComponentsInChildren(true, cleanup.SceneChunksSnapshot);
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
            bool isForeignChunk = sceneChunk.Generator != null && sceneChunk.Generator != generator;
            return isDuplicateCoord || isOutOfBounds || isForeignChunk;
        }

        private void LogCleanupSummary()
        {
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
                chunkPool.Return(chunk);
            }
        }

        private void EvictStaleTileData()
        {
            int dataRetentionRadius = generator.cameraConfig.viewDistanceChunks + 2;
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
            return Mathf.Max(0, generator.cameraConfig.viewDistanceChunks);
        }
    }
}
