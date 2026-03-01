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

        // Sampler
        public void RemoveTileData(Vector2Int coord) => sampler.RemoveTileData(coord);

        public void GenerateRawData(Vector2Int coord) => sampler.GenerateRawData(coord);

        public bool HasTileData(Vector2Int coord) => sampler.HasTileData(coord);

        public ChunkNeighborStruct GetNeighborGrids(Vector2Int coord) =>
            sampler.GetNeighborGrids(coord);

        //

        // Sanitizer
        public void RemoveSanitization(Vector2Int coord) => sanitizer.RemoveSanitization(coord);

        public void SanitizeData(Vector2Int cameraOrigin, int dataRadius) =>
            sanitizer.SanitizeCurrentTileMeshData(cameraOrigin, dataRadius);

        public void MarkSanitized(Vector2Int coord) => sanitizer.MarkSanitized(coord);

        public bool IsSanitized(Vector2Int coord) => sanitizer.IsSanitized(coord);

        public void SanitizeGlobalChunk(Vector2Int tilePos) =>
            sanitizer.SanitizeGlobalChunk(tilePos);

        //

        // Registry
        public bool HasActiveChunk(Vector2Int coord) => registry.HasActiveChunk(coord);

        public bool TryGetActiveChunk(Vector2Int coord, out TerrainChunk chunk) =>
            registry.TryGetActiveChunk(coord, out chunk);

        public void GetActiveKeysNonAlloc(List<Vector2Int> targetList) =>
            registry.GetActiveKeysNonAlloc(targetList);

        public void RegisterChunk(Vector2Int coord, TerrainChunk chunk) =>
            registry.RegisterChunk(coord, chunk);

        public void UnregisterChunk(Vector2Int coord) => registry.UnregisterChunk(coord);

        public Dictionary<Vector2Int, TerrainChunk>.KeyCollection ActiveChunkKeys =>
            registry.ActiveChunkKeys;

        //

        public void ClearAll() // WARNING: Expensive. Should only be only used during the development phase.
        {
            registry.ClearAll();
            sanitizer.ClearAll();
            tileMap.Clear();
        }
    }
}
