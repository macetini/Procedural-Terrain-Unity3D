using System.Collections.Generic;
using UnityEngine;

public class TerrainDataProcessor
{
    private readonly TerrainSampler sampler;
    private readonly TerrainSanitizer sanitizer;
    private readonly TerrainChunkRegistry chunkRegistry;

    private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap = new();

    public TerrainDataProcessor(int chunkSize)
    {
        sampler = new TerrainSampler(tileMap, chunkSize);
        sanitizer = new TerrainSanitizer(tileMap, chunkSize);
        chunkRegistry = new TerrainChunkRegistry();
    }

    // TerrainSampler
    public void RemoveTileData(Vector2Int coord) => sampler.RemoveTileData(coord);

    public void GenerateRawData(Vector2Int coord) => sampler.GenerateRawData(coord);

    public bool HasTileData(Vector2Int coord) => sampler.HasTileData(coord);

    public ChunkNeighborStruct GetNeighborGrids(Vector2Int coord) =>
        sampler.GetNeighborGrids(coord);

    //

    // TerrainSanitizer
    public void RemoveSanitization(Vector2Int coord) => sanitizer.RemoveSanitization(coord);

    public void SanitizeData(Vector2Int cameraOrigin, int dataRadius) =>
        sanitizer.SanitizeCurrentTileMeshData(cameraOrigin, dataRadius);

    public void MarkSanitized(Vector2Int coord) => sanitizer.MarkSanitized(coord);

    public bool IsSanitized(Vector2Int coord) => sanitizer.IsSanitized(coord);

    public void SanitizeGlobalChunk(Vector2Int tilePos) => sanitizer.SanitizeGlobalChunk(tilePos);

    public void RegisterChunk(Vector2Int coord, TerrainChunk chunk) =>
        chunkRegistry.RegisterChunk(coord, chunk);

    public void UnregisterChunk(Vector2Int coord) => chunkRegistry.UnregisterChunk(coord);

    public Dictionary<Vector2Int, TerrainChunk>.KeyCollection ActiveChunkKeys =>
        chunkRegistry.ActiveChunkKeys;

    //

    // TerrainChunkRegistry
    public bool HasActiveChunk(Vector2Int coord) => chunkRegistry.HasActiveChunk(coord);

    public bool TryGetActiveChunk(Vector2Int coord, out TerrainChunk chunk) =>
        chunkRegistry.TryGetActiveChunk(coord, out chunk);

    public void GetActiveKeysNonAlloc(List<Vector2Int> targetList) =>
        chunkRegistry.GetActiveKeysNonAlloc(targetList);

    //

    public void ClearAll() // WARNING: Expensive. Should only be only used during the development phase.
    {
        chunkRegistry.ClearAll();
        sanitizer.ClearAll();
        tileMap.Clear();
    }
}
