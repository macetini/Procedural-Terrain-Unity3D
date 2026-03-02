using System.Collections.Generic;
using UnityEngine;

public struct ChunkNeighborStruct
{
    public TileMeshStruct[,] Center,
        W, // West
        S, // South
        SW, // Southwest
        E, // East
        N, // North
        NW, // Northwest
        NE, // Northeast
        SE; // Southeast

    public static ChunkNeighborStruct GetNeighborGrids(
        Vector2Int coord,
        Dictionary<Vector2Int, TileMeshStruct[,]> tileMap
    )
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

    public readonly float GetElevation(int x, int z, int chunkSize)
    {
        if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
        {
            return Center[x, z].Elevation;
        }

        // Determine Directional Indices (-1, 0, or 1)
        int dirX = GetDirectionIndex(x, chunkSize);
        int dirZ = GetDirectionIndex(z, chunkSize);

        // Map to Local Neighbor Coordinates
        int nx = MapCoordinate(x, chunkSize);
        int nz = MapCoordinate(z, chunkSize);

        // Retrieve Neighbor Grid
        TileMeshStruct[,] targetGrid = GetNeighborByDir(dirX, dirZ);

        if (targetGrid != null)
        {
            return targetGrid[nx, nz].Elevation;
        }

        // Fallback: Clamp to Center's nearest edge
        int fx = Mathf.Clamp(x, 0, chunkSize - 1);
        int fz = Mathf.Clamp(z, 0, chunkSize - 1);
        return Center[fx, fz].Elevation;
    }

    private static int GetDirectionIndex(int val, int limit)
    {
        if (val < 0)
            return -1;
        if (val >= limit)
            return 1;
        return 0;
    }

    private static int MapCoordinate(int val, int limit)
    {
        if (val < 0)
            return val + limit;
        if (val >= limit)
            return val - limit;
        return val;
    }

    private readonly TileMeshStruct[,] GetNeighborByDir(int dx, int dz)
    {
        // Switch expressions are much cleaner than nested ternaries
        return (dx, dz) switch
        {
            (-1, 0) => W,
            (1, 0) => E,
            (0, -1) => S,
            (0, 1) => N,
            (-1, -1) => SW,
            (-1, 1) => NW,
            (1, 1) => NE,
            (1, -1) => SE,
            _ => null,
        };
    }
}
