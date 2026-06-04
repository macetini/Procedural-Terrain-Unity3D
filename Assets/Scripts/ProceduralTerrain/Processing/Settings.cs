namespace ProceduralTerrain.Processing
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
            float chunkBoundSize = host.Terrain.chunkSize * host.Terrain.tileSize;
            float maxHeight =
                host.Terrain.maxElevationStepsCount * host.Terrain.elevationStepHeight;
            float halfSize = chunkBoundSize * 0.5f;
            float padding = host.CameraConfig.frustumPadding;
            float skirtDepth = host.Terrain.skirtDepth;

            return new Settings
            {
                FrustumPadding = padding,
                SkirtDepth = skirtDepth,

                ChunkSize = host.Terrain.chunkSize,
                TileSize = host.Terrain.tileSize,

                ElevationStepHeight = host.Terrain.elevationStepHeight,

                MaxElevationStep = host.Terrain.maxElevationStepsCount,

                ChunkBoundSize = chunkBoundSize,

                SqrLodDistance1 = host.LOD.distance1 * host.LOD.distance1,
                SqrLodDistance2 = host.LOD.distance2 * host.LOD.distance2,

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
