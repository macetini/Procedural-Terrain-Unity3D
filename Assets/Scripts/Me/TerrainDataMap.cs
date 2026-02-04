using System.Collections.Generic;
using UnityEngine;

public class TerrainDataMap
{
    private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap = new();
    private readonly HashSet<Vector2Int> sanitizedSet = new();

    //private readonly Dictionary<Vector2Int, TerrainChunk> activeChunks = new();

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
    // -------------------------------------- DATA ----------------------------------------
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

    // -----------------------------------------------------------------------------------

    /*

    public void PurgeCoordinate(Vector2Int coord)
    {
        UnregisterChunk(coord);
        RemoveTile(coord);
    }

    // -----------------------------------------------------------------------------------

    public bool TryGetTileData(Vector2Int coord, out TileMeshStruct[,] data)
    {
        return tileMap.TryGetValue(coord, out data);
    }

    public void RemoveTile(Vector2Int coord)
    {
        tileMap.Remove(coord);
        sanitizedTileCoords.Remove(coord);
    }

    public bool HasTileData(Vector2Int coord) => tileMap.ContainsKey(coord);

    public bool IsChunkSanitized(Vector2Int coord) => sanitizedTileCoords.Contains(coord);

    // -----------------------------------------------------------------------------------

    public bool HasActiveChunk(Vector2Int coord) => activeChunks.ContainsKey(coord);

    public Dictionary<Vector2Int, TerrainChunk>.KeyCollection GetActiveChunksKeys =>
        activeChunks.Keys;

    public bool TryGetActiveChunk(Vector2Int coord, out TerrainChunk chunk)
    {
        return activeChunks.TryGetValue(coord, out chunk);
    }

    public void RegisterChunk(Vector2Int coord, TerrainChunk chunk)
    {
        activeChunks.Add(coord, chunk);
    }

    public void UnregisterChunk(Vector2Int coord)
    {
        activeChunks.Remove(coord);
    }

    public void GenerateFullMeshData(Vector2Int cameraOrigin, int dataRadius)
    {
        // If radius is 0, this only runs once for the cameraOrigin.
        // If radius is 1, it runs 9 times.
        for (int xChunkOffset = -dataRadius; xChunkOffset <= dataRadius; xChunkOffset++)
        {
            for (int zChunkOffset = -dataRadius; zChunkOffset <= dataRadius; zChunkOffset++)
            {
                Vector2Int coord = new(
                    cameraOrigin.x + xChunkOffset,
                    cameraOrigin.y + zChunkOffset
                );
                if (!tileMap.ContainsKey(coord))
                {
                    TileMeshStruct[,] rawData = GenerateRawTileMeshData(coord);
                    //tileMap.Add(coord, rawData);

                    //AddRawTileData(coord, GenerateRawTileMeshData(coord));
                    tileMap.Add(coord, rawData);
                }
            }
        }
    }

    private TileMeshStruct[,] GenerateRawTileMeshData(Vector2Int tileOrigin)
    {
        TileMeshStruct[,] tileData = new TileMeshStruct[chunkSize, chunkSize];
        int offsetX = tileOrigin.x * chunkSize;
        int offsetZ = tileOrigin.y * chunkSize;

        for (int xTileOffset = 0; xTileOffset < chunkSize; xTileOffset++)
        {
            for (int zTileOffset = 0; zTileOffset < chunkSize; zTileOffset++)
            {
                int tileX = offsetX + xTileOffset;
                int tileZ = offsetZ + zTileOffset;
                float noise = Mathf.PerlinNoise(tileX * noiseScale, tileZ * noiseScale);

                int elevation = Mathf.FloorToInt(noise * (maxElevationStepsCount + 1));
                elevation = Mathf.Clamp(elevation, 0, maxElevationStepsCount);

                tileData[xTileOffset, zTileOffset] = new TileMeshStruct(
                    xTileOffset,
                    zTileOffset,
                    elevation
                );
            }
        }
        return tileData;
    }

    public void SanitizeCurrentTileMeshData(Vector2Int cameraOrigin, int dataRadius)
    {
        for (int x = -dataRadius; x <= dataRadius; x++)
        {
            for (int z = -dataRadius; z <= dataRadius; z++)
            {
                Vector2Int tilePosition = new(cameraOrigin.x + x, cameraOrigin.y + z);
                // We only need to sanitize if the mesh hasn't been built yet
                //if (!sanitizedChunksHash.Contains(tilePosition))
                if (!sanitizedTileCoords.Contains(tilePosition))
                {
                    SanitizeGlobalChunk(tilePosition);
                    //sanitizedChunksHash.Add(tilePosition);
                    //sanitizedSet.Add(tilePosition);
                }
            }
        }
    }

    public void SanitizeGlobalChunk(Vector2Int tilePosition)
    {
        if (!tileMap.TryGetValue(tilePosition, out TileMeshStruct[,] currentData))
            return;

        tileMap.TryGetValue(tilePosition + Vector2Int.right, out TileMeshStruct[,] eastData);
        tileMap.TryGetValue(tilePosition + Vector2Int.up, out TileMeshStruct[,] northData);

        int size = chunkSize;
        int edge = size - 1;

        for (int i = 0; i < 2; i++)
        {
            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    ref TileMeshStruct current = ref currentData[x, z];

                    // --- EAST CHECK ---
                    if (x < edge)
                    {
                        // Internal neighbor
                        ClampNeighbor(ref current, ref currentData[x + 1, z]);
                    }
                    else if (eastData != null)
                    {
                        // Chunk boundary
                        ClampNeighbor(ref current, ref eastData[0, z]);
                    }

                    // --- NORTH CHECK ---
                    if (z < edge)
                    {
                        // Internal neighbor
                        ClampNeighbor(ref current, ref currentData[x, z + 1]);
                    }
                    else if (northData != null)
                    {
                        // Chunk boundary
                        ClampNeighbor(ref current, ref northData[x, 0]);
                    }
                }
            }
        }

        sanitizedTileCoords.Add(tilePosition);
    }

    private static void ClampNeighbor(ref TileMeshStruct a, ref TileMeshStruct b)
    {
        if (Mathf.Abs(a.Elevation - b.Elevation) > 1)
        {
            b.Elevation = a.Elevation + (b.Elevation > a.Elevation ? 1 : -1);
        }
    }

    public void GetNeighborGrids(
        Vector2Int coord,
        out TileMeshStruct[,] c,
        out TileMeshStruct[,] w,
        out TileMeshStruct[,] e,
        out TileMeshStruct[,] n,
        out TileMeshStruct[,] s,
        out TileMeshStruct[,] nw,
        out TileMeshStruct[,] ne,
        out TileMeshStruct[,] sw,
        out TileMeshStruct[,] se
    )
    {
        tileMap.TryGetValue(coord, out c);
        tileMap.TryGetValue(coord + Vector2Int.left, out w);
        tileMap.TryGetValue(coord + Vector2Int.right, out e);
        tileMap.TryGetValue(coord + Vector2Int.up, out n);
        tileMap.TryGetValue(coord + Vector2Int.down, out s);
        tileMap.TryGetValue(coord + new Vector2Int(-1, 1), out nw);
        tileMap.TryGetValue(coord + new Vector2Int(1, 1), out ne);
        tileMap.TryGetValue(coord + new Vector2Int(-1, -1), out sw);
        tileMap.TryGetValue(coord + new Vector2Int(1, -1), out se);
    }

    */
}
