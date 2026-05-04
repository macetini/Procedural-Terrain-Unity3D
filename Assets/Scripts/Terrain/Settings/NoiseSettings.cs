namespace SSHexMap.Terrain.Settings
{
    [System.Serializable]
    public class NoiseSettings
    {
        public int seed = 1337;
        public float scale = 0.05f;
        public int octaves = 4;
        public float persistence = 0.5f;
        public float lacunarity = 2.0f;
    }
}

