using System.Collections;
using UnityEngine;

namespace ProceduralTerrain.Generation
{
    internal partial class TerrainDataGenerator
    {
        // Runtime monitoring: process camera movement and update nearby chunks.
        private IEnumerator WorldMonitoringRoutine()
        {
            Vector2Int lastProcessedPos = new(-9999, -9999);
            while (runtime.WorldMonitoringActive && host is { IsActiveAndEnabled: true })
            {
                if (TryProcessCameraMovement(ref lastProcessedPos))
                {
                    yield return buildQueue.ProcessLocalChunks();
                }

                yield return null;
            }
        }

        private bool TryProcessCameraMovement(ref Vector2Int lastProcessedPos)
        {
            if (runtime.CurrentCameraPosition == lastProcessedPos)
            {
                return false;
            }

            lastProcessedPos = runtime.CurrentCameraPosition;
            cleanupManager.CleanupRemoteChunks();
            return true;
        }
    }
}
