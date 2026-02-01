using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TerrainChunk : MonoBehaviour
{
    private const float LOD_SKIRT_HEIGHT = 0.0f;
    private const string MESH_NAME = "TerrainChunk";

    [Header("Material Settings")]
    public Material terrainMaterial;

    [Header("Debug Settings")]
    public bool DrawGizmos = false;

    public bool IsVisible { get; private set; } = true;
    public int CurrentStep { get; private set; } = -1;

    private TerrainChunksGenerator generator;
    private Vector2Int coord;
    private int chunkSize;
    private float tileSize;
    private float elevationStepHeight;
    private int maxElevation;
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
        maxElevation = generator.maxElevation;

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
        Vector3 chunkCenter = transform.position + new Vector3(halfSize, 0, halfSize);

        float dist = Vector3.Distance(chunkCenter, generator.cameraReference.transform.position);
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
        int terrainVertCount = resolution * resolution;

        // Each of the 4 edges needs 'resolution' number of vertices for the bottom of the skirt
        int totalVerts = terrainVertCount + (resolution * 4);

        Vector3[] vertices = new Vector3[totalVerts];
        Vector2[] uvs = new Vector2[totalVerts];

        GenerateVerticesAndUVs(vertices, uvs);

        // Each edge segment needs 2 triangles (6 indices). 4 edges * (resolution - 1) segments.
        int terrainTris = (resolution - 1) * (resolution - 1) * 6;
        int skirtTris = (resolution - 1) * 4 * 6;
        int[] triangles = new int[terrainTris + skirtTris];

        GenerateTriangles(triangles, resolution);

        if (filterReference.sharedMesh != null)
            Destroy(filterReference.sharedMesh);

        Mesh mesh = new()
        {
            name = $"{MESH_NAME}_{coord.x}_{coord.y}",
            vertices = vertices,
            triangles = triangles,
            uv = uvs,
        };

        mesh.RecalculateNormals();
        // Force every single normal to point straight up
        Vector3[] normals = new Vector3[vertices.Length];
        for (int n = 0; n < normals.Length; n++)
        {
            normals[n] = Vector3.up;
        }
        mesh.normals = normals;

        mesh.RecalculateBounds();
        mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * 2f);

        filterReference.mesh = mesh;

        // Physics only for LOD 0
        if (CurrentStep == 1)
        {
            colliderReference.enabled = true;
            colliderReference.sharedMesh = mesh;
        }
        else
        {
            colliderReference.enabled = false;
        }
    }

    private void GenerateVerticesAndUVs(Vector3[] vertices, Vector2[] UVs)
    {
        int i = 0;
        // 1. Top Terrain Grid
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

        // 2. Skirt Vertices (Hanging down)
        float skirtDepth = 5.0f;

        // South Edge (z = 0)
        for (int x = 0; x <= chunkSize; x += CurrentStep)
        {
            float h = GetVertexElevation(x, 0) * elevationStepHeight - skirtDepth;
            vertices[i] = new Vector3(x * tileSize, h, 0);
            UVs[i] = new Vector2((float)x / chunkSize, 0); // Explicit UV
            i++;
        }
        // North Edge (z = chunkSize)
        for (int x = 0; x <= chunkSize; x += CurrentStep)
        {
            float h = GetVertexElevation(x, chunkSize) * elevationStepHeight - skirtDepth;
            vertices[i] = new Vector3(x * tileSize, h, chunkSize * tileSize);
            UVs[i] = new Vector2((float)x / chunkSize, 1);
            i++;
        }
        // West Edge (x = 0)
        for (int z = 0; z <= chunkSize; z += CurrentStep)
        {
            float h = GetVertexElevation(0, z) * elevationStepHeight - skirtDepth;
            vertices[i] = new Vector3(0, h, z * tileSize);
            UVs[i] = new Vector2(0, (float)z / chunkSize);
            i++;
        }
        // East Edge (x = chunkSize)
        for (int z = 0; z <= chunkSize; z += CurrentStep)
        {
            float h = GetVertexElevation(chunkSize, z) * elevationStepHeight - skirtDepth;
            vertices[i] = new Vector3(chunkSize * tileSize, h, z * tileSize);
            UVs[i] = new Vector2(1, (float)z / chunkSize);
            i++;
        }
    }

    private static void GenerateTriangles(int[] triangles, int res)
    {
        int triIndex = 0;
        int terrainVertCount = res * res;

        // 1. MAIN TERRAIN GRID
        for (int x = 0; x < res - 1; x++)
        {
            for (int z = 0; z < res - 1; z++)
            {
                int bl = x * res + z;
                int tl = bl + 1;
                int br = (x + 1) * res + z;
                int tr = br + 1;

                triangles[triIndex++] = bl;
                triangles[triIndex++] = tl;
                triangles[triIndex++] = br;
                triangles[triIndex++] = tl;
                triangles[triIndex++] = tr;
                triangles[triIndex++] = br;
            }
        }

        // 2. SKIRT WALLS
        // Each loop connects the top edge (grid index) to the bottom edge (skirt index)

        // South Wall (z = 0)
        // Top indices: x * res | Bottom indices: terrainVertCount + x
        int skirtStart = terrainVertCount;
        for (int x = 0; x < res - 1; x++)
        {
            int tL = x * res;
            int tR = (x + 1) * res;
            int bL = skirtStart + x;
            int bR = skirtStart + x + 1;

            triangles[triIndex++] = tL;
            triangles[triIndex++] = bR;
            triangles[triIndex++] = tR;
            triangles[triIndex++] = tL;
            triangles[triIndex++] = bL;
            triangles[triIndex++] = bR;
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

            triangles[triIndex++] = tL;
            triangles[triIndex++] = tR;
            triangles[triIndex++] = bR;
            triangles[triIndex++] = tL;
            triangles[triIndex++] = bR;
            triangles[triIndex++] = bL;
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

            triangles[triIndex++] = tB;
            triangles[triIndex++] = tT;
            triangles[triIndex++] = bT;
            triangles[triIndex++] = tB;
            triangles[triIndex++] = bT;
            triangles[triIndex++] = bB;
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

            triangles[triIndex++] = tB;
            triangles[triIndex++] = bB;
            triangles[triIndex++] = bT;
            triangles[triIndex++] = tB;
            triangles[triIndex++] = bT;
            triangles[triIndex++] = tT;
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
            float realElevationHight = maxElevation * elevationStepHeight;
            Vector3 center =
                transform.position
                + new Vector3(halfBoundSize, realElevationHight * 0.5f, halfBoundSize);
            Vector3 boxSize = new(chunkBoundSize, realElevationHight, chunkBoundSize);
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
                        outTile.Elevation / (float)maxElevation,
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
