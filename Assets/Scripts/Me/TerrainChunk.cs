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

        Vector3[] vertices = new Vector3[totalVerts];
        Vector2[] uvs = new Vector2[totalVerts];
        GenerateGeometry(vertices, uvs);

        int[] tris = GenerateTriangleIndices(resolution);

        // 2. Mesh Construction
        if (filterReference.sharedMesh != null)
            Destroy(filterReference.sharedMesh);

        Mesh mesh = new()
        {
            name = $"{MESH_NAME}_{coord.x}_{coord.y}",
            vertices = vertices,
            triangles = tris,
            uv = uvs,
            // 3. Normal & Lighting Calculations
            normals = CalculateSlopeNormals(vertices, resolution),
        };

        // 4. Finalizing and Component Assignment
        FinalizeMesh(mesh);
    }

    private int[] GenerateTriangleIndices(int resolution)
    {
        int gridTris = (resolution - 1) * (resolution - 1) * 6;
        int skirtTris = (resolution - 1) * 4 * 6;
        int[] tris = new int[gridTris + skirtTris];

        GenerateTriangles(tris, resolution);
        return tris;
    }

    private Vector3[] CalculateSlopeNormals(Vector3[] vertices, int resolution)
    {
        Vector3[] normals = new Vector3[vertices.Length];
        int gridVertCount = resolution * resolution;

        for (int x = 0; x < resolution; x++)
        {
            for (int z = 0; z < resolution; z++)
            {
                int index = x * resolution + z;
                float currentH = vertices[index].y;

                // Sample neighbors in the grid
                // We use a small threshold to determine what counts as "Flat"
                float threshold = 0.01f;
                Vector3 slopeDir = Vector3.zero;

                // Check Left, Right, Back, Forward
                if (x > 0 && currentH - vertices[(x - 1) * resolution + z].y > threshold)
                    slopeDir += Vector3.right;
                if (
                    x < resolution - 1
                    && currentH - vertices[(x + 1) * resolution + z].y > threshold
                )
                    slopeDir += Vector3.left;
                if (z > 0 && currentH - vertices[x * resolution + (z - 1)].y > threshold)
                    slopeDir += Vector3.forward;
                if (
                    z < resolution - 1
                    && currentH - vertices[x * resolution + (z + 1)].y > threshold
                )
                    slopeDir += Vector3.back;

                // THE LOGIC:
                if (slopeDir == Vector3.zero)
                {
                    // 1. Flat Plateau: Face strictly Up
                    normals[index] = Vector3.up;
                }
                else
                {
                    // 2. Slope: Tilt the normal toward the descent.
                    // Using (Up + SlopeDir) at a 1:1 ratio creates a 45-degree normal.
                    // This is exactly what the Triplanar "Green Channel" Power trick needs.
                    normals[index] = (Vector3.up + slopeDir.normalized).normalized;
                }
            }
        }

        // 3. Skirt Normals (strictly horizontal to hide the bottom)
        for (int n = gridVertCount; n < vertices.Length; n++)
        {
            Vector3 pos = vertices[n];
            Vector3 center = new Vector3(chunkBoundSize * 0.5f, pos.y, chunkBoundSize * 0.5f);
            Vector3 dir = (pos - center).normalized;
            normals[n] = new Vector3(dir.x, 0, dir.z);
        }

        return normals;
    }

    private void FinalizeMesh(Mesh mesh)
    {
        mesh.RecalculateBounds();
        // Padding prevents frustum culling from popping the mesh out at the edges
        mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * 2f);

        filterReference.mesh = mesh;

        // Handle Physics: Only high detail gets a collider to save WebGL performance
        bool highDetail = (CurrentStep == 1);
        colliderReference.enabled = highDetail;
        if (highDetail)
        {
            colliderReference.sharedMesh = mesh;
        }
    }

    private void GenerateGeometry(Vector3[] vertices, Vector2[] UVs)
    {
        int i = 0;
        // 1. Main Terrain Grid (Inner Loop Z, Outer Loop X)
        for (int x = 0; x <= chunkSize; x += CurrentStep)
        {
            for (int z = 0; z <= chunkSize; z += CurrentStep)
            {
                float h = GetVertexElevation(x, z) * elevationStepHeight;
                vertices[i] = new Vector3(x * tileSize, h, z * tileSize);
                UVs[i] = new Vector2((float)x / chunkSize, (float)z / chunkSize);
                i++;
            }
        }

        // 2. Skirt (Matching the exact order used in GenerateTriangles)

        // South Edge (z = 0)
        for (int x = 0; x <= chunkSize; x += CurrentStep)
        {
            float h = GetVertexElevation(x, 0) * elevationStepHeight - skirtDepth;
            vertices[i] = new Vector3(x * tileSize, h, 0);
            UVs[i++] = new Vector2((float)x / chunkSize, 0);
        }
        // North Edge (z = chunkSize)
        for (int x = 0; x <= chunkSize; x += CurrentStep)
        {
            float h = GetVertexElevation(x, chunkSize) * elevationStepHeight - skirtDepth;
            vertices[i] = new Vector3(x * tileSize, h, chunkSize * tileSize);
            UVs[i++] = new Vector2((float)x / chunkSize, 1);
        }
        // West Edge (x = 0)
        for (int z = 0; z <= chunkSize; z += CurrentStep)
        {
            float h = GetVertexElevation(0, z) * elevationStepHeight - skirtDepth;
            vertices[i] = new Vector3(0, h, z * tileSize);
            UVs[i++] = new Vector2(0, (float)z / chunkSize);
        }
        // East Edge (x = chunkSize)
        for (int z = 0; z <= chunkSize; z += CurrentStep)
        {
            float h = GetVertexElevation(chunkSize, z) * elevationStepHeight - skirtDepth;
            vertices[i] = new Vector3(chunkSize * tileSize, h, z * tileSize);
            UVs[i++] = new Vector2(1, (float)z / chunkSize);
        }
    }

    private static void GenerateTriangles(int[] tris, int res)
    {
        int trisIndex = 0;
        // 1. MAIN TERRAIN GRID
        for (int x = 0; x < res - 1; x++)
        {
            for (int z = 0; z < res - 1; z++)
            {
                int bl = x * res + z;
                int tl = bl + 1;
                int br = (x + 1) * res + z;
                int tr = br + 1;

                tris[trisIndex++] = bl;
                tris[trisIndex++] = tl;
                tris[trisIndex++] = br;
                tris[trisIndex++] = tl;
                tris[trisIndex++] = tr;
                tris[trisIndex++] = br;
            }
        }

        // 2. SKIRT WALLS
        // Each loop connects the top edge (grid index) to the bottom edge (skirt index)

        // South Wall (z = 0)
        // Top indices: x * res | Bottom indices: terrainVertCount + x
        int skirtStart = res * res;
        for (int x = 0; x < res - 1; x++)
        {
            int tL = x * res;
            int tR = (x + 1) * res;
            int bL = skirtStart + x;
            int bR = skirtStart + x + 1;

            tris[trisIndex++] = tL;
            tris[trisIndex++] = bR;
            tris[trisIndex++] = tR;
            tris[trisIndex++] = tL;
            tris[trisIndex++] = bL;
            tris[trisIndex++] = bR;
        }

        // North Wall (z = res - 1)
        // Top indices: x * res + (res - 1) | Bottom indices: skirtStart + res + x
        skirtStart += res;
        for (int x = 0; x < res - 1; x++)
        {
            int tL = x * res + (res - 1);
            int tR = (x + 1) * res + (res - 1);
            int bL = skirtStart + x;
            int bR = skirtStart + x + 1;

            tris[trisIndex++] = tL;
            tris[trisIndex++] = tR;
            tris[trisIndex++] = bR;
            tris[trisIndex++] = tL;
            tris[trisIndex++] = bR;
            tris[trisIndex++] = bL;
        }

        // West Wall (x = 0)
        // Top indices: z | Bottom indices: skirtStart + res + z
        skirtStart += res;
        for (int z = 0; z < res - 1; z++)
        {
            int tB = z;
            int tT = z + 1;
            int bB = skirtStart + z;
            int bT = skirtStart + z + 1;

            tris[trisIndex++] = tB;
            tris[trisIndex++] = tT;
            tris[trisIndex++] = bT;
            tris[trisIndex++] = tB;
            tris[trisIndex++] = bT;
            tris[trisIndex++] = bB;
        }

        // East Wall (x = res - 1)
        // Top indices: (res - 1) * res + z | Bottom indices: skirtStart + res + z
        skirtStart += res;
        for (int z = 0; z < res - 1; z++)
        {
            int tB = (res - 1) * res + z;
            int tT = (res - 1) * res + z + 1;
            int bB = skirtStart + z;
            int bT = skirtStart + z + 1;

            tris[trisIndex++] = tB;
            tris[trisIndex++] = bB;
            tris[trisIndex++] = bT;
            tris[trisIndex++] = tB;
            tris[trisIndex++] = bT;
            tris[trisIndex++] = tT;
        }
    }

    private float GetVertexElevation(int vx, int vz)
    {
        int maxHeight = 0;
        int globalStartX = coord.x * chunkSize;
        int globalStartZ = coord.y * chunkSize;

        for (int tx = vx - 1; tx <= vx; tx++)
        {
            for (int tz = vz - 1; tz <= vz; tz++)
            {
                bool foundTile = generator.GetTileAt(
                    globalStartX + tx,
                    globalStartZ + tz,
                    out TileMeshStruct outTile
                );
                if (foundTile && outTile.Elevation > maxHeight)
                {
                    maxHeight = outTile.Elevation;
                }
            }
        }
        return maxHeight;
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
