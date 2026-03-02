using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Processor
{
    public class TerrainDataProcessor
    {
        private readonly Sampler sampler;
        private readonly Sanitizer sanitizer;
        private readonly Registry registry;

        private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap = new();

        public TerrainDataProcessor(int chunkSize)
        {
            sampler = new Sampler(tileMap, chunkSize);
            sanitizer = new Sanitizer(tileMap, chunkSize);
            registry = new Registry();
        }

        public void ClearAll() // WARNING: Expensive. Should only be only used during the development phase.
        {
            registry.ClearAll();
            sanitizer.ClearAll();
            sampler.ClearAll();
            tileMap.Clear();
        }

        public void Clear(Vector2Int coord)
        {
            registry.UnregisterChunk(coord);
            sanitizer.Invalidate(coord);
            sampler.RemoveTile(coord);
        }

        // Sampler
        public void GenerateRawData(Vector2Int coord) => sampler.GenerateRawData(coord);

        public bool HasTileData(Vector2Int coord) => sampler.HasTile(coord);

        public ChunkNeighborStruct GetNeighborGrids(Vector2Int coord) =>
            ChunkNeighborStruct.GetNeighborGrids(coord, tileMap);

        //

        // Sanitizer
        public void SanitizeData(Vector2Int cameraOrigin, int dataRadius) =>
            sanitizer.SanitizeCurrentTileMeshData(cameraOrigin, dataRadius);

        public void MarkSanitized(Vector2Int coord) => sanitizer.Validate(coord);

        public bool IsSanitized(Vector2Int coord) => sanitizer.IsSanitized(coord);

        public void SanitizeGlobalChunk(Vector2Int coord) => sanitizer.SanitizeGlobalChunk(coord);

        //

        // Registry
        public void RegisterChunk(Vector2Int coord, TerrainChunk chunk) =>
            registry.RegisterChunk(coord, chunk);

        public bool HasActiveChunk(Vector2Int coord) => registry.HasActiveChunk(coord);

        public bool TryGetActiveChunk(Vector2Int coord, out TerrainChunk chunk) =>
            registry.TryGetActiveChunk(coord, out chunk);

        public void GetActiveKeysNonAlloc(List<Vector2Int> targetList) =>
            registry.GetActiveKeysNonAlloc(targetList);

        public Dictionary<Vector2Int, TerrainChunk>.KeyCollection ActiveChunkKeys =>
            registry.ActiveChunkKeys;
        //
    }
}
