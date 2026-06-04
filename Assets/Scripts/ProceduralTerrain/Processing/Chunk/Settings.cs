namespace ProceduralTerrain.Processing.Chunk
{
    public struct Settings
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

        public static Settings FromHost(ITerrainHost host)
        {
            float chunkBoundSize = host.terrain.chunkSize * host.terrain.tileSize;
            float maxHeight =
                host.terrain.maxElevationStepsCount * host.terrain.elevationStepHeight;
            float halfSize = chunkBoundSize * 0.5f;
            float padding = host.cameraConfig.frustumPadding;
            float skirtDepth = host.terrain.skirtDepth;

            return new Settings
            {
                FrustumPadding = padding,
                SkirtDepth = skirtDepth,

                ChunkSize = host.terrain.chunkSize,
                TileSize = host.terrain.tileSize,

                ElevationStepHeight = host.terrain.elevationStepHeight,

                MaxElevationStep = host.terrain.maxElevationStepsCount,

                ChunkBoundSize = chunkBoundSize,

                SqrLodDistance1 = host.lod.distance1 * host.lod.distance1,
                SqrLodDistance2 = host.lod.distance2 * host.lod.distance2,

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
