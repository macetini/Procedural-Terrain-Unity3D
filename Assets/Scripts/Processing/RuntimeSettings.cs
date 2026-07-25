using ProceduralTerrain.Builder;
using UnityEngine;

namespace ProceduralTerrain.Processing
{
    public struct RuntimeSettings
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
        public Vector3 VisibilityBoundsOffset { get; private set; }
        public Vector3 VisibilityBoundsSize { get; private set; }

        public static RuntimeSettings FromHost(ITerrainHost host)
        {
            float chunkBoundSize = host.Terrain.chunkSize * host.Terrain.tileSize;
            float lodDistance1 = host.LOD.lod1Distance * chunkBoundSize;
            float lodDistance2 = host.LOD.lod2Distance * chunkBoundSize;

            float maxHeight =
                host.Terrain.maxElevationStepsCount * host.Terrain.elevationStepHeight;

            float halfSize = chunkBoundSize * 0.5f;

            float padding = host.CameraConfig.frustumPadding;

            float skirtDepth = host.Terrain.skirtDepth;

            return new RuntimeSettings
            {
                FrustumPadding = padding,
                SkirtDepth = skirtDepth,

                ChunkSize = host.Terrain.chunkSize,
                TileSize = host.Terrain.tileSize,

                ElevationStepHeight = host.Terrain.elevationStepHeight,

                MaxElevationStep = host.Terrain.maxElevationStepsCount,

                ChunkBoundSize = chunkBoundSize,

                SqrLodDistance1 = lodDistance1 * lodDistance1,
                SqrLodDistance2 = lodDistance2 * lodDistance2,

                VisibilityBoundsOffset = new Vector3(halfSize, maxHeight * 0.5f, halfSize),

                VisibilityBoundsSize = new Vector3(
                    chunkBoundSize + padding,
                    maxHeight + skirtDepth + padding,
                    chunkBoundSize + padding
                ),
            };
        }
    }
}
