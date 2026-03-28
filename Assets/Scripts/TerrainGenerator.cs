using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Processor;
using UnityEngine;
using UnityEngine.InputSystem;

public class TerrainChunksGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    public int chunkSize = 16;
    public float tileSize = 1.0f;
    public float elevationStepHeight = 1.0f;
    public int maxElevationStepsCount = 5;
    public float skirtDepth = 5f;

    [Header("Noise Settings")]
    public int noiseSeed = 1337;
    public float noiseScale = 0.05f;
    public int noiseOctaves = 4;
    public float noisePersistence = 0.5f;
    public float noiseLacunarity = 2.0f;

    [Header("Camera Settings")]
    public Camera cameraReference;
    public float frustumPadding = 5.0f;
    public int viewDistanceChunks = 3;

    [Header("LOD Settings")]
    public float lodDist1 = 640f; // Distance to switch to MEDIUM detail
    public float lodDist2 = 768f; // Distance to switch to LOW detail
    public int visibilityCheckFrameCount = 10;

    [Header("Debug")]
    public bool debugCleanupLogs = false;
    public bool debugCleanupLogOnlyOnRemoval = true;
    public bool debugCleanupGizmos = true;
    public float debugGizmoChunkY = 0.25f;

    [Header("Prefabs")]
    public TerrainChunk chunkPrefab;

    // Camera related
    private Vector2Int currentCameraPosition = Vector2Int.zero;
    private Plane[] cameraPlanes;

    // Geometry Cache
    private readonly Dictionary<int, int[]> triangleCache = new();

    // Culling
    private readonly List<Vector2Int> visibilityKeysSnapshot = new();
    private readonly List<TerrainChunk> sceneChunksSnapshot = new();
    private readonly HashSet<Vector2Int> cleanupSeenCoords = new();
    private float chunkBoundSize;
    private readonly List<Vector2Int> lastRemovedChunks = new();
    private int lastCleanupRemovedCount;
    private int lastCleanupScannedCount;
    private int lastCleanupSceneChunkCount;
    private int lastCleanupOrphanedCount;
    private int lastCleanupDuplicateCount;

    // Data Processing
    private bool worldMonitoringActive = true;
    public TerrainDataProcessor TerrainDataProcessor => _terrainDataProcessor;
    private TerrainDataProcessor _terrainDataProcessor;

    // Build Queue
    private readonly List<Vector2Int> buildQueue = new();
    private readonly HashSet<Vector2Int> buildQueueHash = new();
    private bool isProcessingQueue = false;

    void Awake()
    {
        _terrainDataProcessor = new TerrainDataProcessor(chunkSize);
    }

    void OnDestroy()
    {
        worldMonitoringActive = false;
    }

    void Start()
    {
        BuildTerrain();
    }

    void Update()
    {
        UpdateCurrentCameraPosition();

        // WARNING: This will rebuild the whole terrain. Should only be used during development.
        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            Debug.Log("Rebuilding terrain.");

            StopAllCoroutines();
            buildQueue.Clear();
            buildQueueHash.Clear();
            triangleCache.Clear();
            lastRemovedChunks.Clear();
            lastCleanupRemovedCount = 0;
            lastCleanupScannedCount = 0;
            lastCleanupSceneChunkCount = 0;
            lastCleanupOrphanedCount = 0;
            lastCleanupDuplicateCount = 0;
            _terrainDataProcessor.ClearAll();
            BuildTerrain();
        }
    }

    private void BuildTerrain()
    {
        TerrainNoise.Init(
            noiseSeed,
            noiseScale,
            noiseOctaves,
            noisePersistence,
            noiseLacunarity,
            maxElevationStepsCount
        );
        chunkBoundSize = chunkSize * tileSize;
        UpdateCurrentCameraPosition();
        FirstPass();
        SecondPass();

        StartCoroutine(WorldMonitoringRoutine()); // The manager
        StartCoroutine(VisibilityCheckRoutine()); // The culler
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
        while (worldMonitoringActive && this != null && isActiveAndEnabled)
        {
            if (currentCameraPosition != lastProcessedPos)
            {
                lastProcessedPos = currentCameraPosition;

                CleanupRemoteChunks();

                yield return ProcessLocalChunks();
            }
            yield return null;
        }
    }

    private IEnumerator ProcessLocalChunks()
    {
        for (int x = -viewDistanceChunks; x <= viewDistanceChunks; x++)
        {
            for (int z = -viewDistanceChunks; z <= viewDistanceChunks; z++)
            {
                Vector2Int coord = currentCameraPosition + new Vector2Int(x, z);

                if (TryEnqueueChunkBuild(coord)) { }
                else if (_terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk chunk))
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

    private void CleanupRemoteChunks()
    {
        CleanupSceneChunkOrphans();

        visibilityKeysSnapshot.Clear();
        _terrainDataProcessor.GetActiveKeysNonAlloc(visibilityKeysSnapshot);
        lastCleanupScannedCount = visibilityKeysSnapshot.Count;
        lastCleanupRemovedCount = 0;
        lastRemovedChunks.Clear();

        int retentionRadius = GetRetentionRadius();

        foreach (var coord in visibilityKeysSnapshot)
        {
            int dx = Mathf.Abs(coord.x - currentCameraPosition.x);
            int dz = Mathf.Abs(coord.y - currentCameraPosition.y);

            // Remove every chunk outside the active visible square around camera.
            if (
                (dx > retentionRadius || dz > retentionRadius)
                && _terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk chunk)
            )
            {
                RemoveChunk(coord, chunk);
            }
        }

        bool shouldLogCleanup =
            debugCleanupLogs && (!debugCleanupLogOnlyOnRemoval || lastCleanupRemovedCount > 0);

        if (shouldLogCleanup)
        {
            int retainedCount = lastCleanupScannedCount - lastCleanupRemovedCount;
            Debug.Log(
                $"[CleanupRemoteChunks] CameraChunk={currentCameraPosition}, RegistryScanned={lastCleanupScannedCount}, SceneChunks={lastCleanupSceneChunkCount}, Removed={lastCleanupRemovedCount}, Retained={retainedCount}, Orphans={lastCleanupOrphanedCount}, Duplicates={lastCleanupDuplicateCount}, RetentionRadius={retentionRadius}"
            );
        }
    }

    private void CleanupSceneChunkOrphans()
    {
        sceneChunksSnapshot.Clear();
        cleanupSeenCoords.Clear();
        lastCleanupSceneChunkCount = 0;
        lastCleanupOrphanedCount = 0;
        lastCleanupDuplicateCount = 0;

        GetComponentsInChildren(true, sceneChunksSnapshot);
        lastCleanupSceneChunkCount = sceneChunksSnapshot.Count;

        for (int i = 0; i < sceneChunksSnapshot.Count; i++)
        {
            TerrainChunk sceneChunk = sceneChunksSnapshot[i];
            if (sceneChunk == null)
            {
                continue;
            }

            Vector2Int coord = sceneChunk.ChunkCoord;
            bool isDuplicateCoord = !cleanupSeenCoords.Add(coord);
            bool isOutOfBounds = !IsWithinRetentionBounds(coord);
            bool isForeignChunk = sceneChunk.Generator != null && sceneChunk.Generator != this;

            if (isDuplicateCoord)
            {
                lastCleanupDuplicateCount++;
            }

            if (isDuplicateCoord || isOutOfBounds || isForeignChunk)
            {
                lastCleanupOrphanedCount++;
                RemoveChunk(coord, sceneChunk);
            }
        }
    }

    private void RemoveChunk(Vector2Int coord, TerrainChunk chunk)
    {
        lastCleanupRemovedCount++;
        lastRemovedChunks.Add(coord);

        if (
            _terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk activeChunk)
            && activeChunk == chunk
        )
        {
            _terrainDataProcessor.Clear(coord);
        }

        if (chunk != null)
        {
            Destroy(chunk.gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugCleanupGizmos || chunkSize <= 0 || tileSize <= 0f)
        {
            return;
        }

        float boundSize = chunkBoundSize > 0f ? chunkBoundSize : chunkSize * tileSize;
        Vector2Int camChunk = currentCameraPosition;

        if (!Application.isPlaying && cameraReference != null)
        {
            int currentX = Mathf.FloorToInt(cameraReference.transform.position.x / boundSize);
            int currentZ = Mathf.FloorToInt(cameraReference.transform.position.z / boundSize);
            camChunk = new Vector2Int(currentX, currentZ);
        }

        int visibleRadius = Mathf.Max(0, viewDistanceChunks);
        int retentionRadius = GetRetentionRadius();

        DrawChunkBoundsGizmo(camChunk, visibleRadius, boundSize, debugGizmoChunkY, Color.green);
        if (retentionRadius != visibleRadius)
        {
            DrawChunkBoundsGizmo(
                camChunk,
                retentionRadius,
                boundSize,
                debugGizmoChunkY,
                Color.yellow
            );
        }

        Gizmos.color = Color.red;
        for (int i = 0; i < lastRemovedChunks.Count; i++)
        {
            Vector3 removedCenter = ChunkToWorldCenter(
                lastRemovedChunks[i],
                boundSize,
                debugGizmoChunkY
            );
            Gizmos.DrawWireCube(removedCenter, new Vector3(boundSize, 0.05f, boundSize));
        }
    }

    private static Vector3 ChunkToWorldCenter(Vector2Int chunkCoord, float boundSize, float y)
    {
        return new Vector3((chunkCoord.x + 0.5f) * boundSize, y, (chunkCoord.y + 0.5f) * boundSize);
    }

    private static void DrawChunkBoundsGizmo(
        Vector2Int centerChunk,
        int radius,
        float boundSize,
        float y,
        Color color
    )
    {
        float chunkCount = (radius * 2) + 1;
        Vector3 worldCenter = ChunkToWorldCenter(centerChunk, boundSize, y);
        Vector3 worldSize = new Vector3(chunkCount * boundSize, 0.05f, chunkCount * boundSize);

        Gizmos.color = color;
        Gizmos.DrawWireCube(worldCenter, worldSize);
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
        _terrainDataProcessor.SanitizeData(currentCameraPosition, dataRadius);
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
                _terrainDataProcessor.GenerateRawData(coord);
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
                if (TryEnqueueChunkBuild(coord))
                {
                    addedNew = true;
                }
                else if (_terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk chunk))
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

            // Another coroutine may have built this chunk already while we were yielding.
            if (_terrainDataProcessor.HasActiveChunk(coord))
            {
                continue;
            }

            // Don't build if the player already moved away from the retained square.
            if (!IsWithinRetentionBounds(coord))
                continue;

            yield return GenerateRawDataForChunk(coord);
            yield return EnsureSanitized(coord);

            if (_terrainDataProcessor.HasActiveChunk(coord) || !IsWithinRetentionBounds(coord))
            {
                continue;
            }

            SpawnChunkMesh(coord);

            if (_terrainDataProcessor.TryGetActiveChunk(coord, out TerrainChunk chunk))
            {
                chunk.StartFadeIn();
            }
            yield return null;
        }
        isProcessingQueue = false;
    }

    private IEnumerator GenerateRawDataForChunk(Vector2Int coord)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                Vector2Int n = coord + new Vector2Int(x, z);
                if (!_terrainDataProcessor.HasTileData(n))
                {
                    GenerateFullMeshData(n, 0);
                    yield return null;
                }
            }
        }
        yield return null;
    }

    private IEnumerator EnsureSanitized(Vector2Int coord)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                Vector2Int n = coord + new Vector2Int(x, z);
                if (!_terrainDataProcessor.IsSanitized(n))
                {
                    _terrainDataProcessor.SanitizeGlobalChunk(n);
                    _terrainDataProcessor.MarkSanitized(n);
                    yield return null;
                }
            }
        }
        yield return null;
    }

    private void SpawnChunkMesh(Vector2Int coord)
    {
        if (_terrainDataProcessor.HasActiveChunk(coord))
        {
            return;
        }

        Vector3 position = new(coord.x * chunkBoundSize, 0, coord.y * chunkBoundSize);
        TerrainChunk chunk = Instantiate(chunkPrefab, position, Quaternion.identity, transform);

        chunk.InitBuild(this, coord);
        chunk.UpdateVisibility(cameraPlanes);
        _terrainDataProcessor.RegisterChunk(coord, chunk);
    }

    private bool TryEnqueueChunkBuild(Vector2Int coord)
    {
        if (_terrainDataProcessor.HasActiveChunk(coord) || !buildQueueHash.Add(coord))
        {
            return false;
        }

        buildQueue.Add(coord);
        return true;
    }

    private bool IsWithinRetentionBounds(Vector2Int coord)
    {
        int retentionRadius = GetRetentionRadius();
        int dx = Mathf.Abs(coord.x - currentCameraPosition.x);
        int dz = Mathf.Abs(coord.y - currentCameraPosition.y);
        return dx <= retentionRadius && dz <= retentionRadius;
    }

    private int GetRetentionRadius()
    {
        return Mathf.Max(0, viewDistanceChunks);
    }

    private void SortBuildQueue()
    {
        if (buildQueue.Count <= 1)
        {
            return;
        }

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
        while (worldMonitoringActive && this != null && isActiveAndEnabled)
        {
            cameraPlanes = GeometryUtility.CalculateFrustumPlanes(cameraReference);

            // Take a snapshot of the current keys
            visibilityKeysSnapshot.Clear();
            visibilityKeysSnapshot.AddRange(_terrainDataProcessor.ActiveChunkKeys);

            // Iterate through the snapshot
            for (int i = 0; i < visibilityKeysSnapshot.Count; i++)
            {
                Vector2Int key = visibilityKeysSnapshot[i];

                // Safety Check: Make sure the chunk wasn't purged while we were yielding
                if (_terrainDataProcessor.TryGetActiveChunk(key, out TerrainChunk chunk))
                {
                    chunk.UpdateVisibility(cameraPlanes);

                    if (chunk.IsVisible && chunk.CurrentStep < 0)
                    {
                        chunk.UpdateLOD(true);
                    }
                }

                // Time Slicing: Only process after X frames
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
