using System;
using System.Collections.Generic;
using ProceduralTerrain.Processing.Data;
using ProceduralTerrain.Runtime;
using UnityEngine;

namespace ProceduralTerrain.Generation.Data
{
    internal interface ITerrainDataProcessor
    {
        void SetChunkDisposeAction(Action<TerrainChunk> action);
        void ClearAll();
        void Clear(Vector2Int coord);

        // Sampler
        void GenerateRawData(Vector2Int coord);
        bool HasTileData(Vector2Int coord);
        NeighborStruct GetNeighborGrids(Vector2Int coord);

        // Sanitizer
        void SanitizeData(Vector2Int cameraOrigin, int dataRadius);
        void MarkSanitized(Vector2Int coord);
        bool IsSanitized(Vector2Int coord);
        void SanitizeGlobalChunk(Vector2Int coord);

        // Registry
        void RegisterChunk(Vector2Int coord, TerrainChunk chunk);
        bool HasActiveChunk(Vector2Int coord);
        bool TryGetActiveChunk(Vector2Int coord, out TerrainChunk chunk);
        void GetActiveKeysNonAlloc(List<Vector2Int> targetList);
        void GetTileDataKeysNonAlloc(List<Vector2Int> targetList);
        void EvictTileData(Vector2Int coord);
    }
}
