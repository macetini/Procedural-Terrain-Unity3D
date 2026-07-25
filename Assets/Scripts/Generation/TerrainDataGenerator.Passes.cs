using UnityEngine;

namespace ProceduralTerrain.Generation
{
    internal partial class TerrainDataGenerator
    {
        // First pass: generate raw data for all chunks in view distance.
        private void FirstPass()
        {
            var dataRadius = GetInitialDataRadius();
            var totalMs = MeasureExecution(() => GenerateAndSanitizeInitialData(dataRadius));

            LogExecutionTime("Total", totalMs);
        }

        private int GetInitialDataRadius()
        {
            return host.CameraConfig.viewDistanceChunks + 1;
        }

        private void GenerateAndSanitizeInitialData(int dataRadius)
        {
            LogMeasuredStep(
                "GenerateFullMeshData()",
                () => GenerateFullMeshData(runtime.CurrentCameraPosition, dataRadius)
            );
            LogMeasuredStep(
                "SanitizeCurrentTileMeshData()",
                () => terrainDataProcessor.SanitizeData(runtime.CurrentCameraPosition, dataRadius)
            );
        }

        private static double MeasureExecution(System.Action action)
        {
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            
            action();
            
            var end = System.Diagnostics.Stopwatch.GetTimestamp();
            return (end - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        private static void LogMeasuredStep(string label, System.Action action)
        {
            var elapsedMs = MeasureExecution(action);
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
            ForEachCoordInRadius(cameraOrigin, dataRadius, terrainDataProcessor.GenerateRawData);
        }

        private static void ForEachCoordInRadius(
            Vector2Int center,
            int radius,
            System.Action<Vector2Int> action
        )
        {
            for (var x = -radius; x <= radius; x++)
            {
                for (var z = -radius; z <= radius; z++)
                {
                    action(new Vector2Int(center.x + x, center.y + z));
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
