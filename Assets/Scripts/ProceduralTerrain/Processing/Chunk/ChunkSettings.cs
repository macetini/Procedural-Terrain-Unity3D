namespace Assets.Scripts.ProceduralTerrain.Processing.Chunk
{
    public struct ChunkSettings
    {
        public float FrustumPadding { get; private set; }
        public float SkirtDepth { get; private set; }
        public int ChunkSize { get; private set; }
        public float TileSize { get; private set; }
        public float ElevationStepHeight { get; private set; }
        public int MaxElevationStep { get; private set; }
        public float ChunkBoundSize { get; private set; }
        public float SqrLodDistance1 { get; private set; }
        public float SqrLodDistance2 { get; private set; }
        public UnityEngine.Vector3 VisibilityBoundsOffset { get; private set; }
        public UnityEngine.Vector3 VisibilityBoundsSize { get; private set; }

        public static ChunkSettings FromGenerator(ProceduralTerrain generator)
        {
            float chunkBoundSize = generator.terrain.chunkSize * generator.terrain.tileSize;
            float maxHeight =
                generator.terrain.maxElevationStepsCount * generator.terrain.elevationStepHeight;
            float halfSize = chunkBoundSize * 0.5f;
            float padding = generator.cameraConfig.frustumPadding;
            float skirtDepth = generator.terrain.skirtDepth;

            return new ChunkSettings
            {
                FrustumPadding = padding,
                SkirtDepth = skirtDepth,

                ChunkSize = generator.terrain.chunkSize,
                TileSize = generator.terrain.tileSize,

                ElevationStepHeight = generator.terrain.elevationStepHeight,

                MaxElevationStep = generator.terrain.maxElevationStepsCount,

                ChunkBoundSize = chunkBoundSize,

                SqrLodDistance1 = generator.lod.distance1 * generator.lod.distance1,
                SqrLodDistance2 = generator.lod.distance2 * generator.lod.distance2,

                VisibilityBoundsOffset = new UnityEngine.Vector3(
                    halfSize,
                    maxHeight * 0.5f,
                    halfSize
                ),

                VisibilityBoundsSize = new UnityEngine.Vector3(
                    chunkBoundSize + padding,
                    maxHeight + skirtDepth + padding,
                    chunkBoundSize + padding
                ),
            };
        }
    }
}
