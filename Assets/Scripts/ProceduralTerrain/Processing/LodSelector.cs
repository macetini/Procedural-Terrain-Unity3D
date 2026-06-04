using UnityEngine;

namespace ProceduralTerrain.Processing
{
    /// <summary>
    /// Pure LOD step selection, decoupled from mesh construction.
    /// Takes only value-type settings and positional data — no MonoBehaviour references.
    /// </summary>
    internal static class LodSelector
    {
        public static int GetStep(
            in Settings settings,
            Vector3 chunkPosition,
            Transform cameraTransform,
            int step0,
            int step1,
            int step2
        )
        {
            float halfSize = settings.ChunkBoundSize * 0.5f;
            Vector3 center = chunkPosition + new Vector3(halfSize, 0, halfSize);
            float sqrDist = (center - cameraTransform.position).sqrMagnitude;

            if (sqrDist > settings.SqrLodDistance2)
                return step2; // LOD 2 (low)
            if (sqrDist > settings.SqrLodDistance1)
                return step1; // LOD 1 (medium)
            return step0; // LOD 0 (full detail)
        }
    }
}
