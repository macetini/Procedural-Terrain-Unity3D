using System.Collections.Generic;
using UnityEngine;

public class TerrainDataMap
{
    public TerrainSampler TerrainSampler { get; private set; }
    public TerrainSanitizer Sanitizer { get; private set; }
    public TerrainChunkRegistry ChunkRegistry { get; private set; }

    private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap = new();

    public TerrainDataMap(int chunkSize)
    {
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
