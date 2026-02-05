using UnityEngine;

public class TerrainChunkProcessor
{
    // Terrain data
    private TerrainChunksGenerator generator;
    private TerrainDataMap.ChunkNeighborGrids neighbors;

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
        neighbors = generator.TerrainData.GetNeighborGrids(chunkCoord);
        this.resolutionStep = resolutionStep;
        resolution = (chunkSize / resolutionStep) + 1;

        int gridVertCount = resolution * resolution;
        int totalVerts = gridVertCount + (resolution * 4);

        // --- OPTIMIZATION: ARRAY REUSE ---
        if (vertices == null || vertices.Length != totalVerts)
        {
            vertices = new Vector3[totalVerts];
            uvs = new Vector2[totalVerts];
            normals = new Vector3[totalVerts];
        }

        // --- OPTIMIZATION: HEIGHT CACHE REUSE ---
        int cacheRes = resolution + 2;
        int totalCacheSize = cacheRes * cacheRes;
        if (heightCache1D == null || heightCache1D.Length != totalCacheSize)
        {
            heightCache1D = new float[totalCacheSize];
        }
        FillHeightCache();
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
        // Internal - Use neighbors.Center
        if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
            return neighbors.Center[x, z].Elevation;

        // Cardinal Neighbors - Redirect to neighbors struct
        // Note the use of ?.Elevation to safely handle missing neighbors
        if (x < 0 && z >= 0 && z < chunkSize)
            return (neighbors.W != null)
                ? neighbors.W[chunkSize + x, z].Elevation
                : neighbors.Center[0, z].Elevation;

        if (x >= chunkSize && z >= 0 && z < chunkSize)
            return (neighbors.E != null)
                ? neighbors.E[x - chunkSize, z].Elevation
                : neighbors.Center[chunkSize - 1, z].Elevation;

        if (z < 0 && x >= 0 && x < chunkSize)
            return (neighbors.S != null)
                ? neighbors.S[x, chunkSize + z].Elevation
                : neighbors.Center[x, 0].Elevation;

        if (z >= chunkSize && x >= 0 && x < chunkSize)
            return (neighbors.N != null)
                ? neighbors.N[x, z - chunkSize].Elevation
                : neighbors.Center[x, chunkSize - 1].Elevation;

        // Diagonal Neighbors
        if (x < 0 && z < 0)
            return (neighbors.SW != null)
                ? neighbors.SW[chunkSize + x, chunkSize + z].Elevation
                : neighbors.Center[0, 0].Elevation;

        if (x < 0 && z >= chunkSize)
            return (neighbors.NW != null)
                ? neighbors.NW[chunkSize + x, z - chunkSize].Elevation
                : neighbors.Center[0, chunkSize - 1].Elevation;

        if (x >= chunkSize && z >= chunkSize)
            return (neighbors.NE != null)
                ? neighbors.NE[x - chunkSize, z - chunkSize].Elevation
                : neighbors.Center[chunkSize - 1, chunkSize - 1].Elevation;

        if (x >= chunkSize && z < 0)
            return (neighbors.SE != null)
                ? neighbors.SE[x - chunkSize, chunkSize + z].Elevation
                : neighbors.Center[chunkSize - 1, 0].Elevation;

        return neighbors.Center[0, 0].Elevation;
    }

    public void GenerateGeometryData()
    {
        int i = 0;
        float invSize = 1f / chunkSize;
        int cacheStride = resolution + 2;

        // 1. MAIN GRID GENERATION
        for (int x = 0; x < resolution; x++)
        {
            int gx = x * resolutionStep;
            for (int z = 0; z < resolution; z++)
            {
                int gz = z * resolutionStep;

                // We use GetBlendedElevation instead of the raw height.
                // This "bevels" the edges of your steps by averaging neighbor heights.
                float h = heightCache1D[(x + 1) * cacheStride + (z + 1)] * elevationStepHeight;

                vertices[i] = new Vector3(gx * tileSize, h, gz * tileSize);
                uvs[i] = new Vector2(gx * invSize, gz * invSize);
                i++;
            }
        }

        // 2. SKIRT GENERATION
        // Index 1 in the cache is local 0.
        // Index res in the cache is local chunkSize.
        int skirtIdx = resolution * resolution;

        // South (z=0) -> cache z index is 1
        for (int x = 0; x < resolution; x++)
        {
            float h = heightCache1D[(x + 1) * cacheStride + 1] * elevationStepHeight;
            vertices[skirtIdx++] = new Vector3(x * resolutionStep * tileSize, h - skirtDepth, 0);
        }
        // North (z=chunkSize) -> cache z index is resolution
        for (int x = 0; x < resolution; x++)
        {
            float h = heightCache1D[(x + 1) * cacheStride + resolution] * elevationStepHeight;
            vertices[skirtIdx++] = new Vector3(
                x * resolutionStep * tileSize,
                h - skirtDepth,
                chunkBoundSize
            );
        }
        // West (x=0) -> cache x index is 1
        for (int z = 0; z < resolution; z++)
        {
            float h = heightCache1D[1 * cacheStride + z + 1] * elevationStepHeight;
            vertices[skirtIdx++] = new Vector3(0, h - skirtDepth, z * resolutionStep * tileSize);
        }
        // East (x=chunkSize) -> cache x index is resolution
        for (int z = 0; z < resolution; z++)
        {
            float h = heightCache1D[resolution * cacheStride + z + 1] * elevationStepHeight;
            vertices[skirtIdx++] = new Vector3(
                chunkBoundSize,
                h - skirtDepth,
                z * resolutionStep * tileSize
            );
        }
    }

    public void CalculateNormals()
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

        // Skirt Normals logic remains same as before
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
