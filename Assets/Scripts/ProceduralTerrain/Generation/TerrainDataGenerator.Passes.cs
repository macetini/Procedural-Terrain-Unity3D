using UnityEngine;

namespace ProceduralTerrain.Generation
{
    internal partial class TerrainDataGenerator
    {
        // First pass: generate raw data for all chunks in view distance.
        private void FirstPass()
        {
            int dataRadius = host.cameraConfig.viewDistanceChunks + 1;

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

        // Second pass: enqueue visible chunks for mesh build.
        private void SecondPass()
        {
            buildQueue.EnqueueVisibleChunksAroundCamera();
            buildQueue.StartBuildQueueIfNeeded();
        }
    }
}
