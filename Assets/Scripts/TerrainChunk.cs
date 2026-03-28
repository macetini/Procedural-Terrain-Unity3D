using Assets.Scripts.Effects;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainChunk : MonoBehaviour
{
    private const string MESH_IDENTIFIER = "TerrainChunk_";

    private sealed class ChunkConfig
    {
        public float FrustumPadding;
        public float SkirtDepth;
        public int ChunkSize;
        public float TileSize;
        public float ElevationStepHeight;
        public int MaxElevationStep;
        public float ChunkBoundSize;

        public static ChunkConfig FromGenerator(TerrainChunksGenerator generator)
        {
            return new ChunkConfig
            {
                FrustumPadding = generator.frustumPadding,
                SkirtDepth = generator.skirtDepth,
                ChunkSize = generator.chunkSize,
                TileSize = generator.tileSize,
                ElevationStepHeight = generator.elevationStepHeight,
                MaxElevationStep = generator.maxElevationStepsCount,
                ChunkBoundSize = generator.chunkSize * generator.tileSize,
            };
        }
    }

    [Header("Settings")]
    public Material terrainMaterial;

    [Header("Debug Settings")]
    public bool DebugNormals = false;

    [Header("Effects")]
    public TerrainFadeEffect fadeEffect;

    public bool IsVisible { get; private set; } = true;
    public int CurrentStep { get; private set; } = -1;
    public Vector2Int ChunkCoord => chunkCoord;
    public TerrainChunksGenerator Generator => generator;

    // References
    private MeshRenderer rendererReference;
    private MeshFilter filterReference;

    // Data
    private TerrainChunksGenerator generator;
    private Vector2Int chunkCoord;
    private ChunkConfig config;

    // Calculations
    private bool wasVisibleLastCheck = false; // Track state change
    private bool isMeshReady = false; // Prevents "Blips" before the first build
    private string meshName;

    private readonly TerrainChunkProcessor processor = new();

    void Awake()
    {
        rendererReference = GetComponent<MeshRenderer>();
        filterReference = GetComponent<MeshFilter>();
    }

    public void CallDestroy() // WARNING: Not optimized.Should only be only used during the development phase.
    {
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (filterReference != null && filterReference.sharedMesh != null)
        {
            Destroy(filterReference.sharedMesh);
        }
    }

    public void InitBuild(TerrainChunksGenerator generator, Vector2Int chunkCoord)
    {
        this.generator = generator;
        this.chunkCoord = chunkCoord;
        config = ChunkConfig.FromGenerator(generator);

        rendererReference.enabled = false;
        isMeshReady = false;

        meshName = MESH_IDENTIFIER + chunkCoord.x + "_" + chunkCoord.y;

        if (terrainMaterial != null)
        {
            // Use sharedMaterial to allow the GPU to batch all chunks together
            rendererReference.sharedMaterial = terrainMaterial;
        }

        processor.Init(generator);
        UpdateLOD(true);
    }

    public void UpdateLOD(bool force = false)
    {
        int targetStep = GetTargetStep();

        // Only rebuild if the LOD changed OR we are forcing it (initial build)
        if (targetStep != CurrentStep || force)
        {
            CurrentStep = targetStep;
            BuildProceduralMesh();
        }
    }

    private int GetTargetStep()
    {
        // Calculate center for more accurate LOD switching
        float halfSize = config.ChunkBoundSize * 0.5f;
        Vector3 center = transform.position + new Vector3(halfSize, 0, halfSize);

        float dist = Vector3.Distance(center, generator.cameraReference.transform.position);

        if (dist > generator.lodDist2)
        {
            return 4; // LOD 2
        }
        if (dist > generator.lodDist1)
        {
            return 2; // LOD 1
        }
        return 1; // LOD 0 (full detail)
    }

    private void BuildProceduralMesh()
    {
        processor.BuildMeshData(CurrentStep, chunkCoord);
        processor.GenerateGeometryData();
        processor.CalculateNormals();

        Mesh mesh = CreateRawMesh();
        processor.PopulateMesh(mesh);

        isMeshReady = true;
        FinalizeMesh(mesh);
    }

    private Mesh CreateRawMesh()
    {
        if (filterReference.sharedMesh == null)
        {
            filterReference.sharedMesh = new Mesh { name = meshName };
            filterReference.sharedMesh.MarkDynamic();
        }
        return filterReference.sharedMesh;
    }

    private void FinalizeMesh(Mesh mesh)
    {
        float maxHeight = config.MaxElevationStep * config.ElevationStepHeight;

        // We center the bounds and apply the public frustumPadding
        Vector3 center = new(
            config.ChunkBoundSize * 0.5f,
            maxHeight * 0.5f,
            config.ChunkBoundSize * 0.5f
        );
        Vector3 size = new(
            config.ChunkBoundSize + config.FrustumPadding,
            maxHeight + config.SkirtDepth + config.FrustumPadding,
            config.ChunkBoundSize + config.FrustumPadding
        );
        mesh.bounds = new Bounds(center, size);

        if (!rendererReference.enabled)
            rendererReference.enabled = true;
    }

    public void UpdateVisibility(Plane[] planes)
    {
        float halfSize = config.ChunkBoundSize * 0.5f;
        float height = config.MaxElevationStep * config.ElevationStepHeight;

        // Use world space center
        Vector3 worldCenter = transform.position + new Vector3(halfSize, height * 0.5f, halfSize);
        Vector3 size = new(
            config.ChunkBoundSize + config.FrustumPadding,
            height + config.SkirtDepth + config.FrustumPadding,
            config.ChunkBoundSize + config.FrustumPadding
        );
        Bounds checkBounds = new(worldCenter, size);

        // 1. Calculate logical visibility (Frustum check)
        bool frustumVisible = GeometryUtility.TestPlanesAABB(planes, checkBounds);
        IsVisible = frustumVisible;

        bool finalShowState = frustumVisible && isMeshReady;

        if (fadeEffect != null && finalShowState && !wasVisibleLastCheck)
        {
            fadeEffect.Play();
        }

        if (rendererReference.enabled != finalShowState)
        {
            rendererReference.enabled = finalShowState;
        }

        wasVisibleLastCheck = finalShowState;
    }

    // ------------------------------------------------------------------------------------------------
    // -------------------------------------------- [Effects] -----------------------------------------
    // ------------------------------------------------------------------------------------------------

    public void StartFadeIn()
    {
        fadeEffect.Play();
    }

    // ------------------------------------------------------------------------------------------------
    // -------------------------------------------- [Gizmos] ------------------------------------------
    // ------------------------------------------------------------------------------------------------

    void OnDrawGizmosSelected()
    {
        if (!DebugNormals || generator == null)
        {
            return;
        }

        Mesh mesh = filterReference.sharedMesh;
        if (mesh != null)
        {
            Vector3[] verts = filterReference.sharedMesh.vertices;
            Vector3[] norms = mesh.normals;

            Gizmos.color = Color.blue;
            // We only loop through the grid vertices (ignore the skirt for clarity)
            int resolution = (config.ChunkSize / CurrentStep) + 1;
            int gridCount = resolution * resolution;

            for (int i = 0; i < gridCount; i++)
            {
                // Transform the local vertex position to world space
                Vector3 worldV = transform.TransformPoint(verts[i]);
                // Transform the normal to world space
                Vector3 worldN = transform.TransformDirection(norms[i]);
                // Draw the normal line (0.5f is the length of the line)
                Gizmos.DrawLine(worldV, worldV + worldN * 0.5f);
            }
        }
    }
}
