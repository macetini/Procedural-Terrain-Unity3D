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

    //private float chunkBoundSize;

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

        //chunkBoundSize = chunkSize * tileSize;
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
        float halfSize = chunkSize * tileSize * 0.5f;
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
        // Calculate exactly how many verts we need (Array is faster than List)
        int vertsPerRow = (chunkSize / CurrentStep) + 1;
        int totalVerts = vertsPerRow * vertsPerRow;

        Vector3[] vertices = new Vector3[totalVerts];
        Vector2[] uvs = new Vector2[totalVerts];

        GenerateVerticesAndUVs(vertices, uvs);

        // Calculate triangles
        int numSquares = vertsPerRow - 1;
        int[] triangles = new int[numSquares * numSquares * 6];
        GenerateTriangles(triangles, vertsPerRow);

        // CLEANUP: Prevent Memory Leak in WebGL
        // If we don't destroy the old mesh, it sits in browser RAM forever.
        if (filterReference.sharedMesh != null)
        {
            Destroy(filterReference.sharedMesh);
        }

        Mesh mesh = new()
        {
            name = $"{MESH_NAME}_{coord.x}_{coord.y}",
            vertices = vertices,
            triangles = triangles,
            uv = uvs,
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Expand bounds slightly to prevent frustum popping at edges
        mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * 2f);

        filterReference.mesh = mesh;

        // OPTIMIZATION: Only update collider for high-detail chunks
        // WebGL hates physics updates; distant chunks don't need colliders.
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
        for (int x = 0; x <= chunkSize; x += CurrentStep)
        {
            for (int z = 0; z <= chunkSize; z += CurrentStep)
            {
                float height = GetVertexElevation(x, z);

                float xPos = x * tileSize;
                float zPos = z * tileSize;
                float yPos = height * elevationStepHeight;

                // Skirt logic to hide gaps at LOD seams
                if (CurrentStep > 1 && (x == 0 || x == chunkSize || z == 0 || z == chunkSize))
                {
                    yPos -= LOD_SKIRT_HEIGHT;
                }

                vertices[i] = new Vector3(xPos, yPos, zPos);
                UVs[i] = new Vector2((float)x / chunkSize, (float)z / chunkSize);
                i++;
            }
        }
    }

    private static void GenerateTriangles(int[] triangles, int vertsPerRow)
    {
        int triIndex = 0;
        for (int x = 0; x < vertsPerRow - 1; x++)
        {
            for (int z = 0; z < vertsPerRow - 1; z++)
            {
                int bl = x * vertsPerRow + z;
                int tl = bl + 1;
                int br = (x + 1) * vertsPerRow + z;
                int tr = br + 1;

                triangles[triIndex++] = bl;
                triangles[triIndex++] = tl;
                triangles[triIndex++] = br;
                triangles[triIndex++] = tl;
                triangles[triIndex++] = tr;
                triangles[triIndex++] = br;
            }
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
            float size = chunkSize * tileSize;
            Vector3 center =
                transform.position
                + new Vector3(size / 2, (maxElevation * elevationStepHeight) / 2, size / 2);
            Vector3 boxSize = new Vector3(size, maxElevation * elevationStepHeight, size);
            checkBounds = new Bounds(center, boxSize);
        }

        IsVisible = GeometryUtility.TestPlanesAABB(planes, checkBounds);
        // Toggle the renderer so the GPU skips hidden chunks
        rendererReference.enabled = IsVisible;
        // Optimization: If it's hidden, stop the script's UpdateLOD checks too
        this.enabled = IsVisible;
    }

    void OnDrawGizmosSelected()
    {
        if (!DrawGizmos || generator == null)
            return;

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
