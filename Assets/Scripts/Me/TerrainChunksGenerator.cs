using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainChunksGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    public int chunkSize = 16;
    public float tileSize = 1.0f;
    public float elevationStepHeight = 1.0f;
    public int maxElevationStepsCount = 5;
    public float noiseScale = 0.05f;

    [Header("Build Settings")]
    public Camera cameraReference;
    public int viewDistanceChunks = 3;

    [Header("LOD Settings")]
    public float lodDist1 = 640f; // Distance to switch to Medium detail
    public float lodDist2 = 768f; // Distance to switch to Low detail

    [Header("Prefabs")]
    public TerrainChunk chunkPrefab;

    private Vector2Int currentCameraPosition = Vector2Int.zero;

    //private readonly Dictionary<Vector2Int, TerrainChunk> activeChunks = new();

    private readonly List<Vector2Int> buildQueue = new();
    private bool isProcessingQueue = false;
    private Plane[] cameraPlanes;
    private float ChunkBoundSize => chunkSize * tileSize;
    private Vector2Int lastLookupCoord = new(-9999, -9999);
    private TileMeshStruct[,] lastLookupGrid;
    private readonly Dictionary<int, int[]> triangleCache = new();
    private readonly List<Vector2Int> visibilityKeysSnapshot = new();

    // Refactor
    private TerrainDataMap terrainData;

    void Awake()
    {
        terrainData = new TerrainDataMap(this);
    }

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
        Vector2Int lastProcessedPos = new(-9999, -9999);
        while (true)
        {
            if (currentCameraPosition != lastProcessedPos)
            {
                lastProcessedPos = currentCameraPosition;

                CleanupRemoteChunks();

                // Instead of one big loop, we yield every "row" of chunks
                for (int x = -viewDistanceChunks; x <= viewDistanceChunks; x++)
                {
                    for (int z = -viewDistanceChunks; z <= viewDistanceChunks; z++)
                    {
                        Vector2Int coord = currentCameraPosition + new Vector2Int(x, z);

                        //if (!activeChunks.ContainsKey(coord) && !buildQueue.Contains(coord))
                        if (!terrainData.HasActiveChunk(coord) && !buildQueue.Contains(coord))
                        {
                            buildQueue.Add(coord);
                        }
                        //else if (activeChunks.TryGetValue(coord, out TerrainChunk chunk))
                        else if (terrainData.TryGetActiveChunk(coord, out TerrainChunk chunk))
                        {
                            chunk.UpdateLOD();
                        }
                    }
                    // Yield after every 'X' column to keep framerate perfect
                    yield return null; // Row-by-row time slicing
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

    private void CleanupRemoteChunks()
    {
        visibilityKeysSnapshot.Clear();
        visibilityKeysSnapshot.AddRange(terrainData.ActiveChunkKeys); //activeChunks.Keys);

        // Using a simple integer distance (Manhattan) is faster and safer for chunk grids
        int maxChunkDist = viewDistanceChunks + 2;

        foreach (var coord in visibilityKeysSnapshot)
        {
            int chunkDist =
                Mathf.Abs(coord.x - currentCameraPosition.x)
                + Mathf.Abs(coord.y - currentCameraPosition.y);

            if (chunkDist > maxChunkDist)
            {
                //if (activeChunks.TryGetValue(coord, out TerrainChunk chunk))
                if (terrainData.TryGetActiveChunk(coord, out TerrainChunk chunk))
                {
                    Destroy(chunk.gameObject);
                    //activeChunks.Remove(coord);
                    terrainData.UnregisterChunk(coord);

                    terrainData.RemoveSanitization(coord);
                    terrainData.RemoveTileData(coord);
                }
            }
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
        terrainData.SanitizeCurrentTileMeshData(currentCameraPosition, dataRadius); // (See note below)
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
                terrainData.GenerateRawData(coord);
            }
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
                //if (!activeChunks.ContainsKey(coord) && !buildQueue.Contains(coord))
                if (!terrainData.HasActiveChunk(coord) && !buildQueue.Contains(coord))
                {
                    buildQueue.Add(coord);
                    addedNew = true;
                }
                //else if (activeChunks.ContainsKey(coord))
                else if (terrainData.TryGetActiveChunk(coord, out TerrainChunk chunk))
                {
                    //activeChunks[coord].UpdateLOD();
                    chunk.UpdateLOD();
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

            // Don't build if the player already moved away (snapping to a new far away chunk)
            float distToCam = Vector2Int.Distance(coord, currentCameraPosition);
            if (distToCam > viewDistanceChunks + 1)
                continue;

            //if (!activeChunks.ContainsKey(coord))
            if (!terrainData.HasActiveChunk(coord))
            {
                // 1. Ensure a 3x3 block of RAW DATA exists
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector2Int n = coord + new Vector2Int(x, z);
                        //if (!tileMap.ContainsKey(n))
                        if (!terrainData.HasTileData(n))
                        {
                            GenerateFullMeshData(n, 0);
                            yield return null;
                        }
                    }
                }

                yield return null;

                // 2. Ensure a 3x3 block is SANITIZED (Height-Matched)
                // This is the key! We sanitize the neighbors against EACH OTHER
                // so the heights at the edges are identical.
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector2Int n = coord + new Vector2Int(x, z);
                        /*
                        if (!sanitizedTileCoords.Contains(n))
                        {
                            SanitizeGlobalChunk(n);
                            sanitizedTileCoords.Add(n);
                            yield return null;
                        }
                        */
                        if (!terrainData.IsSanitized(n))
                        {
                            terrainData.SanitizeGlobalChunk(n);
                            terrainData.MarkSanitized(n);
                            yield return null;
                        }
                    }
                }

                // 3. Now that the heights are guaranteed to match, spawn
                yield return null;
                SpawnChunkMesh(coord);

                //if (activeChunks.TryGetValue(coord, out TerrainChunk chunk))
                if (terrainData.TryGetActiveChunk(coord, out TerrainChunk chunk))
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
        chunk.UpdateVisibility(cameraPlanes);
        //activeChunks.Add(coord, chunk);
        terrainData.RegisterChunk(coord, chunk);
    }

    public bool GetTileAt(int globalX, int globalZ, out TileMeshStruct tile)
    {
        int cx = Mathf.FloorToInt((float)globalX / chunkSize);
        int cz = Mathf.FloorToInt((float)globalZ / chunkSize);
        int lx = globalX - (cx * chunkSize);
        int lz = globalZ - (cz * chunkSize);

        Vector2Int lookup = new(cx, cz);

        // Check cache first
        if (lookup == lastLookupCoord && lastLookupGrid != null)
        {
            tile = lastLookupGrid[lx, lz];
            return true;
        }

        //if (tileMap.TryGetValue(lookup, out TileMeshStruct[,] grid))
        if (terrainData.TryGetTileData(lookup, out TileMeshStruct[,] grid))
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
            visibilityKeysSnapshot.AddRange(terrainData.ActiveChunkKeys); //activeChunks.Keys);

            // 2. Iterate through the snapshot
            for (int i = 0; i < visibilityKeysSnapshot.Count; i++)
            {
                Vector2Int key = visibilityKeysSnapshot[i];

                // 3. Safety Check: Make sure the chunk wasn't purged while we were yielding
                //if (activeChunks.TryGetValue(key, out TerrainChunk chunk))
                if (terrainData.TryGetActiveChunk(key, out TerrainChunk chunk))
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

        Vector2Int lookupCoord = new(cx, cz);

        // 3. CACHE CHECK (The Performance Win)
        // If we are asking for a tile in the same chunk as the last call,
        // skip the Dictionary entirely and return from the local reference.
        if (lookupCoord == lastLookupCoord && lastLookupGrid != null)
        {
            return lastLookupGrid[lx, lz].Elevation;
        }

        // 4. DICTIONARY LOOKUP (The Fallback)
        //if (tileMap.TryGetValue(lookupCoord, out TileMeshStruct[,] grid))
        if (terrainData.TryGetTileData(lookupCoord, out TileMeshStruct[,] grid))
        {
            lastLookupCoord = lookupCoord;
            lastLookupGrid = grid;
            return grid[lx, lz].Elevation;
        }

        // Return 0 if data isn't generated yet (prevents crashes)
        return 0f;
    }

    public void GetGridReferences(
        Vector2Int coord,
        out TileMeshStruct[,] c,
        out TileMeshStruct[,] w,
        out TileMeshStruct[,] s,
        out TileMeshStruct[,] sw,
        out TileMeshStruct[,] e,
        out TileMeshStruct[,] n,
        out TileMeshStruct[,] nw,
        out TileMeshStruct[,] ne,
        out TileMeshStruct[,] se
    )
    {
        terrainData.TryGetTileData(coord, out c);
        terrainData.TryGetTileData(coord + Vector2Int.left, out w);
        terrainData.TryGetTileData(coord + Vector2Int.down, out s);
        terrainData.TryGetTileData(coord + new Vector2Int(-1, -1), out sw);
        terrainData.TryGetTileData(coord + Vector2Int.right, out e);
        terrainData.TryGetTileData(coord + Vector2Int.up, out n);
        // Added these 3 missing diagonals:
        terrainData.TryGetTileData(coord + new Vector2Int(-1, 1), out nw);
        terrainData.TryGetTileData(coord + new Vector2Int(1, 1), out ne);
        terrainData.TryGetTileData(coord + new Vector2Int(1, -1), out se);
    }
}
