namespace Assets.Scripts.ProceduralTerrain.Settings
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
    }
}
