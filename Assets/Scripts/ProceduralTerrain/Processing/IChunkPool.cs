using UnityEngine;

namespace ProceduralTerrain.Processing
{
    public interface IChunkPool
    {
        TerrainChunk Get(Vector3 position);
        void Return(TerrainChunk chunk);
        void Clear();
    }
}
