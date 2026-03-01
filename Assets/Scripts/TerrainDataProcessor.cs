using System.Collections.Generic;
using UnityEngine;

public class TerrainDataMap
{
    private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap = new();
    private readonly int chunkSize;

    public TerrainSampler TerrainSampler { get; private set; }
    public TerrainSanitizer Sanitizer { get; private set; }
    public TerrainChunkRegistry ChunkRegistry { get; private set; }

    public TerrainDataMap(TerrainChunksGenerator generator)
    {
        chunkSize = generator.chunkSize;
        TerrainSampler = new TerrainSampler(tileMap, chunkSize);
        Sanitizer = new TerrainSanitizer(tileMap, chunkSize);
        ChunkRegistry = new TerrainChunkRegistry();
    }

    public void ClearAll() // WARNING: Expensive. Should only be only used during the development phase.
    {
        ChunkRegistry.ClearAll();
        Sanitizer.ClearAll();
        tileMap.Clear();
    }
}
