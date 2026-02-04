using System.Collections.Generic;
using UnityEngine;

public class TerrainDataMap
{
    private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap = new();
    private readonly HashSet<Vector2Int> sanitizedSet = new();
    private readonly Dictionary<Vector2Int, TerrainChunk> activeChunks = new();

    private readonly int chunkSize;
    private readonly float noiseScale;
    private readonly int maxElevationStepsCount;

    public TerrainDataMap(TerrainChunksGenerator generator)
    {
        chunkSize = generator.chunkSize;
        noiseScale = generator.noiseScale;
        maxElevationStepsCount = generator.maxElevationStepsCount;
    }

    // --------------------------------------------------------------------------------------------
    // -------------------------------------- RAW DATA --------------------------------------------
    // --------------------------------------------------------------------------------------------

    public bool HasTileData(Vector2Int coord) => tileMap.ContainsKey(coord);

    public bool TryGetTileData(Vector2Int coord, out TileMeshStruct[,] grid) =>
        tileMap.TryGetValue(coord, out grid);

    public void RemoveTileData(Vector2Int coord) => tileMap.Remove(coord);

    public void GenerateRawData(Vector2Int coord)
    {
        if (tileMap.ContainsKey(coord))
            return;

        TileMeshStruct[,] data = new TileMeshStruct[chunkSize, chunkSize];
        int ox = coord.x * chunkSize;
        int oz = coord.y * chunkSize;

        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                float n = Mathf.PerlinNoise((ox + x) * noiseScale, (oz + z) * noiseScale);
                int e = Mathf.Clamp(
                    Mathf.FloorToInt(n * (maxElevationStepsCount + 1)),
                    0,
                    maxElevationStepsCount
                );
                data[x, z] = new TileMeshStruct(x, z, e);
            }
        }
        tileMap.Add(coord, data);
    }

    // --------------------------------------------------------------------------------------------
    // -------------------------------------- SANITIZATION ----------------------------------------
    // --------------------------------------------------------------------------------------------

    public bool IsSanitized(Vector2Int coord) => sanitizedSet.Contains(coord);

    public void MarkSanitized(Vector2Int coord) => sanitizedSet.Add(coord);

    public void RemoveSanitization(Vector2Int coord) => sanitizedSet.Remove(coord);

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
            return;

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
                        Clamp(ref current, ref currentData[x + 1, z]);
                    else if (eastData != null)
                        Clamp(ref current, ref eastData[0, z]);

                    // North check
                    if (z < edge)
                        Clamp(ref current, ref currentData[x, z + 1]);
                    else if (northData != null)
                        Clamp(ref current, ref northData[x, 0]);
                }
            }
        }
    }

    private static void Clamp(ref TileMeshStruct a, ref TileMeshStruct b)
    {
        if (Mathf.Abs(a.Elevation - b.Elevation) > 1)
        {
            b.Elevation = a.Elevation + (b.Elevation > a.Elevation ? 1 : -1);
        }
    }

    // --------------------------------------------------------------------------------------------
    // -------------------------------------- ACTIVE DATA -----------------------------------------
    // --------------------------------------------------------------------------------------------

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
        sanitizedSet.Remove(coord);
    }

    // Property to let the Generator see the keys for culling/cleanup
    public Dictionary<Vector2Int, TerrainChunk>.KeyCollection ActiveChunkKeys => activeChunks.Keys;
}
