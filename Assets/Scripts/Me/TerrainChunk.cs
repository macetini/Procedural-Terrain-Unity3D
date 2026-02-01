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
        int resolution = (chunkSize / CurrentStep) + 1;
        int gridVertCount = resolution * resolution;

        // Each of the 4 edges needs 'resolution' number of vertices for the bottom of the skirt
        int totalVerts = gridVertCount + (resolution * 4);

        Vector3[] vertices = new Vector3[totalVerts];
        Vector2[] uvs = new Vector2[totalVerts];

        GenerateGeometry(vertices, uvs);

        // Each edge segment needs 2 triangles (6 indices). 4 edges * (resolution - 1) segments.
        int gridTris = (resolution - 1) * (resolution - 1) * 6;
        int skirtTris = (resolution - 1) * 4 * 6;
        int[] tris = new int[gridTris + skirtTris];

        GenerateTriangles(tris, resolution);

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
        };

        // --- NORMAL LOGIC START ---
        // Basic recalculation for the flat parts
        mesh.RecalculateNormals();

        // Force every single normal to point straight up
        Vector3[] normals = new Vector3[vertices.Length];
        for (int n = 0; n < normals.Length; n++)
        {
            normals[n] = Vector3.up;
        }
        mesh.normals = normals;
        // --- NORMAL LOGIC END ---

        mesh.RecalculateBounds();
        mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * boundPadding);

        filterReference.mesh = mesh;

        // Physics only for LOD 0
        colliderReference.enabled = CurrentStep == 1;
        if (colliderReference.enabled)
        {
            colliderReference.sharedMesh = mesh;
        }
    }

    private void GenerateGeometry(Vector3[] verts, Vector2[] uvs)
    {
        int i = 0;

        // 1. Generate Main Grid
        for (int x = 0; x <= chunkSize; x += CurrentStep)
        {
            for (int z = 0; z <= chunkSize; z += CurrentStep)
            {
                float h = GetVertexElevation(x, z) * elevationStepHeight;
                verts[i] = new Vector3(x * tileSize, h, z * tileSize);
                uvs[i] = new Vector2((float)x / chunkSize, (float)z / chunkSize);
                i++;
            }
        }

        // 2. Generate Skirt (Reusing edge logic)
        // South & North
        for (int x = 0; x <= chunkSize; x += CurrentStep)
        {
            verts[i] = new Vector3(
                x * tileSize,
                GetVertexElevation(x, 0) * elevationStepHeight - skirtDepth,
                0
            );
            uvs[i++] = new Vector2((float)x / chunkSize, 0);

            verts[i] = new Vector3(
                x * tileSize,
                GetVertexElevation(x, chunkSize) * elevationStepHeight - skirtDepth,
                chunkSize * tileSize
            );
            uvs[i++] = new Vector2((float)x / chunkSize, 1);
        }
        // West & East
        for (int z = 0; z <= chunkSize; z += CurrentStep)
        {
            verts[i] = new Vector3(
                0,
                GetVertexElevation(0, z) * elevationStepHeight - skirtDepth,
                z * tileSize
            );
            uvs[i++] = new Vector2(0, (float)z / chunkSize);

            verts[i] = new Vector3(
                chunkSize * tileSize,
                GetVertexElevation(chunkSize, z) * elevationStepHeight - skirtDepth,
                z * tileSize
            );
            uvs[i++] = new Vector2(1, (float)z / chunkSize);
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
    }
}
