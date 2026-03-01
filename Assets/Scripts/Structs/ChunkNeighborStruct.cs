using System.Collections.Generic;
using UnityEngine;

public struct ChunkNeighborStruct
{
    public TileMeshStruct[,] Center,
        W,
        S,
        SW,
        E,
        N,
        NW,
        NE,
        SE;

    // A helper to make sure we have the critical center data
    public readonly bool IsValid => Center != null;

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
}
