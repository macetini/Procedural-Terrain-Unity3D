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
    private Vector3[] _vertices;
    private Vector2[] _uvs;
    private Vector3[] _normals;

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
        if (_vertices == null || _vertices.Length != totalVerts)
        {
            _vertices = new Vector3[totalVerts];
            _uvs = new Vector2[totalVerts];
            _normals = new Vector3[totalVerts];
        }

        GenerateGeometry(_vertices, _uvs, resolution);
        int[] tris = GenerateTriangleIndices(resolution);

        CalculateSlopeNormals(_vertices, _normals, resolution);

        if (filterReference.sharedMesh != null)
        {
            Destroy(filterReference.sharedMesh);
        }

        Mesh mesh = new()
        {
            name = $"{MESH_NAME}_{coord.x}_{coord.y}",
            vertices = _vertices,
            triangles = tris,
            uv = _uvs,
            normals = _normals,
        };

        FinalizeMesh(mesh);
    }

    private void GenerateGeometry(Vector3[] vertices, Vector2[] uvs, int res)
    {
        int i = 0;
        float invSize = 1f / chunkSize;

        for (int x = 0; x < res; x++)
        {
            int gx = x * CurrentStep;
            for (int z = 0; z < res; z++)
            {
                int gz = z * CurrentStep;
                float h = GetVertexElevation(gx, gz) * elevationStepHeight;
                vertices[i] = new Vector3(gx * tileSize, h, gz * tileSize);
                uvs[i] = new Vector2(gx * invSize, gz * invSize);
                i++;
            }
        }

        // Skirts
        for (int x = 0; x < res; x++)
        { // South
            int gx = x * CurrentStep;
            vertices[i] = new Vector3(
                gx * tileSize,
                GetVertexElevation(gx, 0) * elevationStepHeight - skirtDepth,
                0
            );
            uvs[i++] = new Vector2(gx * invSize, 0);
        }
        for (int x = 0; x < res; x++)
        { // North
            int gx = x * CurrentStep;
            vertices[i] = new Vector3(
                gx * tileSize,
                GetVertexElevation(gx, chunkSize) * elevationStepHeight - skirtDepth,
                chunkBoundSize
            );
            uvs[i++] = new Vector2(gx * invSize, 1);
        }
        for (int z = 0; z < res; z++)
        { // West
            int gz = z * CurrentStep;
            vertices[i] = new Vector3(
                0,
                GetVertexElevation(0, gz) * elevationStepHeight - skirtDepth,
                gz * tileSize
            );
            uvs[i++] = new Vector2(0, gz * invSize);
        }
        for (int z = 0; z < res; z++)
        { // East
            int gz = z * CurrentStep;
            vertices[i] = new Vector3(
                chunkBoundSize,
                GetVertexElevation(chunkSize, gz) * elevationStepHeight - skirtDepth,
                gz * tileSize
            );
            uvs[i++] = new Vector2(1, gz * invSize);
        }
    }

    private int[] GenerateTriangleIndices(int res)
    {
        int gridTris = (res - 1) * (res - 1) * 6;
        int skirtTris = (res - 1) * 4 * 6;
        int[] tris = new int[gridTris + skirtTris];
        int t = 0;

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

        // 2. SKIRTS (Connecting Grid-Edges to Skirt-Vertices)
        // Direction order: South, North, West, East
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

    private void CalculateSlopeNormals(Vector3[] vertices, Vector3[] normals, int res)
    {
        int gridVertCount = res * res;
        float[,] hBuffer = new float[res + 2, res + 2];
        for (int x = -1; x <= res; x++)
        {
            for (int z = -1; z <= res; z++)
            {
                int gX = (coord.x * chunkSize) + (x * CurrentStep);
                int gZ = (coord.y * chunkSize) + (z * CurrentStep);
                hBuffer[x + 1, z + 1] = GetGlobalElevation(gX, gZ);
            }
        }

        float hStep = elevationStepHeight;
        float tStep = 2.0f * tileSize * CurrentStep;

        // 2. Grid Normals
        for (int x = 0; x < res; x++)
        {
            for (int z = 0; z < res; z++)
            {
                int index = x * res + z;
                float dx = hBuffer[x + 2, z + 1] - hBuffer[x, z + 1];
                float dz = hBuffer[x + 1, z + 2] - hBuffer[x + 1, z];

                Vector3 sharpNormal = new Vector3(-dx * hStep, tStep, -dz * hStep).normalized;

                if (Mathf.Abs(dx) < 0.01f && Mathf.Abs(dz) < 0.01f)
                    normals[index] = Vector3.up;
                else
                    normals[index] = Vector3.Slerp(Vector3.up, sharpNormal, 0.85f);
            }
        }

        // 3. Skirt Normals (Purely horizontal)
        for (int n = gridVertCount; n < vertices.Length; n++)
        {
            Vector3 pos = vertices[n];
            Vector3 dir = (
                pos - new Vector3(chunkBoundSize * 0.5f, pos.y, chunkBoundSize * 0.5f)
            ).normalized;
            normals[n] = new Vector3(dir.x, 0, dir.z);
        }
    }

    private void FinalizeMesh(Mesh mesh)
    {
        mesh.RecalculateBounds();
        // Padding prevents frustum culling from popping the mesh out at the edges
        mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * 2f);

        filterReference.mesh = mesh;

        // Handle Physics: Only high detail gets a collider to save WebGL performance
        bool highDetail = CurrentStep == 1;
        colliderReference.enabled = highDetail;
        if (highDetail)
        {
            Physics.BakeMesh(mesh.GetInstanceID(), false);
            colliderReference.sharedMesh = mesh;
        }
    }

    private float GetVertexElevation(int vx, int vz)
    {
        if (
            generator.GetTileAt(
                coord.x * chunkSize + vx,
                coord.y * chunkSize + vz,
                out TileMeshStruct tile
            )
        )
            return tile.Elevation;
        return 0;
    }

    private float GetGlobalElevation(int gx, int gz)
    {
        if (generator.GetTileAt(gx, gz, out TileMeshStruct tile))
            return tile.Elevation;
        return 0;
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
