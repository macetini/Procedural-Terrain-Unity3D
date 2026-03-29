namespace Assets.Scripts.Terrain.Runtime
{
    public class ChunkConfig
    {
        public float FrustumPadding;
        public float SkirtDepth;
        public int ChunkSize;
        public float TileSize;
        public float ElevationStepHeight;
        public int MaxElevationStep;
        public float ChunkBoundSize;

        public static ChunkConfig FromGenerator(TerrainGenerator generator)
        {
            return new ChunkConfig
            {
                FrustumPadding = generator.cameraConfig.frustumPadding,
                SkirtDepth = generator.terrain.skirtDepth,
                ChunkSize = generator.terrain.chunkSize,
                TileSize = generator.terrain.tileSize,
                ElevationStepHeight = generator.terrain.elevationStepHeight,
                MaxElevationStep = generator.terrain.maxElevationStepsCount,
                ChunkBoundSize = generator.terrain.chunkSize * generator.terrain.tileSize,
            };
        }
    }
}
