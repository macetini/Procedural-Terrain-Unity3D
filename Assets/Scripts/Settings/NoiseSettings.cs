namespace ProceduralTerrain.Settings
{
    [System.Serializable]
    public class NoiseSettings
    {
        public int seed = 1337;
        public float scale = 0.05f;
        public int octaves = 4;
        public float persistence = 0.5f;
        public float lacunarity = 2.0f;

        public void ClampValues()
        {
            scale = UnityEngine.Mathf.Max(0.0001f, scale);
            octaves = UnityEngine.Mathf.Max(1, octaves);
            persistence = UnityEngine.Mathf.Clamp01(persistence);
            lacunarity = UnityEngine.Mathf.Max(1f, lacunarity);
        }
    }
}
