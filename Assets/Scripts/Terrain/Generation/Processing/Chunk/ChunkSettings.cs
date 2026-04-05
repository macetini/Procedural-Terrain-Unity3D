namespace Assets.Scripts.Terrain.Generation.Processing.Chunk
{
    internal class ChunkSettings
    {
        public float FrustumPadding { get; private set; }
        public float SkirtDepth { get; private set; }
        public int ChunkSize { get; private set; }
        public float TileSize { get; private set; }
        public float ElevationStepHeight { get; private set; }
        public int MaxElevationStep { get; private set; }
        public float ChunkBoundSize { get; private set; }

        public static ChunkSettings FromGenerator(ProceduralTerrain generator)
        {
            return new ChunkSettings
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
