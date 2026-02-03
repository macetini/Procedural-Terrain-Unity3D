using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainChunksGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    public int chunkSize = 16;
    public float tileSize = 1.0f;
    public float elevationStepHeight = 1.0f;
    public int maxElevationStep = 5;
    public float noiseScale = 0.05f;

    [Header("Infinite Settings")]
    public Camera cameraReference;
    public int viewDistanceChunks = 3;

    [Header("Build Settings")]
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
    private float ChunkBoundSize => chunkSize * tileSize;

    private Vector2Int lastLookupCoord = new(-9999, -9999);
    private TileMeshStruct[,] lastLookupGrid;

    private readonly Dictionary<int, int[]> triangleCache = new();

    private readonly List<Vector2Int> visibilityKeysSnapshot = new();

    void Start()
    {
        UpdateCurrentCameraPosition();
        FirstPass();
        SecondPass();

        StartCoroutine(WorldMonitoringRoutine()); // The manager
        StartCoroutine(ProcessBuildQueue()); // The builder
        StartCoroutine(VisibilityCheckRoutine()); // The culler
    }

    void Update()
    {
        UpdateCurrentCameraPosition();
    }

    private void UpdateCurrentCameraPosition()
    {
        int currentX = Mathf.FloorToInt(cameraReference.transform.position.x / ChunkBoundSize);
        int currentZ = Mathf.FloorToInt(cameraReference.transform.position.z / ChunkBoundSize);
        currentCameraPosition = new Vector2Int(currentX, currentZ);
    }

    private IEnumerator WorldMonitoringRoutine()
    {
        Vector2Int lastProcessedPos = new Vector2Int(-9999, -9999);

        while (true)
        {
            if (currentCameraPosition != lastProcessedPos)
            {
                lastProcessedPos = currentCameraPosition;

                // Instead of one big loop, we yield every "row" of chunks
                for (int x = -viewDistanceChunks; x <= viewDistanceChunks; x++)
                {
                    for (int z = -viewDistanceChunks; z <= viewDistanceChunks; z++)
                    {
                        Vector2Int coord = currentCameraPosition + new Vector2Int(x, z);

                        if (!chunksDict.ContainsKey(coord) && !buildQueue.Contains(coord))
                        {
                            buildQueue.Add(coord);
                        }
                        else if (chunksDict.TryGetValue(coord, out TerrainChunk chunk))
                        {
                            chunk.UpdateLOD();
                        }
                    }
                    // Yield after every 'X' column to keep framerate perfect
                    yield return null;
                }

                SortBuildQueue();

                if (!isProcessingQueue && buildQueue.Count > 0)
                {
                    StartCoroutine(ProcessBuildQueue());
                }
            }
            yield return null;
        }
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
                if (!fullTileMeshData.ContainsKey(coord))
                {
                    TileMeshStruct[,] rawData = GenerateRawTileMeshData(coord);
                    fullTileMeshData.Add(coord, rawData);
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

                int elevation = Mathf.FloorToInt(noise * (maxElevationStep + 1));
                elevation = Mathf.Clamp(elevation, 0, maxElevationStep);

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
        //Debug.Log("[TerrainChunksGenerator] Sanitizing Raw Tile Mesh Data.");
        for (int x = -dataRadius; x <= dataRadius; x++)
        {
            for (int z = -dataRadius; z <= dataRadius; z++)
            {
                Vector2Int tilePosition = new(cameraOrigin.x + x, cameraOrigin.y + z);
                // We only need to sanitize if the mesh hasn't been built yet
                if (!sanitizedChunksHash.Contains(tilePosition))
                {
                    SanitizeGlobalChunk(tilePosition);
                    sanitizedChunksHash.Add(tilePosition);
                }
            }
        }
        //Debug.Log("[TerrainChunksGenerator] Sanitization Complete.");
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

        while (buildQueue.Count > 0)
        {
            Vector2Int coord = buildQueue[0];
            buildQueue.RemoveAt(0);

            // --- STALE CHECK ---
            Vector3 chunkPos = new Vector3(coord.x * ChunkBoundSize, 0, coord.y * ChunkBoundSize);
            float distSqr = (chunkPos - cameraReference.transform.position).sqrMagnitude;
            float maxDistSqr = Mathf.Pow((viewDistanceChunks + 2) * ChunkBoundSize, 2);

            if (distSqr > maxDistSqr)
            {
                continue; // Skip this chunk, it's too far away now!
            }

            // [Keep your Stale/Distance Check here]
            if (!chunksDict.ContainsKey(coord))
            {
                // --- JIT (Just In Time) DATA GENERATION ---
                // If data doesn't exist, generate it now.
                // --- SURGICAL JIT DATA GENERATION ---
                // We need the center AND its immediate North/East neighbors for sanitization
                for (int x = 0; x <= 1; x++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector2Int neighborCoord = coord + new Vector2Int(x, z);
                        if (!fullTileMeshData.ContainsKey(neighborCoord))
                        {
                            // We use radius 0 to generate ONLY this specific neighbor
                            GenerateFullMeshData(neighborCoord, 0);
                        }
                    }
                }
                // --- JIT SANITIZATION ---
                if (!sanitizedChunksHash.Contains(coord))
                {
                    SanitizeGlobalChunk(coord);
                    sanitizedChunksHash.Add(coord);
                }

                // Now spawn. If the player is moving fast, they see empty space
                // until this specific 0.1s tick finishes.
                SpawnChunkMesh(coord);

                if (chunksDict.TryGetValue(coord, out TerrainChunk chunk))
                {
                    chunk.StartFadeIn();
                }
            }

            yield return null;
        }
        isProcessingQueue = false;
    }

    private void SpawnChunkMesh(Vector2Int coord)
    {
        Vector3 position = new(coord.x * ChunkBoundSize, 0, coord.y * ChunkBoundSize);
        TerrainChunk chunk = Instantiate(chunkPrefab, position, Quaternion.identity, transform);

        // This sets up variables
        chunk.InitBuild(this, coord);

        // Force a frustum check right now
        cameraPlanes = GeometryUtility.CalculateFrustumPlanes(cameraReference);
        chunk.UpdateVisibility(cameraPlanes);

        chunksDict.Add(coord, chunk);
    }

    public bool GetTileAt(int globalX, int globalZ, out TileMeshStruct tile)
    {
        int cx = Mathf.FloorToInt((float)globalX / chunkSize);
        int cz = Mathf.FloorToInt((float)globalZ / chunkSize);
        int lx = globalX - (cx * chunkSize);
        int lz = globalZ - (cz * chunkSize);

        Vector2Int lookup = new Vector2Int(cx, cz);

        // Check cache first
        if (lookup == lastLookupCoord && lastLookupGrid != null)
        {
            tile = lastLookupGrid[lx, lz];
            return true;
        }

        if (fullTileMeshData.TryGetValue(lookup, out TileMeshStruct[,] grid))
        {
            lastLookupCoord = lookup;
            lastLookupGrid = grid;
            tile = grid[lx, lz];
            return true;
        }

        tile = default;
        return false;
    }

    private void SortBuildQueue()
    {
        if (buildQueue.Count <= 1)
            return;

        // Capture camera pos in chunk-coordinates once to avoid repeated math
        // We use a local variable to avoid thread/sync issues during the sort
        Vector2Int camCoord = currentCameraPosition;

        buildQueue.Sort(
            (a, b) =>
            {
                // Use "Manhattan Distance" or squared coordinate distance
                // Manhattan: abs(x1-x2) + abs(y1-y2) is even faster than squaring
                int distA = Mathf.Abs(a.x - camCoord.x) + Mathf.Abs(a.y - camCoord.y);
                int distB = Mathf.Abs(b.x - camCoord.x) + Mathf.Abs(b.y - camCoord.y);

                return distA.CompareTo(distB);
            }
        );
    }

    private IEnumerator VisibilityCheckRoutine()
    {
        while (true)
        {
            cameraPlanes = GeometryUtility.CalculateFrustumPlanes(cameraReference);

            // 1. Take a snapshot of the current keys
            visibilityKeysSnapshot.Clear();
            visibilityKeysSnapshot.AddRange(chunksDict.Keys);

            // 2. Iterate through the snapshot
            for (int i = 0; i < visibilityKeysSnapshot.Count; i++)
            {
                Vector2Int key = visibilityKeysSnapshot[i];

                // 3. Safety Check: Make sure the chunk wasn't purged while we were yielding
                if (chunksDict.TryGetValue(key, out TerrainChunk chunk))
                {
                    chunk.UpdateVisibility(cameraPlanes);

                    if (chunk.IsVisible && chunk.CurrentStep < 0)
                    {
                        chunk.UpdateLOD(true);
                    }
                }

                // 4. Time Slicing: Only process 10 chunks per frame
                if (i % 10 == 0)
                    yield return null;
            }

            // Short rest before the next full world sweep
            yield return null;
        }
    }

    private void UpdateVisibleChunks()
    {
        for (int x = -viewDistanceChunks; x <= viewDistanceChunks; x++)
        {
            for (int z = -viewDistanceChunks; z <= viewDistanceChunks; z++)
            {
                Vector2Int coord = currentCameraPosition + new Vector2Int(x, z);

                // If we don't even have the Raw Data yet, just mark the coord for building.
                // We won't generate data here; we'll do it in the rhythm of the Coroutine.
                if (!chunksDict.ContainsKey(coord) && !buildQueue.Contains(coord))
                {
                    buildQueue.Add(coord);
                }
                else if (chunksDict.TryGetValue(coord, out TerrainChunk chunk))
                {
                    chunk.UpdateLOD();
                }
            }
        }
    }

    public int[] GetPrecalculatedTriangles(int resolution)
    {
        if (triangleCache.TryGetValue(resolution, out int[] cachedTris))
        {
            return cachedTris;
        }

        // If not in cache, calculate it once
        int[] newTris = GenerateTriangleIndices(resolution);
        triangleCache.Add(resolution, newTris);
        return newTris;
    }

    private int[] GenerateTriangleIndices(int resolution)
    {
        int gridTris = (resolution - 1) * (resolution - 1) * 6;
        int skirtTris = (resolution - 1) * 4 * 6;
        int[] tris = new int[gridTris + skirtTris];
        int t = 0;

        // 1. Grid
        for (int x = 0; x < resolution - 1; x++)
        {
            for (int z = 0; z < resolution - 1; z++)
            {
                int bl = x * resolution + z;
                int tl = bl + 1;
                int br = (x + 1) * resolution + z;
                int tr = br + 1;
                tris[t++] = bl;
                tris[t++] = tl;
                tris[t++] = br;
                tris[t++] = tl;
                tris[t++] = tr;
                tris[t++] = br;
            }
        }

        // 2. Skirts (Correctly offset)
        int gridCount = resolution * resolution;
        int sStart = gridCount;
        int nStart = gridCount + resolution;
        int wStart = gridCount + resolution * 2;
        int eStart = gridCount + resolution * 3;

        for (int j = 0; j < resolution - 1; j++)
        {
            // South
            int gL = j * resolution;
            int gR = (j + 1) * resolution;
            int sL = sStart + j;
            int sR = sStart + j + 1;
            tris[t++] = gL;
            tris[t++] = sR;
            tris[t++] = gR;
            tris[t++] = gL;
            tris[t++] = sL;
            tris[t++] = sR;

            // North
            int ngL = j * resolution + (resolution - 1);
            int ngR = (j + 1) * resolution + (resolution - 1);
            int nsL = nStart + j;
            int nsR = nStart + j + 1;
            tris[t++] = ngL;
            tris[t++] = ngR;
            tris[t++] = nsR;
            tris[t++] = ngL;
            tris[t++] = nsR;
            tris[t++] = nsL;

            // West
            int wgB = j;
            int wgT = j + 1;
            int wsB = wStart + j;
            int wsT = wStart + j + 1;
            tris[t++] = wgB;
            tris[t++] = wsT;
            tris[t++] = wgT;
            tris[t++] = wgB;
            tris[t++] = wsB;
            tris[t++] = wsT;

            // East
            int egB = (resolution - 1) * resolution + j;
            int egT = (resolution - 1) * resolution + j + 1;
            int esB = eStart + j;
            int esT = eStart + j + 1;
            tris[t++] = egB;
            tris[t++] = egT;
            tris[t++] = esT;
            tris[t++] = egB;
            tris[t++] = esT;
            tris[t++] = esB;
        }
        return tris;
    }

    public float GetElevationAt(int gx, int gz)
    {
        // 1. Determine which chunk these global coordinates belong to
        int cx = Mathf.FloorToInt((float)gx / chunkSize);
        int cz = Mathf.FloorToInt((float)gz / chunkSize);

        // 2. Determine the local index within that chunk [0-15]
        int lx = gx - (cx * chunkSize);
        int lz = gz - (cz * chunkSize);

        Vector2Int lookupCoord = new Vector2Int(cx, cz);

        // 3. CACHE CHECK (The Performance Win)
        // If we are asking for a tile in the same chunk as the last call,
        // skip the Dictionary entirely and return from the local reference.
        if (lookupCoord == lastLookupCoord && lastLookupGrid != null)
        {
            return lastLookupGrid[lx, lz].Elevation;
        }

        // 4. DICTIONARY LOOKUP (The Fallback)
        if (fullTileMeshData.TryGetValue(lookupCoord, out TileMeshStruct[,] grid))
        {
            lastLookupCoord = lookupCoord;
            lastLookupGrid = grid;
            return grid[lx, lz].Elevation;
        }

        // Return 0 if data isn't generated yet (prevents crashes)
        return 0f;
    }
}
