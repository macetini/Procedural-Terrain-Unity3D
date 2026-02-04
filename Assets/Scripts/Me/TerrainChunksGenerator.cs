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
    public float lodDist1 = 640f; // Distance to switch to MEDIUM detail
    public float lodDist2 = 768f; // Distance to switch to LOW detail
    public int visibilityCheckFrameCount = 10;

    [Header("Prefabs")]
    public TerrainChunk chunkPrefab;

    // Camera related
    private Vector2Int currentCameraPosition = Vector2Int.zero;
    private Plane[] cameraPlanes;

    // Geometry Cache
    private readonly Dictionary<int, int[]> triangleCache = new();

    // Culling
    private readonly List<Vector2Int> visibilityKeysSnapshot = new();
    private float chunkBoundSize;

    // Data Processing
    public TerrainDataMap TerrainData => terrainData;
    private TerrainDataMap terrainData;

    // Build Queue
    private readonly List<Vector2Int> buildQueue = new();
    private readonly HashSet<Vector2Int> buildQueueHash = new();
    private bool isProcessingQueue = false;

    void Awake()
    {
        terrainData = new TerrainDataMap(this);
        chunkBoundSize = chunkSize * tileSize;
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
        int currentX = Mathf.FloorToInt(cameraReference.transform.position.x / chunkBoundSize);
        int currentZ = Mathf.FloorToInt(cameraReference.transform.position.z / chunkBoundSize);
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

                        if (!terrainData.HasActiveChunk(coord) && buildQueueHash.Add(coord))
                        {
                            buildQueue.Add(coord);
                        }
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
        terrainData.GetActiveKeysNonAlloc(visibilityKeysSnapshot);

        // Using a simple integer distance (Manhattan) is faster and safer for chunk grids
        int maxChunkDist = viewDistanceChunks + 2;

        foreach (var coord in visibilityKeysSnapshot)
        {
            int chunkDist =
                Mathf.Abs(coord.x - currentCameraPosition.x)
                + Mathf.Abs(coord.y - currentCameraPosition.y);

            if (
                chunkDist > maxChunkDist
                && terrainData.TryGetActiveChunk(coord, out TerrainChunk chunk)
            )
            {
                // Physical chunk removal
                Destroy(chunk.gameObject);

                // Data removal
                terrainData.UnregisterChunk(coord);
                terrainData.RemoveSanitization(coord);
                terrainData.RemoveTileData(coord);
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
                if (!terrainData.HasActiveChunk(coord) && !buildQueue.Contains(coord))
                {
                    buildQueue.Add(coord);
                    addedNew = true;
                }
                else if (terrainData.TryGetActiveChunk(coord, out TerrainChunk chunk))
                {
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
            buildQueueHash.Remove(coord);

            // Don't build if the player already moved away (snapping to a new far away chunk)
            float distToCam = Vector2Int.Distance(coord, currentCameraPosition);
            if (distToCam > viewDistanceChunks + 1)
                continue;

            if (!terrainData.HasActiveChunk(coord))
            {
                // 1. Ensure a 3x3 block of RAW DATA exists
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector2Int n = coord + new Vector2Int(x, z);
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
        Vector3 position = new(coord.x * chunkBoundSize, 0, coord.y * chunkBoundSize);
        TerrainChunk chunk = Instantiate(chunkPrefab, position, Quaternion.identity, transform);

        chunk.InitBuild(this, coord);
        chunk.UpdateVisibility(cameraPlanes);
        terrainData.RegisterChunk(coord, chunk);
    }

    public bool GetTileAt(int globalX, int globalZ, out TileMeshStruct tile)
    {
        // We can actually move this to TerrainDataMap too, but for now:
        int cx = Mathf.FloorToInt((float)globalX / chunkSize);
        int cz = Mathf.FloorToInt((float)globalZ / chunkSize);
        int lx = globalX - (cx * chunkSize);
        int lz = globalZ - (cz * chunkSize);

        if (terrainData.TryGetTileData(new Vector2Int(cx, cz), out TileMeshStruct[,] grid))
        {
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
            visibilityKeysSnapshot.AddRange(terrainData.ActiveChunkKeys);

            // 2. Iterate through the snapshot
            for (int i = 0; i < visibilityKeysSnapshot.Count; i++)
            {
                Vector2Int key = visibilityKeysSnapshot[i];

                // 3. Safety Check: Make sure the chunk wasn't purged while we were yielding
                if (terrainData.TryGetActiveChunk(key, out TerrainChunk chunk))
                {
                    chunk.UpdateVisibility(cameraPlanes);

                    if (chunk.IsVisible && chunk.CurrentStep < 0)
                    {
                        chunk.UpdateLOD(true);
                    }
                }

                // 4. Time Slicing: Only process after X frames
                if (i % visibilityCheckFrameCount == 0)
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
        int[] newTris = TerrainMath.GenerateTriangleIndices(resolution);
        triangleCache.Add(resolution, newTris);
        return newTris;
    }
}
