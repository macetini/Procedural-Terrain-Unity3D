namespace ProceduralTerrain.Settings
{
    [System.Serializable]
    public class TerrainSettings
    {
        public int chunkSize = 16;
        public float tileSize = 1.0f;
        public float elevationStepHeight = 1.0f;
        public int maxElevationStepsCount = 5;
        public float skirtDepth = 5f;

        public void ClampValues()
        {
            chunkSize = UnityEngine.Mathf.Max(1, chunkSize);
            tileSize = UnityEngine.Mathf.Max(0.01f, tileSize);
            elevationStepHeight = UnityEngine.Mathf.Max(0.01f, elevationStepHeight);
            maxElevationStepsCount = UnityEngine.Mathf.Max(0, maxElevationStepsCount);
            skirtDepth = UnityEngine.Mathf.Max(0f, skirtDepth);
        }
    }
}
