namespace ProceduralTerrain.Settings
{
    [System.Serializable]
    public class LODSettings
    {
        public float distance1 = 640f; // Distance to switch to MEDIUM detail
        public float distance2 = 960f; // Distance to switch to LOW detail
        public int visibilityBatchSize = 10; // Formerly visibilityCheckFrameCount
        public int step0 = 1; // LOD 0 (full detail)
        public int step1 = 2; // LOD 1 (medium)
        public int step2 = 4; // LOD 2 (low)

        public void ClampValues(int chunkSize)
        {
            int safeChunkSize = UnityEngine.Mathf.Max(1, chunkSize);

            distance1 = UnityEngine.Mathf.Max(0f, distance1);
            distance2 = UnityEngine.Mathf.Max(distance1, distance2);

            visibilityBatchSize = UnityEngine.Mathf.Max(1, visibilityBatchSize);

            step0 = UnityEngine.Mathf.Clamp(step0, 1, safeChunkSize);
            step1 = UnityEngine.Mathf.Clamp(step1, 1, safeChunkSize);
            step2 = UnityEngine.Mathf.Clamp(step2, 1, safeChunkSize);
        }
    }
}
