using System.Collections;
using System.Collections.Generic;
using ProceduralTerrain.Generation.Data;
using ProceduralTerrain.Runtime;
using UnityEngine;

namespace ProceduralTerrain.Generation
{
    /// <summary>
    /// Runs frustum-based visibility updates for active chunks.
    /// </summary>
    internal class VisibilitySystem
    {
        private readonly RuntimeState runtime;
        private readonly ITerrainDataProcessor terrainDataProcessor;
        private readonly ITerrainHost host;
        private readonly List<Vector2Int> keysSnapshot = new();

        public VisibilitySystem(
            RuntimeState runtime,
            ITerrainDataProcessor terrainDataProcessor,
            ITerrainHost host
        )
        {
            this.runtime = runtime;
            this.terrainDataProcessor = terrainDataProcessor;
            this.host = host;
        }

        // Public visibility loop

        public IEnumerator VisibilityCheckRoutine()
        {
            while (runtime.WorldMonitoringActive && host != null && host.IsActiveAndEnabled)
            {
                // Update camera frustum planes for this sweep.
                GeometryUtility.CalculateFrustumPlanes(
                    host.CameraConfig.reference,
                    runtime.CameraPlanes
                );

                // Snapshot active keys to avoid mutation during iteration.
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
                    // Process in batches to spread work across frames.
                    if (i > 0 && i % host.LOD.visibilityBatchSize == 0)
                    {
                        yield return null;
                    }
                }
                // Yield once before the next full visibility sweep.
                yield return null;
            }
        }
    }
}
