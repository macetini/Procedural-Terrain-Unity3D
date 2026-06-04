using UnityEngine;

namespace ProceduralTerrain.Settings
{
    [System.Serializable]
    public class TerrainSettings
    {
        public int chunkSize = 16;
        public int tileSize = 1;
        public int elevationStepHeight = 1;
        public int maxElevationStepsCount = 5;
        public int skirtDepth = 5;

        public void ClampValues()
        {
            chunkSize = Mathf.Max(1, chunkSize);
            tileSize = Mathf.Max(1, tileSize);
            elevationStepHeight = Mathf.Max(1, elevationStepHeight);
            maxElevationStepsCount = Mathf.Max(1, maxElevationStepsCount);
            skirtDepth = Mathf.Max(1, skirtDepth);
        }
    }
}
