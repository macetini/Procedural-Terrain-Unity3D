using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainChunk : MonoBehaviour
{
    private const string MESH_IDENTIFIER = "TerrainChunk_";

    [Header("Settings")]
    public Material terrainMaterial;

    [Header("Debug Settings")]
    public bool DebugNormals = false;

    [Header("Effects")]
    public TerrainFadeEffect fadeEffect;

    public bool IsVisible { get; private set; } = true;
    public int CurrentStep { get; private set; } = -1;

    // References
    private MeshRenderer rendererReference;
    private MeshFilter filterReference;

    // Data
    private TerrainChunksGenerator generator;
    private Vector2Int chunkCoord;

    private float frustumPadding;
    private float skirtDepth;
    private int chunkSize;
    private float tileSize;
    private float elevationStepHeight;
    private int maxElevationStep;

    // Calculations
    private float chunkBoundSize;
    private bool wasVisibleLastCheck = false; // Track state change
    private bool isMeshReady = false; // Prevents "Blips" before the first build
    private string meshName;

    private readonly TerrainChunkProcessor processor = new();

    void Awake()
    {
        rendererReference = GetComponent<MeshRenderer>();
        filterReference = GetComponent<MeshFilter>();
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

        frustumPadding = this.generator.frustumPadding;
        skirtDepth = this.generator.skirtDepth;

        chunkSize = this.generator.chunkSize;
        tileSize = this.generator.tileSize;
        elevationStepHeight = this.generator.elevationStepHeight;
        maxElevationStep = this.generator.maxElevationStepsCount;
        chunkBoundSize = chunkSize * tileSize;

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
        float halfSize = chunkBoundSize * 0.5f;
        Vector3 center = transform.position + new Vector3(halfSize, 0, halfSize);

        float dist = Vector3.Distance(center, generator.cameraReference.transform.position);

        // TODO - Put LOD values in Config
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
        float maxHeight = maxElevationStep * elevationStepHeight;

        // We center the bounds and apply the public frustumPadding
        Vector3 center = new(chunkBoundSize * 0.5f, maxHeight * 0.5f, chunkBoundSize * 0.5f);
        Vector3 size = new(
            chunkBoundSize + frustumPadding,
            maxHeight + skirtDepth + frustumPadding,
            chunkBoundSize + frustumPadding
        );
        mesh.bounds = new Bounds(center, size);

        if (!rendererReference.enabled)
            rendererReference.enabled = true;
    }

    public void UpdateVisibility(Plane[] planes)
    {
        float halfSize = chunkBoundSize * 0.5f;
        float height = maxElevationStep * elevationStepHeight;

        // Use world space center
        Vector3 worldCenter = transform.position + new Vector3(halfSize, height * 0.5f, halfSize);
        Vector3 size = new(
            chunkBoundSize + frustumPadding,
            height + skirtDepth + frustumPadding,
            chunkBoundSize + frustumPadding
        );
        Bounds checkBounds = new(worldCenter, size);

        // 1. Calculate logical visibility (Frustum check)
        bool frustumVisible = GeometryUtility.TestPlanesAABB(planes, checkBounds);
        IsVisible = frustumVisible;

        bool finalShowState = frustumVisible && isMeshReady;

        // [OPTIMIZED] Trigger Fade Effect on "Entry"
        if (finalShowState && !wasVisibleLastCheck)
        {
            if (fadeEffect != null)
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
            int resolution = (chunkSize / CurrentStep) + 1;
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
