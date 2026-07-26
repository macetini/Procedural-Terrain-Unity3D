using ProceduralTerrain.Builder;
using UnityEngine;

namespace ProceduralTerrain.Processing
{
    internal interface IChunkPool
    {
        TerrainChunk Get(Vector3 position);

        void Return(TerrainChunk chunk);
    }
}
