using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainChunk : MonoBehaviour
{
    private const string MESH_NAME = "TerrainChunk";

    [Header("Settings")]
    public Material terrainMaterial;
    public float boundPadding = 2f;
    public float skirtDepth = 5f;

    [Header("Debug Settings")]
    public bool DrawGizmos = false;

    public bool IsVisible { get; private set; } = true;
    public int CurrentStep { get; private set; } = -1;
    public TerrainFadeEffect fadeEffect;

    private TerrainChunksGenerator generator;
    private Vector2Int coord;
    private int chunkSize;
    private float tileSize;
    private float elevationStepHeight;
    private int maxElevationStep;
    private float chunkBoundSize;

    private MeshRenderer rendererReference;
    private MeshFilter filterReference;

    // Pre-allocated arrays to reduce Garbage Collection (GC) pressure
    private Vector3[] vertices;
    private Vector2[] uvs;
    private Vector3[] normals;
    private float[,] heightCache; // Added: Reuse the height cache array
    private bool isFirstBuild = true;

    void Awake()
    {
        rendererReference = GetComponent<MeshRenderer>();
        filterReference = GetComponent<MeshFilter>();
    }

    public void InitBuild(TerrainChunksGenerator gen, Vector2Int chunkCoord)
    {
        generator = gen;
        coord = chunkCoord;

        chunkSize = generator.chunkSize;
        tileSize = generator.tileSize;
        elevationStepHeight = generator.elevationStepHeight;
        maxElevationStep = generator.maxElevationStep;
        chunkBoundSize = chunkSize * tileSize;

        if (terrainMaterial != null)
        {
            rendererReference.material = terrainMaterial;
        }
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
        int resolution = (chunkSize / CurrentStep) + 1;
        int gridVertCount = resolution * resolution;
        int totalVerts = gridVertCount + (resolution * 4);

        // --- OPTIMIZATION: ARRAY REUSE ---
        if (vertices == null || vertices.Length != totalVerts)
        {
            vertices = new Vector3[totalVerts];
            uvs = new Vector2[totalVerts];
            normals = new Vector3[totalVerts];
        }

        // --- OPTIMIZATION: HEIGHT CACHE REUSE ---
        int cacheRes = resolution + 2;
        if (heightCache == null || heightCache.GetLength(0) != cacheRes)
        {
            heightCache = new float[cacheRes, cacheRes];
        }

        // --- OPTIMIZATION: MESH REUSE ---
        if (filterReference.sharedMesh == null)
        {
            filterReference.sharedMesh = new Mesh { name = $"{MESH_NAME}_{coord.x}_{coord.y}" };
            filterReference.sharedMesh.MarkDynamic();
        }
        Mesh mesh = filterReference.sharedMesh;

        // Populate Height Cache using the new Generator Fast-Lookup
        for (int x = -1; x <= resolution; x++)
        {
            for (int z = -1; z <= resolution; z++)
            {
                // We pass CurrentStep to sample the correct "Gap" for the current LOD
                heightCache[x + 1, z + 1] = GetBlendedElevation(x * CurrentStep, z * CurrentStep);
            }
        }

        GenerateGeometry(vertices, uvs, resolution, heightCache);

        // Fetch shared triangle indices (Zero Alloc)
        int[] tris = generator.GetPrecalculatedTriangles(resolution);

        CalculateSlopeNormals(vertices, normals, resolution, heightCache);

        // --- OPTIMIZATION: FAST GPU UPLOAD ---
        mesh.Clear(); // Clears indices but keeps vertex memory containers
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = tris;

        FinalizeMesh(mesh);
    }

    private void GenerateGeometry(
        Vector3[] vertices,
        Vector2[] uvCoords,
        int resolution,
        float[,] heightCache
    )
    {
        int i = 0;
        float invSize = 1f / chunkSize;

        // 1. MAIN GRID GENERATION
        for (int x = 0; x < resolution; x++)
        {
            int gx = x * CurrentStep;
            for (int z = 0; z < resolution; z++)
            {
                int gz = z * CurrentStep;

                // ANALYSIS: We use GetBlendedElevation instead of the raw height.
                // This "bevels" the edges of your steps by averaging neighbor heights.
                float h = heightCache[x + 1, z + 1] * elevationStepHeight;

                vertices[i] = new Vector3(gx * tileSize, h, gz * tileSize);
                uvCoords[i] = new Vector2(gx * invSize, gz * invSize);
                i++;
            }
        }

        // 2. SKIRT GENERATION

        // 2. SKIRTS
        // Note: We use the exact same cache indices for the top of the skirts
        // Index 1 in the cache is local 0.
        // Index res in the cache is local chunkSize.
        int skirtIdx = resolution * resolution;
        // South (z=0)
        for (int x = 0; x < resolution; x++)
        {
            float h = heightCache[x + 1, 1] * elevationStepHeight;
            vertices[skirtIdx++] = new Vector3(x * CurrentStep * tileSize, h - skirtDepth, 0);
        }
        // North (z=chunkSize)
        for (int x = 0; x < resolution; x++)
        {
            float h = heightCache[x + 1, resolution] * elevationStepHeight;
            vertices[skirtIdx++] = new Vector3(
                x * CurrentStep * tileSize,
                h - skirtDepth,
                chunkBoundSize
            );
        }
        // West (x=0)
        for (int z = 0; z < resolution; z++)
        {
            float h = heightCache[1, z + 1] * elevationStepHeight;
            vertices[skirtIdx++] = new Vector3(0, h - skirtDepth, z * CurrentStep * tileSize);
        }
        // East (x=chunkSize)
        for (int z = 0; z < resolution; z++)
        {
            float h = heightCache[resolution, z + 1] * elevationStepHeight;
            vertices[skirtIdx++] = new Vector3(
                chunkBoundSize,
                h - skirtDepth,
                z * CurrentStep * tileSize
            );
        }
    }

    // ANALYSIS: This is the core "softening" logic.
    // By sampling the 4 tiles around a vertex, we create a 3D bevel.
    private float GetBlendedElevation(int lx, int lz)
    {
        // These are the global coordinates for the current vertex
        int globalX = coord.x * chunkSize + lx;
        int globalZ = coord.y * chunkSize + lz;

        float total = 0;

        // We still sample the 4-tile cross, but now we use the
        // Generator's "LastAccessed" cache to make it lightning fast.
        total += generator.GetElevationAt(globalX, globalZ);
        total += generator.GetElevationAt(globalX - 1, globalZ);
        total += generator.GetElevationAt(globalX, globalZ - 1);
        total += generator.GetElevationAt(globalX - 1, globalZ - 1);

        // Division by 4 is much faster than checking 'samples++' every time
        return total * 0.25f;
    }

    private void CalculateSlopeNormals(Vector3[] verts, Vector3[] norms, int res, float[,] cache)
    {
        int gridCount = res * res;
        float vScale = elevationStepHeight;
        float hDist = 2.0f * tileSize * CurrentStep;

        for (int x = 0; x < res; x++)
        {
            for (int z = 0; z < res; z++)
            {
                int idx = x * res + z;

                // Sample from our padded cache [x+1, z+1] is current
                // x+2 is Right, x is Left
                float hL = cache[x, z + 1];
                float hR = cache[x + 2, z + 1];
                float hB = cache[x + 1, z];
                float hF = cache[x + 1, z + 2];

                // Standard Central Difference Tangents
                Vector3 tangentX = new(hDist, (hR - hL) * vScale, 0);
                Vector3 tangentZ = new(0, (hF - hB) * vScale, hDist);

                norms[idx] = Vector3.Cross(tangentZ, tangentX).normalized;
            }
        }

        // Skirt Normals (Stay the same: facing outward, Y=0)
        float centerX = chunkBoundSize * 0.5f;
        for (int n = gridCount; n < verts.Length; n++)
        {
            Vector3 dir = (verts[n] - new Vector3(centerX, verts[n].y, centerX)).normalized;
            norms[n] = new Vector3(dir.x, 0, dir.z);
        }
    }

    private void FinalizeMesh(Mesh mesh)
    {
        mesh.RecalculateBounds();

        // Manual Padding to ensure skirts don't trigger "popping"
        mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * boundPadding);

        if (!rendererReference.enabled)
            rendererReference.enabled = true;

        if (isFirstBuild)
        {
            if (fadeEffect != null)
                fadeEffect.Play();
            isFirstBuild = false;
        }
    }

    public void UpdateVisibility(Plane[] planes)
    {
        // Simple visibility check
        IsVisible = GeometryUtility.TestPlanesAABB(planes, rendererReference.bounds);
        rendererReference.enabled = IsVisible;
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
        if (!DrawGizmos || generator == null)
        {
            return;
        }

        /*
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                int globalX = (coord.x * chunkSize) + x;
                int globalZ = (coord.y * chunkSize) + z;

                if (generator.GetTileAt(globalX, globalZ, out TileMeshStruct outTile))
                {
                    Gizmos.color = Color.HSVToRGB(
                        outTile.Elevation / (float)maxElevationStep,
                        0.7f,
                        1f
                    );
                    Vector3 center =
                        transform.position
                        + new Vector3(
                            x * tileSize,
                            outTile.Elevation * elevationStepHeight,
                            z * tileSize
                        );
                    Gizmos.DrawWireCube(center, new Vector3(tileSize, 0.1f, tileSize));
                }
            }
        }
        */

        // --- New Normal Visualization Gizmo ---
        Mesh mesh = filterReference.sharedMesh;
        if (mesh != null)
        {
            Vector3[] verts = mesh.vertices;
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
