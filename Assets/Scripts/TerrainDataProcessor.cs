using System.Collections.Generic;
using UnityEngine;

public class TerrainDataMap
{
    private readonly int chunkSize;

    public TerrainSanitizer Sanitizer { get; private set; }

    public TerrainDataMap(TerrainChunksGenerator generator)
    {
        chunkSize = generator.chunkSize;
        Sanitizer = new TerrainSanitizer(tileMap, chunkSize);
    }

    public void ClearAll() // WARNING: Expensive. Should only be only used during the development phase.
    {
        foreach (var chunk in activeChunks.Values)
        {
            if (chunk != null)
            {
                chunk.CallDestroy();
            }
        }

        activeChunks.Clear();
        tileMap.Clear();
        Sanitizer.ClearAll();
    }

    // --------------------------------------------------------------------------------------------
    // -------------------------------------- RAW DATA --------------------------------------------
    // --------------------------------------------------------------------------------------------

    private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap = new();

    public bool HasTileData(Vector2Int coord) => tileMap.ContainsKey(coord);

    public bool TryGetTileData(Vector2Int coord, out TileMeshStruct[,] grid) =>
        tileMap.TryGetValue(coord, out grid);

    public void RemoveTileData(Vector2Int coord) => tileMap.Remove(coord);

    public void GenerateRawData(Vector2Int coord)
    {
        if (tileMap.ContainsKey(coord))
            return;

        TileMeshStruct[,] data = new TileMeshStruct[chunkSize, chunkSize];
        int offsetX = coord.x * chunkSize;
        int offsetZ = coord.y * chunkSize;

        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                int elevation = TerrainNoise.GetElevation(offsetX + x, offsetZ + z);
                data[x, z] = new TileMeshStruct(x, z, elevation);
            }
        }
        tileMap.Add(coord, data);
    }

    // --------------------------------------------------------------------------------------------
    // -------------------------------------- ACTIVE DATA -----------------------------------------
    // --------------------------------------------------------------------------------------------

    private readonly Dictionary<Vector2Int, TerrainChunk> activeChunks = new();

    // Registry Helpers
    public bool HasActiveChunk(Vector2Int coord) => activeChunks.ContainsKey(coord);

    public bool TryGetActiveChunk(Vector2Int coord, out TerrainChunk chunk) =>
        activeChunks.TryGetValue(coord, out chunk);

    public void RegisterChunk(Vector2Int coord, TerrainChunk chunk) => activeChunks[coord] = chunk;

    public void UnregisterChunk(Vector2Int coord) => activeChunks.Remove(coord);

    // Atomic Purge: Cleans up data and registry in one go
    public void PurgeCoordinate(Vector2Int coord)
    {
        activeChunks.Remove(coord);
        tileMap.Remove(coord);
        Sanitizer.Clear(coord);
    }

    public void GetActiveKeysNonAlloc(List<Vector2Int> targetList)
    {
        targetList.Clear();
        foreach (var key in activeChunks.Keys)
            targetList.Add(key);
    }

    // Property to let the Generator see the keys for culling/cleanup
    public Dictionary<Vector2Int, TerrainChunk>.KeyCollection ActiveChunkKeys => activeChunks.Keys;

    // --------------------------------------------------------------------------------------------
    // -------------------------------------- SAMPLING --------------------------------------------
    // --------------------------------------------------------------------------------------------

    private Vector2Int lastLookupCoord = new(int.MaxValue, int.MinValue);
    private TileMeshStruct[,] lastLookupGrid;

    public float GetElevationAt(int gx, int gz)
    {
        int cx = Mathf.FloorToInt((float)gx / chunkSize);
        int cz = Mathf.FloorToInt((float)gz / chunkSize);
        int lx = gx - (cx * chunkSize);
        int lz = gz - (cz * chunkSize);

        Vector2Int lookup = new(cx, cz);

        // Cache check for high-frequency calls (like physics/droids)
        if (lookup == lastLookupCoord && lastLookupGrid != null)
        {
            return lastLookupGrid[lx, lz].Elevation;
        }

        if (tileMap.TryGetValue(lookup, out TileMeshStruct[,] grid))
        {
            lastLookupCoord = lookup;
            lastLookupGrid = grid;
            return grid[lx, lz].Elevation;
        }

        return 0f;
    }

    // --------------------------------------------------------------------------------------------
    // -------------------------------------- NEIGHBOR DATA ---------------------------------------
    // --------------------------------------------------------------------------------------------

    public ChunkNeighborStruct GetNeighborGrids(Vector2Int coord)
    {
        ChunkNeighborStruct neighbors = new();

        // Cardinal
        tileMap.TryGetValue(coord, out neighbors.Center);
        tileMap.TryGetValue(coord + Vector2Int.left, out neighbors.W);
        tileMap.TryGetValue(coord + Vector2Int.down, out neighbors.S);
        tileMap.TryGetValue(coord + Vector2Int.right, out neighbors.E);
        tileMap.TryGetValue(coord + Vector2Int.up, out neighbors.N);

        // Diagonals
        tileMap.TryGetValue(coord + new Vector2Int(-1, -1), out neighbors.SW);
        tileMap.TryGetValue(coord + new Vector2Int(-1, 1), out neighbors.NW);
        tileMap.TryGetValue(coord + new Vector2Int(1, 1), out neighbors.NE);
        tileMap.TryGetValue(coord + new Vector2Int(1, -1), out neighbors.SE);

        return neighbors;
    }
}
