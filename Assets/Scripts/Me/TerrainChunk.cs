using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TerrainChunk : MonoBehaviour
{
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

        float dist = Vector3.Distance(chunkCenter, generator.playerCamera.position);
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
        // 1. GENERATE VERTICES AND UVs
        List<Vector3> vertices = new();
        List<Vector2> uvs = new();
        GenerateVerticesAndUVs(vertices, uvs);

        // 2. GENERATE TRIANGLES
        List<int> triangles = new();
        int vertsPerRow = (chunkSize / currentStep) + 1;
        GenerateTriangles(triangles, vertsPerRow);

        // 3. ADD SKIRT
        //float skirtHeight = 10; // Adjust as needed
        //AddSkirt(vertices, uvs, triangles, skirtHeight);

        // 4. CREATE THE MESH
        Mesh mesh = new() { name = "TerrainChunk" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // 5. Assign to components
        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;

        // 6. Assign material
        if (terrainMaterial != null)
        {
            GetComponent<MeshRenderer>().material = terrainMaterial;
        }
    }

    private void GenerateVerticesAndUVs(List<Vector3> vertices, List<Vector2> UVs)
    {
        float edgeDrop = 0.2f; // A small dip to hide the gap

        for (int x = 0; x <= chunkSize; x += currentStep)
        {
            for (int z = 0; z <= chunkSize; z += currentStep)
            {
                float height = GetVertexElevation(x, z);

                // Convert to world-relative float position
                float xPos = x * tileSize;
                float zPos = z * tileSize;
                float yPos = height * elevationStepHeight;

                // --- THE TRICK ---
                // If we are on ANY edge, pull the vertex down slightly.
                // This creates a "seam-cover" that hides the T-junctions.
                if (currentStep > 1 && (x == 0 || x == chunkSize || z == 0 || z == chunkSize))
                {
                    yPos -= edgeDrop;
                }

                vertices.Add(new Vector3(xPos, yPos, zPos));
                UVs.Add(new Vector2((float)x / chunkSize, (float)z / chunkSize));
            }
        }
    }

    // OLD
    private void GenerateVerticesAndUVs_OLD(List<Vector3> vertices, List<Vector2> UVs)
    {
        for (int x = 0; x <= chunkSize; x += currentStep)
        {
            for (int z = 0; z <= chunkSize; z += currentStep)
            {
                float height = GetVertexElevation(x, z);
                vertices.Add(new Vector3(x * tileSize, height * elevationStepHeight, z * tileSize));
                UVs.Add(new Vector2((float)x / chunkSize, (float)z / chunkSize));
            }
        }
    }

    // OLD
    private void AddSkirt_DISABLED(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        float skirtHeight
    )
    {
        int surfaceVertCount = vertices.Count;
        float worldChunkSize = chunkSize * tileSize;

        // We loop through the surface vertices and find the ones on the 4 edges
        for (int i = 0; i < surfaceVertCount; i++)
        {
            Vector3 v = vertices[i];

            // Is this vertex on the edge of the chunk?
            bool onLeft = v.x == 0;
            bool onRight = v.x == worldChunkSize;
            bool onBack = v.z == 0;
            bool onForward = v.z == worldChunkSize;

            if (onLeft || onRight || onBack || onForward)
            {
                // 1. Add the "bottom" vertex
                int topIndex = i;
                int bottomIndex = vertices.Count;
                vertices.Add(new Vector3(v.x, v.y - skirtHeight, v.z));
                uvs.Add(uvs[i]);

                // 2. Find the NEXT vertex along the edge to form a quad
                // This is a bit complex due to indexing, so we look for a neighbor
                // that is also an edge vertex within 'currentStep' distance.
                for (int j = i + 1; j < surfaceVertCount; j++)
                {
                    Vector3 v2 = vertices[j];
                    float dist = Vector3.Distance(
                        new Vector3(v.x, 0, v.z),
                        new Vector3(v2.x, 0, v2.z)
                    );

                    // If this is the immediate next neighbor on the same edge
                    if (dist <= (currentStep * tileSize) + 0.1f)
                    {
                        bool sameEdge =
                            (onLeft && v2.x == 0)
                            || (onRight && v2.x == worldChunkSize)
                            || (onBack && v2.z == 0)
                            || (onForward && v2.z == worldChunkSize);

                        if (sameEdge)
                        {
                            int nextTop = j;
                            int nextBottom = vertices.Count; // This will be the next one we haven't added yet

                            // We will add the triangles once we find the next pair
                            // For simplicity in this logic, we stitch as we go:
                            // (CurrentTop, NextTop, CurrentBottom) and (NextTop, NextBottom, CurrentBottom)
                            // Note: Depending on the edge, you might need to flip winding order for normals
                            triangles.Add(topIndex);
                            triangles.Add(nextTop);
                            triangles.Add(bottomIndex);
                        }
                    }
                }
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
