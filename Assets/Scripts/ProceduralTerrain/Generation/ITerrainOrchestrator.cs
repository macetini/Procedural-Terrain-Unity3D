using ProceduralTerrain.Processing.Chunk.Data;
using UnityEngine;

namespace ProceduralTerrain.Generation
{
    public interface ITerrainOrchestrator
    {
        void BuildTerrain();
        void ResetGeneratorState();
        void UpdateCurrentCameraPosition();
        void Destroy();
        ChunkNeighborStruct GetNeighborGrids(Vector2Int chunkCoord);
        int[] GetPrecalculatedTriangles(int resolution);
    }
}
