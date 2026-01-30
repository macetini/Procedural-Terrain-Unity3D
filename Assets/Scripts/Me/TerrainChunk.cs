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
            return 4;
        if (dist > generator.lodDist1)
            return 2;
        return 1;
    }

    private void BuildProceduralMesh()
    {
        float skirtHeight = 2.0f; // How far down the "curtain" goes
        Mesh mesh = new() { name = "TerrainChunk" };
        List<Vector3> vertices = new();
        List<Vector2> uvs = new();
        List<int> triangles = new();

        // 1. GENERATE VERTICES AND UVs
        for (int x = 0; x <= chunkSize; x += currentStep)
        {
            for (int z = 0; z <= chunkSize; z += currentStep)
            {
                float height = GetVertexElevation(x, z);
                vertices.Add(new Vector3(x * tileSize, height * elevationStepHeight, z * tileSize));
                uvs.Add(new Vector2((float)x / chunkSize, (float)z / chunkSize));
            }
        }

        // 2. GENERATE TRIANGLES
        // 3. Generate Triangles with adjusted indexing
        int vertsPerRow = (chunkSize / currentStep) + 1;
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

        // --- 3. GENERATE SKIRTS (The Fix) ---
        // We add a loop to create vertical walls on the 4 edges
        AddSkirt(vertices, triangles, uvs, vertsPerRow, skirtHeight);

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Assign to components
        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;

        if (terrainMaterial != null)
        {
            GetComponent<MeshRenderer>().material = terrainMaterial;
        }
    }

    private void AddSkirt(
        List<Vector3> verts,
        List<int> tris,
        List<Vector2> uvs,
        int vPerRow,
        float sHeight
    )
    {
        int surfaceVertCount = verts.Count;

        // This is a simplified logic:
        // You essentially duplicate the edge vertices,
        // set their Y position to (OriginalY - sHeight),
        // and create triangles between the original edge and the new lower edge.

        // For a cleaner Martian look, would you like the full Skirt helper method,
        // or should we try to fix the "T-Junctions" by forcing the edges
        // to always stay at LOD 0?
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
