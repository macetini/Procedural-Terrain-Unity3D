using UnityEngine;

namespace ProceduralTerrain.Settings
{
    [System.Serializable]
    public class LODSettings
    {
        public float lod1Distance = 3f; // Chunks from camera to switch to MEDIUM detail
        public float lod2Distance = 6f; // Chunks from camera to switch to LOW detail
        public int visibilityBatchSize = 10; //
        public int lod0Step = 1; // LOD 0 (full detail)
        public int lod1Step = 2; // LOD 1 (medium)
        public int lod2Step = 4; // LOD 2 (low)

        public void ClampValues(int chunkSize)
        {
            int safeChunkSize = Mathf.Max(1, chunkSize);

            lod1Distance = Mathf.Max(0f, lod1Distance);
            lod2Distance = Mathf.Max(lod1Distance, lod2Distance);

            visibilityBatchSize = Mathf.Max(1, visibilityBatchSize);

            lod0Step = Mathf.Clamp(lod0Step, 1, safeChunkSize);
            lod1Step = Mathf.Clamp(lod1Step, 1, safeChunkSize);
            lod2Step = Mathf.Clamp(lod2Step, 1, safeChunkSize);
        }
    }
}
