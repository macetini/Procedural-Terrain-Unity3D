namespace Assets.Scripts.ProceduralTerrain.Settings
{
    [System.Serializable]
    public class TerrainSettings
    {
        public int chunkSize = 16;
        public float tileSize = 1.0f;
        public float elevationStepHeight = 1.0f;
        public int maxElevationStepsCount = 5;
        public float skirtDepth = 5f;
    }
}
