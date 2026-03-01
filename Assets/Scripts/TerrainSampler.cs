using System.Collections.Generic;
using UnityEngine;

public class TerrainSampler
{
    private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap;
    private readonly int chunkSize;

    private Vector2Int lastLookupCoord = new(int.MaxValue, int.MinValue);
    private TileMeshStruct[,] lastLookupGrid;

    public TerrainSampler(Dictionary<Vector2Int, TileMeshStruct[,]> tileMap, int chunkSize) =>
        (this.tileMap, this.chunkSize) = (tileMap, chunkSize);

    public bool HasTileData(Vector2Int coord) => tileMap.ContainsKey(coord);

    public bool TryGetTileData(Vector2Int coord, out TileMeshStruct[,] grid) =>
        tileMap.TryGetValue(coord, out grid);

    public void RemoveTileData(Vector2Int coord) => tileMap.Remove(coord);

    public void GenerateRawData(Vector2Int coord)
    {
        if (tileMap.ContainsKey(coord))
        {
            return;
        }

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
