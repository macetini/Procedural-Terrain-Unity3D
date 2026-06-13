using System.Collections.Generic;
using ProceduralTerrain.Generation.Data;
using ProceduralTerrain.Processing;
using ProceduralTerrain.Processing.Data;
using ProceduralTerrain.Runtime;
using ProceduralTerrain.Utils;
using UnityEngine;

namespace ProceduralTerrain.Generation
{
    /// <summary>
    /// Coordinates terrain runtime flow: initial data pass, chunk build queue, cleanup, and visibility updates.
    /// </summary>
    internal partial class TerrainDataGenerator : ITerrainOrchestrator
    {
        private readonly RuntimeState runtime = new();
        private readonly ITerrainHost host;
        private readonly TerrainNoise terrainNoise;
        private readonly TerrainDataCoordinator terrainDataProcessor;
        private readonly TerrainChunkPool chunkPool;
        private readonly BuildQueue buildQueue;
        private readonly CleanupManager cleanupManager;
        private readonly VisibilitySystem visibilitySystem;
        private readonly Dictionary<int, int[]> triangleCache = new();

        public NeighborStruct GetNeighborGrids(Vector2Int coord) =>
            terrainDataProcessor.GetNeighborGrids(coord);

        public void Destroy()
        {
            runtime.WorldMonitoringActive = false;
            chunkPool.Clear();
        }

        public TerrainDataGenerator(ITerrainHost host)
        {
            this.host = host;
            terrainNoise = CreateTerrainNoise();
            terrainDataProcessor = new TerrainDataCoordinator(host.Terrain.chunkSize, terrainNoise);
            chunkPool = CreateChunkPool();
            terrainDataProcessor.SetChunkDisposeAction(chunk => chunkPool.Return(chunk));

            (buildQueue, cleanupManager, visibilitySystem) = CreateRuntimeSystems();
        }

        public void ResetGeneratorState()
        {
            runtime.IsBuildCancelled = true;
            host.StopAllCoroutines();
            buildQueue.Clear();
            triangleCache.Clear();
            cleanupManager.ResetForRebuild();
            terrainDataProcessor.ClearAll();
            chunkPool.Clear();
            runtime.IsBuildCancelled = false;
        }

        public void BuildTerrain()
        {
            InitializeRuntimeState();
            RunInitialGenerationPasses();
            StartRuntimeCoroutines();
        }

        public void UpdateCurrentCameraPosition()
        {
            var pos = host.CameraConfig.reference.transform.position;
            var newPos = GetCameraChunkPosition(pos);
            if (newPos == runtime.CurrentCameraPosition)
                return;
            runtime.CurrentCameraPosition = newPos;
        }

        private TerrainNoise CreateTerrainNoise()
        {
            return new TerrainNoise(
                host.Noise.seed,
                host.Noise.scale,
                host.Noise.octaves,
                host.Noise.persistence,
                host.Noise.lacunarity,
                host.Terrain.maxElevationStepsCount
            );
        }

        private TerrainChunkPool CreateChunkPool()
        {
            int poolMaxSize = TerrainChunkPool.CalculateMaxSize(
                host.CameraConfig.viewDistanceChunks
            );

            return new TerrainChunkPool(host.ChunkPrefab, host.ChunkParent, poolMaxSize);
        }

        private (
            BuildQueue buildQueue,
            CleanupManager cleanupManager,
            VisibilitySystem visibilitySystem
        ) CreateRuntimeSystems()
        {
            var queue = new BuildQueue(runtime, terrainDataProcessor, chunkPool, host);
            var cleanup = new CleanupManager(runtime, terrainDataProcessor, chunkPool, host);
            var visibility = new VisibilitySystem(runtime, terrainDataProcessor, host);
            return (queue, cleanup, visibility);
        }

        private void InitializeRuntimeState()
        {
            runtime.ChunkBoundSize = host.Terrain.chunkSize * host.Terrain.tileSize;
            UpdateCurrentCameraPosition();
        }

        private void RunInitialGenerationPasses()
        {
            FirstPass();
            SecondPass();
        }

        private void StartRuntimeCoroutines()
        {
            host.StartCoroutine(WorldMonitoringRoutine());
            host.StartCoroutine(visibilitySystem.VisibilityCheckRoutine());
        }

        private Vector2Int GetCameraChunkPosition(Vector3 worldPosition)
        {
            int currentX = Mathf.FloorToInt(worldPosition.x / runtime.ChunkBoundSize);
            int currentZ = Mathf.FloorToInt(worldPosition.z / runtime.ChunkBoundSize);
            return new Vector2Int(currentX, currentZ);
        }

        public int[] GetPrecalculatedTriangles(int resolution)
        {
            if (triangleCache.TryGetValue(resolution, out var cachedTris))
            {
                return cachedTris;
            }

            var newTris = TerrainMath.GenerateTriangleIndices(resolution);
            triangleCache[resolution] = newTris;

            return newTris;
        }
    }
}
