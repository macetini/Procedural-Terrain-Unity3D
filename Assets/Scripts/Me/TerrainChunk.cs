using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TerrainChunk : MonoBehaviour
{
    private const float LOD_SKIRT_HEIGHT = 0.2f;
    private const string MESH_NAME = "TerrainChunk";

    [Header("Material Settings")]
    public Material terrainMaterial;

    [Header("Debug Settings")]
    public bool DrawGizmos = false;

    private TerrainChunksGenerator generator;
    private Vector2Int coord;
    private int chunkSize;
    private float tileSize;
    private float elevationStepHeight;
    private int maxElevation;
    private int currentStep = -1;
    private float chunkBoundSize;
    private Bounds chunkBounds;
    private MeshRenderer rendererReference;

    void Awake()
    {
        // Get the bounds of the mesh (this was calculated in BuildProceduralMesh)
        chunkBounds = GetComponent<MeshRenderer>().bounds;
        rendererReference = GetComponent<MeshRenderer>();
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

        UpdateLOD(true);
    }

    public void UpdateLOD(bool force = false)
    {
        int targetStep = GetTargetStep();

        // Only rebuild if the LOD changed OR we are forcing it (initial build)
        if (targetStep != currentStep || force)
        {
            currentStep = targetStep;
            BuildProceduralMesh();
        }
    }

    private int GetTargetStep()
    {
        float xPos = transform.position.x + chunkBoundSize * 0.5f;
        float zPos = transform.position.z - chunkBoundSize;
        Vector3 chunkCenter = new(xPos, 0, zPos);

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
        List<Vector3> vertices = new();
        List<Vector2> uvs = new();
        GenerateVerticesAndUVs(vertices, uvs);

        List<int> triangles = new();
        int vertsPerRow = (chunkSize / currentStep) + 1;
        GenerateTriangles(triangles, vertsPerRow);

        Mesh mesh = new() { name = $"{MESH_NAME}_{coord.x}_{coord.y}" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        // Expand bounds by a few units so they don't pop out too early
        mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * 5f);

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;

        if (terrainMaterial != null)
        {
            GetComponent<MeshRenderer>().material = terrainMaterial;
        }
    }

    private void GenerateVerticesAndUVs(List<Vector3> vertices, List<Vector2> UVs)
    {
        for (int x = 0; x <= chunkSize; x += currentStep)
        {
            for (int z = 0; z <= chunkSize; z += currentStep)
            {
                float height = GetVertexElevation(x, z);

                // Convert to world-relative float position
                float xPos = x * tileSize;
                float zPos = z * tileSize;
                float yPos = height * elevationStepHeight;

                // If we are on ANY edge and OVER LOD 0, pull the vertex down slightly.
                // This creates a "seam-cover" that hides the T-junctions.
                if (currentStep > 1 && (x == 0 || x == chunkSize || z == 0 || z == chunkSize))
                {
                    yPos -= LOD_SKIRT_HEIGHT;
                }

                vertices.Add(new Vector3(xPos, yPos, zPos));
                UVs.Add(new Vector2((float)x / chunkSize, (float)z / chunkSize));
            }
        }
    }

    private void GenerateTriangles(List<int> triangles, int vertsPerRow)
    {
        for (int x = 0; x < vertsPerRow - 1; x++)
        {
            for (int z = 0; z < vertsPerRow - 1; z++)
            {
                int bl = x * vertsPerRow + z;
                int tl = bl + 1;
                int br = (x + 1) * vertsPerRow + z;
                int tr = br + 1;

                triangles.Add(bl);
                triangles.Add(tl);
                triangles.Add(br);
                triangles.Add(tl);
                triangles.Add(tr);
                triangles.Add(br);
            }
        }
    }

    private float GetVertexElevation(int vx, int vz)
    {
        int maxHeight = 0;
        // Calculate the Global X/Z of this corner
        int globalStartX = coord.x * chunkSize;
        int globalStartZ = coord.y * chunkSize;

        // Look at the 4 tiles touching this vertex
        for (int tx = vx - 1; tx <= vx; tx++)
        {
            for (int tz = vz - 1; tz <= vz; tz++)
            {
                // PEEK INTO THE GLOBAL DATA
                TileMeshData tile = generator.GetTileAt(globalStartX + tx, globalStartZ + tz);
                if (tile != null && tile.Elevation > maxHeight)
                {
                    maxHeight = tile.Elevation;
                }
            }
        }
        return maxHeight;
    }

    public void UpdateVisibility(Plane[] planes)
    {
        bool isVisible = GeometryUtility.TestPlanesAABB(planes, rendererReference.bounds);
        rendererReference.enabled = isVisible;
    }

    void OnDrawGizmosSelected()
    {
        // Safety check: ensure generator and data exist
        if (!DrawGizmos || generator == null)
            return;

        // Gizmos now represent the Logic Tiles by asking the generator for data
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                int globalX = (coord.x * chunkSize) + x;
                int globalZ = (coord.y * chunkSize) + z;

                TileMeshData tile = generator.GetTileAt(globalX, globalZ);
                if (tile == null)
                    continue;

                Gizmos.color = Color.HSVToRGB(tile.Elevation / (float)maxElevation, 0.7f, 1f);
                Vector3 center = new(
                    transform.position.x + (x * tileSize),
                    tile.Elevation * elevationStepHeight,
                    transform.position.z + (z * tileSize)
                );
                Gizmos.DrawWireCube(center, new Vector3(tileSize, 0.1f, tileSize));
            }
        }
    }
}
