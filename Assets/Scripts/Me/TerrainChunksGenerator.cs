using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainChunksGenerator : MonoBehaviour
{
    //private const float MOVE_THRESHOLD = 1f;
    private static readonly WaitForSeconds WaitForSeconds0_2 = new(0.2f);

    [Header("Generation Settings")]
    public int chunkSize = 16;
    public float tileSize = 1.0f;
    public float elevationStepHeight = 1.0f;
    public int maxElevation = 5;
    public float noiseScale = 0.05f;

    [Header("Infinite Settings")]
    public Camera cameraReference;
    public int viewDistanceChunks = 3;

    [Header("Build Settings")]
    public int initialBuildsPerFrame = 5;
    public int buildsPerFrame = 1; // Start with 1 to ensure 60fps on WebGL

    [Header("LOD Settings")]
    public float lodDist1 = 50f; // Distance to switch to Medium detail
    public float lodDist2 = 100f; // Distance to switch to Low detail

    [Header("Prefabs")]
    public TerrainChunk chunkPrefab;

    private Vector2Int currentCameraPosition = Vector2Int.zero;

    private readonly Dictionary<Vector2Int, TileMeshStruct[,]> fullTileMeshData = new();
    private readonly Dictionary<Vector2Int, TerrainChunk> chunksDict = new();
    private readonly HashSet<Vector2Int> sanitizedChunksHash = new();
    private readonly List<Vector2Int> buildQueue = new();
    private bool isProcessingQueue = false;

    private Plane[] cameraPlanes;

    void Start()
    {
        UpdateCurrentCameraPosition();
        FirstPass();
        SecondPass();
        StartCoroutine(VisibilityCheckRoutine());
    }

    private void UpdateCurrentCameraPosition()
    {
        int currentX = Mathf.FloorToInt(
            cameraReference.transform.position.x / (chunkSize * tileSize)
        );
        int currentZ = Mathf.FloorToInt(
            cameraReference.transform.position.z / (chunkSize * tileSize)
        );
        currentCameraPosition = new Vector2Int(currentX, currentZ);
    }

    void Update()
    {
        UpdateCurrentCameraPosition();
    }

    private void FirstPass()
    {
        // Calculate the current chunk coordinates based on camera position
        // Use FloorToInt to get a consistent "Bottom-Left" anchor

        System.Diagnostics.Stopwatch sw0 = new();
        System.Diagnostics.Stopwatch sw1 = new();
        System.Diagnostics.Stopwatch sw2 = new();
        double ms;

        sw0.Start();

        sw1.Start();
        int dataRadius = viewDistanceChunks + 1;
        GenerateFullMeshData(currentCameraPosition, dataRadius);
        sw1.Stop();
        ms = sw1.Elapsed.TotalMilliseconds;
        if (ms > 1.0f)
        {
            Debug.Log($"<color=orange>'GenerateFullMeshData()' Execution Time: {ms:F2} ms</color>");
        }

        sw2.Start();
        SanitizeCurrentTileMeshData(currentCameraPosition, dataRadius);
        sw2.Stop();
        ms = sw2.Elapsed.TotalMilliseconds;
        if (ms > 1.0f)
        {
            Debug.Log(
                $"<color=orange>'SanitizeCurrentTileMeshData()' Execution Time: {ms:F2} ms</color>"
            );
        }

        sw0.Stop();
        ms = sw0.Elapsed.TotalMilliseconds;
        if (ms > 1.0f)
        {
            Debug.Log($"<color=orange>Total Execution Time: {ms:F2} ms</color>");
        }
    }

    private void GenerateFullMeshData(Vector2Int cameraOrigin, int dataRadius)
    {
        Debug.Log("[TerrainChunksGenerator] Generating Raw Mesh Data.");
        for (int xChunkOffset = -dataRadius; xChunkOffset <= dataRadius; xChunkOffset++)
        {
            for (int zChunkOffset = -dataRadius; zChunkOffset <= dataRadius; zChunkOffset++)
            {
                Vector2Int coord = new(
                    cameraOrigin.x + xChunkOffset,
                    cameraOrigin.y + zChunkOffset
                );
                if (!fullTileMeshData.ContainsKey(coord))
                {
                    TileMeshStruct[,] rawData = GenerateRawTileMeshData(coord);
                    fullTileMeshData.Add(coord, rawData);
                }
            }
        }
        Debug.Log(
            $"[TerrainChunksGenerator] Raw Mesh Data Generation Complete. Chunks generated: {fullTileMeshData.Count}.  "
        );
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

                int elevation = Mathf.FloorToInt(noise * (maxElevation + 1));
                elevation = Mathf.Clamp(elevation, 0, maxElevation);

                tileData[xTileOffset, zTileOffset] = new TileMeshStruct(
                    xTileOffset,
                    zTileOffset,
                    elevation
                );
            }
        }
        return tileData;
    }

    private void SanitizeCurrentTileMeshData(Vector2Int cameraOrigin, int dataRadius)
    {
        Debug.Log("[TerrainChunksGenerator] Sanitizing Raw Tile Mesh Data.");
        for (int x = -dataRadius; x <= dataRadius; x++)
        {
            for (int z = -dataRadius; z <= dataRadius; z++)
            {
                Vector2Int tilePos = new(cameraOrigin.x + x, cameraOrigin.y + z);
                // We only need to sanitize if the mesh hasn't been built yet
                if (!sanitizedChunksHash.Contains(tilePos))
                {
                    SanitizeGlobalChunk(tilePos);
                    sanitizedChunksHash.Add(tilePos);
                }
            }
        }
        Debug.Log("[TerrainChunksGenerator] Sanitization Complete.");
    }

    private void SanitizeGlobalChunk(Vector2Int tilePos)
    {
        if (!fullTileMeshData.TryGetValue(tilePos, out TileMeshStruct[,] currentData))
            return;

        fullTileMeshData.TryGetValue(tilePos + Vector2Int.right, out TileMeshStruct[,] eastData);
        fullTileMeshData.TryGetValue(tilePos + Vector2Int.up, out TileMeshStruct[,] northData);

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
    }

    private static void ClampNeighbor(ref TileMeshStruct a, ref TileMeshStruct b)
    {
        if (Mathf.Abs(a.Elevation - b.Elevation) > 1)
        {
            b.Elevation = a.Elevation + (b.Elevation > a.Elevation ? 1 : -1);
        }
    }

    private void SecondPass()
    {
        bool addedNew = false;

        for (int x = -viewDistanceChunks; x <= viewDistanceChunks; x++)
        {
            for (int z = -viewDistanceChunks; z <= viewDistanceChunks; z++)
            {
                Vector2Int coord = new(currentCameraPosition.x + x, currentCameraPosition.y + z);
                if (!chunksDict.ContainsKey(coord) && !buildQueue.Contains(coord))
                {
                    buildQueue.Add(coord);
                    addedNew = true;
                }
                else if (chunksDict.ContainsKey(coord))
                {
                    chunksDict[coord].UpdateLOD();
                }
            }
        }
        // Sort the list so closest chunks are at the front
        if (addedNew)
        {
            SortBuildQueue();
        }
        if (!isProcessingQueue && buildQueue.Count > 0)
        {
            StartCoroutine(ProcessBuildQueue());
        }
    }

    private IEnumerator ProcessBuildQueue()
    {
        isProcessingQueue = true;
        int buildsCount = initialBuildsPerFrame;

        while (buildQueue.Count > 0)
        {
            for (int i = 0; i < buildsCount && buildQueue.Count > 0; i++)
            {
                // Always take the first item (the closest one)
                Vector2Int coord = buildQueue[0];
                buildQueue.RemoveAt(0);

                if (!chunksDict.ContainsKey(coord))
                {
                    SpawnChunkMesh(coord);
                }
            }
            buildsCount = buildsPerFrame;
            yield return null;
        }
        isProcessingQueue = false;
    }

    private void SpawnChunkMesh(Vector2Int coord)
    {
        float chunkBoundSize = chunkSize * tileSize;
        Vector3 position = new(coord.x * chunkBoundSize, 0, coord.y * chunkBoundSize);

        TerrainChunk chunk = Instantiate(chunkPrefab, position, Quaternion.identity, transform);

        // 1. Setup variables (No Build yet)
        chunk.InitBuild(this, coord);

        // 2. Immediate Frustum Check
        cameraPlanes ??= GeometryUtility.CalculateFrustumPlanes(cameraReference);

        chunk.UpdateVisibility(cameraPlanes);

        // 3. ONLY build the mesh if it's actually on screen
        // If not, it stays as an empty object until VisibilityCheckRoutine finds it
        if (chunk.IsVisible)
        {
            chunk.UpdateLOD(true);
        }

        chunksDict.Add(coord, chunk);
    }

    public bool GetTileAt(int globalX, int globalZ, out TileMeshStruct tile)
    {
        int cx = Mathf.FloorToInt((float)globalX / chunkSize);
        int cz = Mathf.FloorToInt((float)globalZ / chunkSize);

        int lx = globalX - (cx * chunkSize);
        int lz = globalZ - (cz * chunkSize);

        if (fullTileMeshData.TryGetValue(new Vector2Int(cx, cz), out TileMeshStruct[,] grid))
        {
            tile = grid[lx, lz];
            return true;
        }

        // If no tile exists, we return a "blank" one and say 'false'
        tile = default;
        return false;
    }

    private void SortBuildQueue()
    {
        Vector3 camPos = cameraReference.transform.position;

        buildQueue.Sort(
            (a, b) =>
            {
                // Calculate world positions for both coordinates
                Vector3 posA = new Vector3(
                    a.x * chunkSize * tileSize,
                    0,
                    a.y * chunkSize * tileSize
                );
                Vector3 posB = new Vector3(
                    b.x * chunkSize * tileSize,
                    0,
                    b.y * chunkSize * tileSize
                );

                float distA = Vector3.SqrMagnitude(camPos - posA);
                float distB = Vector3.SqrMagnitude(camPos - posB);

                // Sort Ascending (Smallest distance first)
                return distA.CompareTo(distB);
            }
        );
    }

    private IEnumerator VisibilityCheckRoutine()
    {
        while (true)
        {
            cameraPlanes = GeometryUtility.CalculateFrustumPlanes(cameraReference);
            foreach (var chunk in chunksDict.Values)
            {
                chunk.UpdateVisibility(cameraPlanes);

                // If it just became visible and was never built (currentStep is -1)
                if (chunk.IsVisible && chunk.CurrentStep < 0)
                {
                    chunk.UpdateLOD(true);
                }
            }
            yield return WaitForSeconds0_2;
        }
    }
}
