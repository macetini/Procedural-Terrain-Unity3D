using System.Collections.Generic;
using UnityEngine;

public class TerrainDataProcessor
{
    private readonly TerrainSampler sampler;
    private TerrainSanitizer sanitizer;
    public TerrainChunkRegistry ChunkRegistry { get; private set; }

    private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap = new();

    public TerrainDataProcessor(int chunkSize)
    {
        sampler = new TerrainSampler(tileMap, chunkSize);
        sanitizer = new TerrainSanitizer(tileMap, chunkSize);
        ChunkRegistry = new TerrainChunkRegistry();
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

    public void SanitizeCurrentTileMeshData(Vector2Int cameraOrigin, int dataRadius) =>
        sanitizer.SanitizeCurrentTileMeshData(cameraOrigin, dataRadius);

    public bool IsSanitized(Vector2Int coord) => sanitizer.IsSanitized(coord);

    public void SanitizeGlobalChunk(Vector2Int tilePos) => sanitizer.SanitizeGlobalChunk(tilePos);

    public void MarkSanitized(Vector2Int coord) => sanitizer.MarkSanitized(coord);
    //

    public void ClearAll() // WARNING: Expensive. Should only be only used during the development phase.
    {
        ChunkRegistry.ClearAll();
        sanitizer.ClearAll();
        tileMap.Clear();
    }
}
