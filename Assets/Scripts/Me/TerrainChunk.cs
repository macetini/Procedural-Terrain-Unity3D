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

    private TileMeshData[,] gridTilesData;

    public void Build(TerrainChunksGenerator gen, Vector2Int chunkCoord)
    {
        generator = gen;
        coord = chunkCoord;

        chunkSize = generator.chunkSize;
        tileSize = generator.tileSize;
        elevationStepHeight = generator.elevationStepHeight;
        maxElevation = generator.maxElevation;

        // Note: We don't call GenerateRawGridData anymore!
        // The data is already in the generator.
        BuildProceduralMesh();
    }

    private void BuildProceduralMesh()
    {
        Mesh mesh = new();

        List<Vector3> vertices = new();
        List<Vector2> uvs = new();
        List<int> triangles = new();

        // 1. GENERATE VERTICES AND UVs
        for (int x = 0; x <= chunkSize; x++)
        {
            for (int z = 0; z <= chunkSize; z++)
            {
                float height = GetVertexElevation(x, z);
                vertices.Add(new Vector3(x * tileSize, height * elevationStepHeight, z * tileSize));

                // UV mapping: normalizes the coordinates between 0 and 1
                uvs.Add(new Vector2((float)x / chunkSize, (float)z / chunkSize));
            }
        }

        // 2. GENERATE TRIANGLES
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                int bottomLeft = x * (chunkSize + 1) + z;
                int topLeft = bottomLeft + 1;
                int bottomRight = (x + 1) * (chunkSize + 1) + z;
                int topRight = bottomRight + 1;

                triangles.Add(bottomLeft);
                triangles.Add(topLeft);
                triangles.Add(bottomRight);

                triangles.Add(topLeft);
                triangles.Add(topRight);
                triangles.Add(bottomRight);
            }
        }

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
        if (!DrawGizmos || gridTilesData == null)
        {
            return;
        }

        // Gizmos now represent the Logic Tiles, not the Mesh itself
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                TileMeshData tile = gridTilesData[x, z];
                Gizmos.color = Color.HSVToRGB(tile.Elevation / (float)maxElevation, 0.7f, 1f);
                Vector3 center = new(
                    x * tileSize + tileSize / 2,
                    tile.Elevation * elevationStepHeight,
                    z * tileSize + tileSize / 2
                );
                Gizmos.DrawWireCube(center, new Vector3(tileSize, 0.1f, tileSize));
            }
        }
    }
}
