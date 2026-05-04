using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Terrain.Generation.Processing;
using Assets.Scripts.Terrain.Generation.Processing.Chunk;
using Assets.Scripts.Terrain.Generation.Processing.Chunk.Data;
using Assets.Scripts.Terrain.Utils;
using UnityEngine;

namespace Assets.Scripts.Terrain.Generation
{
    public class TerrainDataGenerator
    {
        private readonly RuntimeState runtime = new();
        private readonly ProceduralTerrain generator;
        private readonly TerrainDataProcessor terrainDataProcessor;
        private readonly TerrainChunkPool chunkPool;
        private readonly ChunkBuildQueue buildQueue;
        private readonly ChunkCleanupManager cleanupManager;
        private readonly ChunkVisibilitySystem visibilitySystem;
        private readonly Dictionary<int, int[]> triangleCache = new();

        public ChunkNeighborStruct GetNeighborGrids(Vector2Int coord)
        {
            if (coord == null)
            {
                Debug.LogError("[TerrainDataGenerator] GetNeighborGrids called with null coord");
                return default;
            }
            return terrainDataProcessor.GetNeighborGrids(coord);
        }

        public void Destroy()
        {
            runtime.WorldMonitoringActive = false;
            chunkPool.Clear();
        }

        public TerrainDataGenerator(ProceduralTerrain generator)
        {
            this.generator = generator;
            terrainDataProcessor = new TerrainDataProcessor(generator.terrain.chunkSize);
            int poolMaxSize = TerrainChunkPool.CalculateMaxSize(
                generator.cameraConfig.viewDistanceChunks
            );
            chunkPool = new TerrainChunkPool(
                generator.chunkPrefab,
                generator.transform,
                poolMaxSize
            );
            terrainDataProcessor.SetChunkDisposeAction(chunk => chunkPool.Return(chunk));

            buildQueue = new ChunkBuildQueue(runtime, terrainDataProcessor, chunkPool, generator);
            cleanupManager = new ChunkCleanupManager(
                runtime,
                terrainDataProcessor,
                chunkPool,
                generator
            );
            visibilitySystem = new ChunkVisibilitySystem(runtime, terrainDataProcessor, generator);
        }

        public void ResetGeneratorState()
        {
            generator.StopAllCoroutines();
            buildQueue.Clear();
            triangleCache.Clear();
            cleanupManager.ResetForRebuild();
            terrainDataProcessor.ClearAll();
            chunkPool.Clear();
        }

        public void BuildTerrain()
        {
            var noise = generator.noise;
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

            generator.StartCoroutine(WorldMonitoringRoutine());
            generator.StartCoroutine(visibilitySystem.VisibilityCheckRoutine());
        }

        public void UpdateCurrentCameraPosition()
        {
            var pos = generator.cameraConfig.reference.transform.position;
            int currentX = Mathf.FloorToInt(pos.x / runtime.ChunkBoundSize);
            int currentZ = Mathf.FloorToInt(pos.z / runtime.ChunkBoundSize);
            var newPos = new Vector2Int(currentX, currentZ);
            if (newPos == runtime.CurrentCameraPosition)
                return;
            runtime.CurrentCameraPosition = newPos;
        }

        // --------------- FIRST PASS : GENERATE RAW DATA FOR ALL CHUNKS IN VIEW DISTANCE ---------------

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

        private static double MeasureExecution(System.Action action)
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            action();
            long end = System.Diagnostics.Stopwatch.GetTimestamp();
            return (end - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
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
            for (int x = -dataRadius; x <= dataRadius; x++)
            {
                for (int z = -dataRadius; z <= dataRadius; z++)
                {
                    var coord = new Vector2Int(cameraOrigin.x + x, cameraOrigin.y + z);
                    terrainDataProcessor.GenerateRawData(coord);
                }
            }
        }

        // --------------- SECOND PASS : BUILD MESHES FOR VISIBLE CHUNKS ---------------

        private void SecondPass()
        {
            buildQueue.EnqueueVisibleChunksAroundCamera();
            buildQueue.StartBuildQueueIfNeeded();
        }

        // --------------- WORLD MONITORING ---------------

        private IEnumerator WorldMonitoringRoutine()
        {
            Vector2Int lastProcessedPos = new(-9999, -9999);
            while (
                runtime.WorldMonitoringActive && generator != null && generator.isActiveAndEnabled
            )
            {
                if (runtime.CurrentCameraPosition != lastProcessedPos)
                {
                    lastProcessedPos = runtime.CurrentCameraPosition;

                    cleanupManager.CleanupRemoteChunks();

                    yield return buildQueue.ProcessLocalChunks();
                }
                yield return null;
            }
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
