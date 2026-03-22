using UnityEngine;

public class TerrainChunkProcessor
{
    // Terrain data
    private TerrainChunksGenerator generator;
    private ChunkNeighborStruct neighbors;

    // Terrain settings
    private int chunkSize;
    private float tileSize;
    private float elevationStepHeight;
    private float skirtDepth;

    // Mesh data
    private Vector3[] vertices;
    private Vector2[] uvs;
    private Vector3[] normals;
    private float[] heightCache1D; // Added: Reuse the height cache array

    // Calculations
    private int resolution;
    private int resolutionStep;
    private float chunkBoundSize;

    // Flags
    private int lastTriangleCount = -1;

    public void Init(TerrainChunksGenerator generator)
    {
        this.generator = generator;

        chunkSize = generator.chunkSize;
        tileSize = generator.tileSize;
        chunkBoundSize = chunkSize * tileSize;

        elevationStepHeight = generator.elevationStepHeight;
        skirtDepth = generator.skirtDepth;
    }

    public void BuildMeshData(int resolutionStep, Vector2Int chunkCoord)
    {
        InitializeNeighbors(chunkCoord);
        InitializeResolution(resolutionStep);
        InitializeMeshData();
        InitializeHeightCache();
        FillHeightCache();
    }

    private void InitializeNeighbors(Vector2Int chunkCoord)
    {
        neighbors = generator.TerrainDataProcessor.GetNeighborGrids(chunkCoord);
    }

    private void InitializeResolution(int resolutionStep)
    {
        this.resolutionStep = resolutionStep;
        resolution = (chunkSize / resolutionStep) + 1;
    }

    private void InitializeMeshData()
    {
        int gridVertCount = resolution * resolution;
        int totalVerts = gridVertCount + (resolution * 4);

        if (vertices == null || vertices.Length != totalVerts)
        {
            vertices = new Vector3[totalVerts];
            uvs = new Vector2[totalVerts];
            normals = new Vector3[totalVerts];
        }
    }

    private void InitializeHeightCache()
    {
        int cacheRes = resolution + 2;
        int totalCacheSize = cacheRes * cacheRes;
        if (heightCache1D == null || heightCache1D.Length != totalCacheSize)
        {
            heightCache1D = new float[totalCacheSize];
        }
    }

    private void FillHeightCache()
    {
        int cacheStride = resolution + 2;
        for (int x = -1; x <= resolution; x++)
        {
            int rowOffset = (x + 1) * cacheStride;
            for (int z = -1; z <= resolution; z++)
            {
                heightCache1D[rowOffset + z + 1] = GetBlendedElevation(
                    x * resolutionStep,
                    z * resolutionStep
                );
            }
        }
    }

    private float GetBlendedElevation(int lx, int lz)
    {
        float total = 0;
        total += SampleGrid(lx, lz);
        total += SampleGrid(lx - 1, lz);
        total += SampleGrid(lx, lz - 1);
        total += SampleGrid(lx - 1, lz - 1);
        return total * 0.25f;
    }

    private float SampleGrid(int x, int z)
    {
        return neighbors.GetElevation(x, z, chunkSize);
    }

    public void GenerateGeometryData()
    {
        GenerateMainGridVertices();
        GenerateSkirtVertices();
    }

    private void GenerateMainGridVertices()
    {
        int i = 0;
        float invSize = 1f / chunkSize;
        int cacheStride = resolution + 2;

        for (int x = 0; x < resolution; x++)
        {
            int gx = x * resolutionStep;
            for (int z = 0; z < resolution; z++)
            {
                int gz = z * resolutionStep;

                float h = heightCache1D[(x + 1) * cacheStride + (z + 1)] * elevationStepHeight;

                vertices[i] = new Vector3(gx * tileSize, h, gz * tileSize);
                uvs[i] = new Vector2(gx * invSize, gz * invSize);
                i++;
            }
        }
    }

    private void GenerateSkirtVertices()
    {
        int skirtIdx = resolution * resolution;

        GenerateSkirtVerticesSouth(skirtIdx);
        skirtIdx += resolution;

        GenerateSkirtVerticesNorth(skirtIdx);
        skirtIdx += resolution;

        GenerateSkirtVerticesWest(skirtIdx);
        skirtIdx += resolution;

        GenerateSkirtVerticesEast(skirtIdx);
    }

    private void GenerateSkirtVerticesSouth(int skirtIdx)
    {
        for (int x = 0; x < resolution; x++)
        {
            float h = heightCache1D[(x + 1) * (resolution + 2) + 1] * elevationStepHeight;
            vertices[skirtIdx++] = new Vector3(x * resolutionStep * tileSize, h - skirtDepth, 0);
        }
    }

    private void GenerateSkirtVerticesNorth(int skirtIdx)
    {
        for (int x = 0; x < resolution; x++)
        {
            float h = heightCache1D[(x + 1) * (resolution + 2) + resolution] * elevationStepHeight;
            vertices[skirtIdx++] = new Vector3(
                x * resolutionStep * tileSize,
                h - skirtDepth,
                chunkBoundSize
            );
        }
    }

    private void GenerateSkirtVerticesWest(int skirtIdx)
    {
        for (int z = 0; z < resolution; z++)
        {
            float h = heightCache1D[1 * (resolution + 2) + z + 1] * elevationStepHeight;
            vertices[skirtIdx++] = new Vector3(0, h - skirtDepth, z * resolutionStep * tileSize);
        }
    }

    private void GenerateSkirtVerticesEast(int skirtIdx)
    {
        for (int z = 0; z < resolution; z++)
        {
            float h = heightCache1D[resolution * (resolution + 2) + z + 1] * elevationStepHeight;
            vertices[skirtIdx++] = new Vector3(
                chunkBoundSize,
                h - skirtDepth,
                z * resolutionStep * tileSize
            );
        }
    }

    public void CalculateNormals()
    {
        CalculateNormalsBody();
        CalculateNormalsSkirt();
    }

    private void CalculateNormalsBody()
    {
        float vScale = elevationStepHeight;
        float hDist = 2.0f * tileSize * resolutionStep;
        int stride = resolution + 2;

        for (int x = 0; x < resolution; x++)
        {
            int row = (x + 1) * stride;
            for (int z = 0; z < resolution; z++)
            {
                int idx = x * resolution + z;
                int cz = z + 1;

                // Sample 4 directions from the padded height cache
                float hL = heightCache1D[row - stride + cz];
                float hR = heightCache1D[row + stride + cz];
                float hB = heightCache1D[row + cz - 1];
                float hF = heightCache1D[row + cz + 1];

                // Standard Sobel-filter style normal generation
                Vector3 normal = new(hL - hR, 2.0f * (hDist / vScale), hB - hF);
                normals[idx] = normal.normalized;
            }
        }
    }

    private void CalculateNormalsSkirt()
    {
        float centerX = chunkBoundSize * 0.5f;
        for (int n = resolution * resolution; n < vertices.Length; n++)
        {
            Vector3 dir = (vertices[n] - new Vector3(centerX, vertices[n].y, centerX)).normalized;
            normals[n] = new Vector3(dir.x, 0, dir.z);
        }
    }

    public void PopulateMesh(Mesh targetMesh)
    {
        targetMesh.Clear();
        targetMesh.SetVertices(vertices);
        targetMesh.SetUVs(0, uvs);
        targetMesh.SetNormals(normals);

        int[] tris = generator.GetPrecalculatedTriangles(resolution);
        if (lastTriangleCount != tris.Length)
        {
            targetMesh.SetTriangles(tris, 0);
            lastTriangleCount = tris.Length;
        }

        targetMesh.UploadMeshData(false);
    }
}
