using System.Collections.Generic;
using SSHexMap.Terrain.Data;
using SSHexMap.Terrain.Utils;
using UnityEngine;

namespace Assets.Scripts.ProceduralTerrain.Generation.Processing
{
    public class TerrainDataProcessor
    {
        private readonly Sampler sampler;
        private readonly Sanitizer sanitizer;
        private readonly Registry registry;

        private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap = new();

        public TerrainDataProcessor(int chunkSize, TerrainNoise noise)
        {
            sampler = new Sampler(tileMap, chunkSize, noise);
            sanitizer = new Sanitizer(tileMap, chunkSize);
            registry = new Registry();
        }

        public void SetChunkDisposeAction(System.Action<TerrainChunk> action) =>
            registry.SetChunkDisposeAction(action);

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

        public void GetTileDataKeysNonAlloc(List<Vector2Int> targetList) =>
            sampler.GetTileKeysNonAlloc(targetList);

        public void EvictTileData(Vector2Int coord)
        {
            sampler.RemoveTile(coord);
            sanitizer.Invalidate(coord);
        }

        public Dictionary<Vector2Int, TerrainChunk>.KeyCollection ActiveChunkKeys =>
            registry.ActiveChunkKeys;
        //
    }
}
