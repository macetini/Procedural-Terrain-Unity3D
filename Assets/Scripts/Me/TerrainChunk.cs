using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
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

    private TerrainChunksGenerator generator;
    private Vector2Int coord;
    private int chunkSize;
    private float tileSize;
    private float elevationStepHeight;
    private int maxElevationStep;
    private float chunkBoundSize;

    private MeshRenderer rendererReference;
    private MeshFilter filterReference;
    private MeshCollider colliderReference;

    // Pre-allocated arrays to reduce Garbage Collection (GC) pressure
    private Vector3[] vertices;
    private Vector2[] uvs;
    private Vector3[] normals;

    void Awake()
    {
        rendererReference = GetComponent<MeshRenderer>();
        filterReference = GetComponent<MeshFilter>();
        colliderReference = GetComponent<MeshCollider>();
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
        // 1. Data Allocation & Geometry Generation
        int resolution = (chunkSize / CurrentStep) + 1;
        int gridVertCount = resolution * resolution;
        int totalVerts = gridVertCount + (resolution * 4);

        // Re-allocate only if resolution changed
        if (vertices == null || vertices.Length != totalVerts)
        {
            vertices = new Vector3[totalVerts];
            uvs = new Vector2[totalVerts];
            normals = new Vector3[totalVerts];
        }

        GenerateGeometry(vertices, uvs, resolution);
        int[] tris = GenerateTriangleIndices(resolution);

        CalculateSlopeNormals(vertices, normals, resolution);

        if (filterReference.sharedMesh != null)
        {
            Destroy(filterReference.sharedMesh);
        }

        Mesh mesh = new()
        {
            name = $"{MESH_NAME}_{coord.x}_{coord.y}",
            vertices = vertices,
            triangles = tris,
            uv = uvs,
            normals = normals,
        };

        FinalizeMesh(mesh);
    }

    private void GenerateGeometry(Vector3[] vertices, Vector2[] uvs, int res)
    {
        int i = 0;
        float invSize = 1f / chunkSize;

        // 1. MAIN GRID GENERATION
        for (int x = 0; x < res; x++)
        {
            int gx = x * CurrentStep;
            for (int z = 0; z < res; z++)
            {
                int gz = z * CurrentStep;

                // ANALYSIS: We use GetBlendedElevation instead of the raw height.
                // This "bevels" the edges of your steps by averaging neighbor heights.
                float h = GetBlendedElevation(gx, gz) * elevationStepHeight;

                vertices[i] = new Vector3(gx * tileSize, h, gz * tileSize);
                uvs[i] = new Vector2(gx * invSize, gz * invSize);
                i++;
            }
        }

        // 2. SKIRT GENERATION
        // Skirts provide the "sides" so the terrain doesn't look like a floating sheet.
        // We subtract 'skirtDepth' to push the bottom vertices into the ground.

        // South Wall (z = 0)
        for (int x = 0; x < res; x++)
        {
            int gx = x * CurrentStep;
            vertices[i] = new Vector3(
                gx * tileSize,
                GetBlendedElevation(gx, 0) * elevationStepHeight - skirtDepth,
                0
            );
            uvs[i++] = new Vector2(gx * invSize, 0);
        }
        // North Wall (z = chunkSize)
        for (int x = 0; x < res; x++)
        {
            int gx = x * CurrentStep;
            vertices[i] = new Vector3(
                gx * tileSize,
                GetBlendedElevation(gx, chunkSize) * elevationStepHeight - skirtDepth,
                chunkBoundSize
            );
            uvs[i++] = new Vector2(gx * invSize, 1);
        }
        // West Wall (x = 0)
        for (int z = 0; z < res; z++)
        {
            int gz = z * CurrentStep;
            vertices[i] = new Vector3(
                0,
                GetBlendedElevation(0, gz) * elevationStepHeight - skirtDepth,
                gz * tileSize
            );
            uvs[i++] = new Vector2(0, gz * invSize);
        }
        // East Wall (x = chunkSize)
        for (int z = 0; z < res; z++)
        {
            int gz = z * CurrentStep;
            vertices[i] = new Vector3(
                chunkBoundSize,
                GetBlendedElevation(chunkSize, gz) * elevationStepHeight - skirtDepth,
                gz * tileSize
            );
            uvs[i++] = new Vector2(1, gz * invSize);
        }
    }

    // ANALYSIS: This is the core "softening" logic.
    // By sampling the 4 tiles around a vertex, we create a 3D bevel.
    private float GetBlendedElevation(int lx, int lz)
    {
        int globalX = coord.x * chunkSize + lx;
        int globalZ = coord.y * chunkSize + lz;

        float total = 0;
        int samples = 0;

        // Sample a cross pattern (the current tile and 3 neighbors)
        // This is mathematically equivalent to Bilinear Filtering.
        for (int x = -1; x <= 0; x++)
        {
            for (int z = -1; z <= 0; z++)
            {
                if (generator.GetTileAt(globalX + x, globalZ + z, out TileMeshStruct tile))
                {
                    total += tile.Elevation;
                    samples++;
                }
            }
        }
        return samples > 0 ? total / samples : 0;
    }

    private void CalculateSlopeNormals(Vector3[] vertices, Vector3[] normals, int res)
    {
        int gridCount = res * res;
        // Step-aware lighting: We look at the actual geometry slopes.
        for (int x = 0; x < res; x++)
        {
            for (int z = 0; z < res; z++)
            {
                int idx = x * res + z;
                // Central difference sampling for smooth normal transitions
                float hL = GetBlendedElevation((x - 1) * CurrentStep, z * CurrentStep);
                float hR = GetBlendedElevation((x + 1) * CurrentStep, z * CurrentStep);
                float hB = GetBlendedElevation(x * CurrentStep, (z - 1) * CurrentStep);
                float hF = GetBlendedElevation(x * CurrentStep, (z + 1) * CurrentStep);

                Vector3 tangentX = new Vector3(
                    2 * tileSize * CurrentStep,
                    (hR - hL) * elevationStepHeight,
                    0
                );
                Vector3 tangentZ = new Vector3(
                    0,
                    (hF - hB) * elevationStepHeight,
                    2 * tileSize * CurrentStep
                );

                // The Cross Product ensures the normal is perpendicular to the "softened" slope
                normals[idx] = Vector3.Cross(tangentZ, tangentX).normalized;
            }
        }

        // Skirt Normals: Force them to face strictly horizontal to avoid sand-texture bleeding
        float centerX = chunkBoundSize * 0.5f;
        for (int n = gridCount; n < vertices.Length; n++)
        {
            Vector3 dir = (vertices[n] - new Vector3(centerX, vertices[n].y, centerX)).normalized;
            normals[n] = new Vector3(dir.x, 0, dir.z);
        }
    }

    private int[] GenerateTriangleIndices(int res)
    {
        int gridTris = (res - 1) * (res - 1) * 6;
        int skirtTris = (res - 1) * 4 * 6;
        int[] tris = new int[gridTris + skirtTris];
        int t = 0;

        // 1. GRID TRIANGLES
        for (int x = 0; x < res - 1; x++)
        {
            for (int z = 0; z < res - 1; z++)
            {
                int bl = x * res + z;
                int tl = bl + 1;
                int br = (x + 1) * res + z;
                int tr = br + 1;
                tris[t++] = bl;
                tris[t++] = tl;
                tris[t++] = br;
                tris[t++] = tl;
                tris[t++] = tr;
                tris[t++] = br;
            }
        }

        // 2. SKIRT TRIANGLES (Connecting top edges to skirt floor)
        int gridCount = res * res;
        for (int j = 0; j < res - 1; j++)
        {
            // South
            int gL = j * res;
            int gR = (j + 1) * res;
            int sL = gridCount + j;
            int sR = gridCount + j + 1;
            tris[t++] = gL;
            tris[t++] = sR;
            tris[t++] = gR;
            tris[t++] = gL;
            tris[t++] = sL;
            tris[t++] = sR;
            // North
            int ngL = j * res + (res - 1);
            int ngR = (j + 1) * res + (res - 1);
            int nsL = gridCount + res + j;
            int nsR = gridCount + res + j + 1;
            tris[t++] = ngL;
            tris[t++] = ngR;
            tris[t++] = nsR;
            tris[t++] = ngL;
            tris[t++] = nsR;
            tris[t++] = nsL;
            // West
            int wgB = j;
            int wgT = j + 1;
            int wsB = gridCount + res * 2 + j;
            int wsT = gridCount + res * 2 + j + 1;
            tris[t++] = wgB;
            tris[t++] = wsT;
            tris[t++] = wgT;
            tris[t++] = wgB;
            tris[t++] = wsB;
            tris[t++] = wsT;
            // East
            int egB = (res - 1) * res + j;
            int egT = (res - 1) * res + j + 1;
            int esB = gridCount + res * 3 + j;
            int esT = gridCount + res * 3 + j + 1;
            tris[t++] = egB;
            tris[t++] = egT;
            tris[t++] = esT;
            tris[t++] = egB;
            tris[t++] = esT;
            tris[t++] = esB;
        }
        return tris;
    }

    private void FinalizeMesh(Mesh mesh)
    {
        mesh.RecalculateBounds();
        mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * 2f);
        filterReference.mesh = mesh;
        bool highDetail = (CurrentStep == 1);
        colliderReference.enabled = highDetail;
        if (highDetail)
        {
            Physics.BakeMesh(mesh.GetInstanceID(), false);
            colliderReference.sharedMesh = mesh;
        }
    }

    public void UpdateVisibility(Plane[] planes)
    {
        // Use mesh bounds if available, otherwise calculate a proxy bound based on settings
        Bounds checkBounds;
        if (filterReference.sharedMesh != null)
        {
            checkBounds = rendererReference.bounds;
        }
        else
        {
            // Proxy bound: Center of the chunk with a height based on maxElevation
            float halfBoundSize = chunkBoundSize * 0.5f;
            float totalMaxElevationHight = maxElevationStep * elevationStepHeight;
            Vector3 center =
                transform.position
                + new Vector3(halfBoundSize, totalMaxElevationHight * 0.5f, halfBoundSize);
            Vector3 boxSize = new(chunkBoundSize, totalMaxElevationHight, chunkBoundSize);
            checkBounds = new Bounds(center, boxSize);
        }

        IsVisible = GeometryUtility.TestPlanesAABB(planes, checkBounds);

        rendererReference.enabled = IsVisible;
        colliderReference.enabled = IsVisible;
    }

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
