using System.Collections.Generic;
using UnityEngine;

public class TerrainDataProcessor
{
    private readonly TerrainSampler sampler;
    public TerrainSanitizer Sanitizer { get; private set; }
    public TerrainChunkRegistry ChunkRegistry { get; private set; }

    private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap = new();

    public TerrainDataProcessor(int chunkSize)
    {
        sampler = new TerrainSampler(tileMap, chunkSize);
        Sanitizer = new TerrainSanitizer(tileMap, chunkSize);
        ChunkRegistry = new TerrainChunkRegistry();
    }

    // TerrainSampler
    public void RemoveTileData(Vector2Int coord) => sampler.RemoveTileData(coord);

    public void GenerateRawData(Vector2Int coord) => sampler.GenerateRawData(coord);

    public bool HasTileData(Vector2Int coord) => sampler.HasTileData(coord);

    public ChunkNeighborStruct GetNeighborGrids(Vector2Int coord) =>
        sampler.GetNeighborGrids(coord);

    //

    public void ClearAll() // WARNING: Expensive. Should only be only used during the development phase.
    {
        ChunkRegistry.ClearAll();
        Sanitizer.ClearAll();
        tileMap.Clear();
    }
}
