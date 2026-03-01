using System.Collections.Generic;
using UnityEngine;

public class Sanitizer
{
    private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap;
    private readonly int chunkSize;
    private readonly HashSet<Vector2Int> sanitizedSet = new();

    public bool IsSanitized(Vector2Int coord) => sanitizedSet.Contains(coord);

    public void MarkSanitized(Vector2Int coord) => sanitizedSet.Add(coord);

    public void RemoveSanitization(Vector2Int coord) => sanitizedSet.Remove(coord);

    public void ClearAll() => sanitizedSet.Clear();

    public void Clear(Vector2Int coord) => sanitizedSet.Remove(coord);

    // Constructor
    public Sanitizer(Dictionary<Vector2Int, TileMeshStruct[,]> tileMap, int chunkSize) =>
        (this.tileMap, this.chunkSize) = (tileMap, chunkSize);

    public void SanitizeCurrentTileMeshData(Vector2Int cameraOrigin, int dataRadius)
    {
        for (int x = -dataRadius; x <= dataRadius; x++)
        {
            for (int z = -dataRadius; z <= dataRadius; z++)
            {
                Vector2Int tilePosition = new(cameraOrigin.x + x, cameraOrigin.y + z);
                // We only need to sanitize if the mesh hasn't been built yet
                if (!sanitizedSet.Contains(tilePosition))
                {
                    SanitizeGlobalChunk(tilePosition);
                    sanitizedSet.Add(tilePosition);
                }
            }
        }
    }

    public void SanitizeGlobalChunk(Vector2Int tilePos)
    {
        if (!tileMap.TryGetValue(tilePos, out TileMeshStruct[,] currentData))
        {
            return;
        }

        tileMap.TryGetValue(tilePos + Vector2Int.right, out TileMeshStruct[,] eastData);
        tileMap.TryGetValue(tilePos + Vector2Int.up, out TileMeshStruct[,] northData);

        int size = chunkSize;
        int edge = size - 1;

        for (int i = 0; i < 2; i++)
        {
            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    ref TileMeshStruct current = ref currentData[x, z];

                    // East check
                    if (x < edge)
                    {
                        TerrainMath.ClampNeighbor(ref current, ref currentData[x + 1, z]);
                    }
                    else if (eastData != null)
                    {
                        TerrainMath.ClampNeighbor(ref current, ref eastData[0, z]);
                    }

                    // North check
                    if (z < edge)
                    {
                        TerrainMath.ClampNeighbor(ref current, ref currentData[x, z + 1]);
                    }
                    else if (northData != null)
                    {
                        TerrainMath.ClampNeighbor(ref current, ref northData[x, 0]);
                    }
                }
            }
        }
    }
}
