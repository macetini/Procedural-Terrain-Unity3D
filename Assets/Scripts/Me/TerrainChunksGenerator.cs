using System.Collections.Generic;
using UnityEngine;

public class TerrainChunksGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    public int chunkSize = 20;
    public float tileSize = 1.0f;
    public float elevationStepHeight = 1.0f;
    public float noiseScale = 0.05f;
    public int maxElevation = 5;

    [Header("Infinite Settings")]
    public Transform playerCamera;
    public int viewDistanceChunks = 3;
    public int dataBuffer = 1;

    [Header("Prefabs")]
    public GameObject chunkPrefab;

    private readonly Dictionary<Vector2Int, TileMeshData[,]> masterData = new();
    private readonly Dictionary<Vector2Int, TerrainChunk> chunkDict = new();

    void Update()
    {
        UpdateVisibleChunks();
    }

    private void UpdateVisibleChunks()
    {
        // Calculate the current chunk coordinates based on camera position
        int currentChunkX = Mathf.RoundToInt(playerCamera.position.x / (chunkSize * tileSize));
        int currentChunkZ = Mathf.RoundToInt(playerCamera.position.z / (chunkSize * tileSize));

        // PASS 1: Generate and Sanitize Raw Data for all chunks in the data radius
        FirstPass(currentChunkX, currentChunkZ);
        // PASS 2: Spawn Meshes in the actual view radius
        SecondPass(currentChunkX, currentChunkZ);
        // PASS 3: Cleanup distant chunks
        ThirdPass(currentChunkX, currentChunkZ);
    }

    private void FirstPass(int currentChunkX, int currentChunkZ)
    {
        // PASS 1A: Generate raw data for all chunks in the view radius
        int dataRadius = viewDistanceChunks + dataBuffer;
        for (int x = -dataRadius; x <= dataRadius; x++)
        {
            for (int z = -dataRadius; z <= dataRadius; z++)
            {
                Vector2Int coord = new(currentChunkX + x, currentChunkZ + z);
                if (!masterData.ContainsKey(coord))
                {
                    // Just generate raw noise, don't sanitize yet
                    masterData.Add(coord, GenerateRawData(coord));
                }
            }
        }
        // PASS 1B: Now that all raw data is guaranteed to exist, sanitize
        for (int x = -dataRadius; x <= dataRadius; x++)
        {
            for (int z = -dataRadius; z <= dataRadius; z++)
            {
                Vector2Int coord = new(currentChunkX + x, currentChunkZ + z);
                // We only need to sanitize if the mesh hasn't been built yet
                if (!chunkDict.ContainsKey(coord))
                {
                    SanitizeGlobalChunk(coord);
                }
            }
        }
    }

    private void SecondPass(int currentChunkX, int currentChunkZ)
    {
        for (int x = -viewDistanceChunks; x <= viewDistanceChunks; x++)
        {
            for (int z = -viewDistanceChunks; z <= viewDistanceChunks; z++)
            {
                Vector2Int coord = new(currentChunkX + x, currentChunkZ + z);

                if (!chunkDict.ContainsKey(coord))
                {
                    // By now, we GUARANTEE that coord and all its neighbors
                    // are already sanitized in Pass 1.
                    SpawnChunkMesh(coord);
                }
            }
        }
    }

    private void ThirdPass(int currentChunkX, int currentChunkZ)
    {
        List<Vector2Int> chunksToRemove = new();
        foreach (var chunkEntry in chunkDict)
        {
            if (
                Vector3.Distance(playerCamera.position, chunkEntry.Value.transform.position)
                > (viewDistanceChunks + 2) * chunkSize * tileSize
            )
            {
                chunksToRemove.Add(chunkEntry.Key);
            }
        }

        foreach (var coord in chunksToRemove)
        {
            Destroy(chunkDict[coord].gameObject);
            chunkDict.Remove(coord);
        }
    }

    private void SanitizeGlobalChunk(Vector2Int coord)
    {
        TileMeshData[,] data = masterData[coord];
        int startX = coord.x * chunkSize;
        int startZ = coord.y * chunkSize;

        // Run 2-3 passes to smooth the cliffs
        for (int i = 0; i < 2; i++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    TileMeshData current = data[x, z];
                    // Check North and East neighbors via Global Lookup
                    TileMeshData east = GetTileAt(startX + x + 1, startZ + z);
                    TileMeshData north = GetTileAt(startX + x, startZ + z + 1);

                    if (east != null)
                        ClampNeighbor(current, east);
                    if (north != null)
                        ClampNeighbor(current, north);
                }
            }
        }
    }

    private static void ClampNeighbor(TileMeshData a, TileMeshData b)
    {
        if (Mathf.Abs(a.Elevation - b.Elevation) > 1)
        {
            b.Elevation = a.Elevation + (b.Elevation > a.Elevation ? 1 : -1);
        }
    }

    private TileMeshData[,] GenerateRawData(Vector2Int coord)
    {
        TileMeshData[,] data = new TileMeshData[chunkSize, chunkSize];
        int offsetX = coord.x * chunkSize;
        int offsetZ = coord.y * chunkSize;

        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                float noise = Mathf.PerlinNoise(
                    (offsetX + x) * noiseScale,
                    (offsetZ + z) * noiseScale
                );
                int elevation = Mathf.FloorToInt(noise * (maxElevation + 1));
                elevation = Mathf.Clamp(elevation, 0, maxElevation);
                data[x, z] = new TileMeshData(x, z, elevation);
            }
        }
        return data;
    }

    private void SpawnChunkMesh(Vector2Int coord)
    {
        Vector3 pos = new(coord.x * chunkSize * tileSize, 0, coord.y * chunkSize * tileSize);
        GameObject go = Instantiate(chunkPrefab, pos, Quaternion.identity, transform);

        TerrainChunk chunk = go.GetComponent<TerrainChunk>();

        // Pass the Generator reference so the chunk can "Look up" neighbor data
        chunk.Build(this, coord);

        chunkDict.Add(coord, chunk);
        go.name = $"Chunk_{coord.x}_{coord.y}";
    }

    // Global Lookup Function for Chunks
    public TileMeshData GetTileAt(int globalX, int globalZ)
    {
        int cx = Mathf.FloorToInt((float)globalX / chunkSize);
        int cz = Mathf.FloorToInt((float)globalZ / chunkSize);

        // Local index inside the chunk array
        int lx = globalX - (cx * chunkSize);
        int lz = globalZ - (cz * chunkSize);

        if (masterData.TryGetValue(new Vector2Int(cx, cz), out TileMeshData[,] grid))
        {
            return grid[lx, lz];
        }
        return null;
    }
}
