using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.ProceduralTerrain.Generation.Data;
using UnityEngine;

namespace Assets.Scripts.ProceduralTerrain.Generation
{
    internal class ChunkVisibilitySystem
    {
        private readonly RuntimeState runtime;
        private readonly TerrainDataProcessor terrainDataProcessor;
        private readonly ProceduralTerrain generator;
        private readonly List<Vector2Int> keysSnapshot = new();

        public ChunkVisibilitySystem(
            RuntimeState runtime,
            TerrainDataProcessor terrainDataProcessor,
            ProceduralTerrain generator
        )
        {
            this.runtime = runtime;
            this.terrainDataProcessor = terrainDataProcessor;
            this.generator = generator;
        }

        public IEnumerator VisibilityCheckRoutine()
        {
            while (
                runtime.WorldMonitoringActive && generator != null && generator.isActiveAndEnabled
            )
            {
                GeometryUtility.CalculateFrustumPlanes(
                    generator.cameraConfig.reference,
                    runtime.CameraPlanes
                );

                terrainDataProcessor.GetActiveKeysNonAlloc(keysSnapshot);

                for (int i = 0; i < keysSnapshot.Count; i++)
                {
                    Vector2Int key = keysSnapshot[i];

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
                    {
                        yield return null;
                    }
                }
                // Short rest before the next full world sweep
                yield return null;
            }
        }
    }
}
