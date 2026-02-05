using UnityEngine;

public class TerrainChunkProcessor
{
    private const string MESH_NAME = "TerrainChunk";

    // Mesh references
    //private MeshRenderer rendererReference;
    private int lastTriangleCount = -1;

    // Terrain data
    private TerrainDataMap.ChunkNeighborGrids neighbors;

    // Terrain settings
    private int chunkSize;
    private float tileSize;
    private float elevationStepHeight;
    private float skirtDepth;

    // Cache data
    private Vector3[] vertices;
    private Vector2[] uvs;
    private Vector3[] normals;

    //
    private float[] heightCache1D; // Added: Reuse the height cache array

    // Calculations
    private int resolution;
    private int resolutionStep;
    private float chunkBoundSize;

    public void SetDimensions(
        int chunkSize,
        float tileSize,
        float elevationStepHeight,
        float skirtDepth
    )
    {
        this.chunkSize = chunkSize;
        this.tileSize = tileSize;
        chunkBoundSize = chunkSize * tileSize;

        this.elevationStepHeight = elevationStepHeight;
        this.skirtDepth = skirtDepth;
    }

    public void BuildMeshData(int resolutionStep, TerrainDataMap.ChunkNeighborGrids neighbors)
    {
        //
        this.resolutionStep = resolutionStep;
        this.neighbors = neighbors;

        resolution = (chunkSize / resolutionStep) + 1;
        //

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

        // --- OPTIMIZATION: MESH REUSE ---
        /*
        if (filterReference.sharedMesh == null)
        {
            filterReference.sharedMesh = new Mesh { name = MESH_NAME }; //new Mesh { name = $"{MESH_NAME}_{coord.x}_{coord.y}" };
            filterReference.sharedMesh.MarkDynamic();
        }
        mesh = filterReference.sharedMesh;
        */

        CacheHeights();
    }

    public void CacheHeights()
    {
        // Populate Height Cache using the new Generator Fast-Lookup
        int cacheStride = resolution + 2;
        for (int x = -1; x <= resolution; x++)
        {
            int rowOffset = (x + 1) * cacheStride; // Calculate once per row
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
        int size = chunkSize; // TODO Change to direct refrence

        // 1. Internal - Use neighbors.Center
        if (x >= 0 && x < size && z >= 0 && z < size)
            return neighbors.Center[x, z].Elevation;

        // 2. Cardinal Neighbors - Redirect to neighbors struct
        // Note the use of ?.Elevation to safely handle missing neighbors
        if (x < 0 && z >= 0 && z < size)
            return (neighbors.W != null)
                ? neighbors.W[size + x, z].Elevation
                : neighbors.Center[0, z].Elevation;

        if (x >= size && z >= 0 && z < size)
            return (neighbors.E != null)
                ? neighbors.E[x - size, z].Elevation
                : neighbors.Center[size - 1, z].Elevation;

        if (z < 0 && x >= 0 && x < size)
            return (neighbors.S != null)
                ? neighbors.S[x, size + z].Elevation
                : neighbors.Center[x, 0].Elevation;

        if (z >= size && x >= 0 && x < size)
            return (neighbors.N != null)
                ? neighbors.N[x, z - size].Elevation
                : neighbors.Center[x, size - 1].Elevation;

        // 3. Diagonal Neighbors
        if (x < 0 && z < 0)
            return (neighbors.SW != null)
                ? neighbors.SW[size + x, size + z].Elevation
                : neighbors.Center[0, 0].Elevation;

        if (x < 0 && z >= size)
            return (neighbors.NW != null)
                ? neighbors.NW[size + x, z - size].Elevation
                : neighbors.Center[0, size - 1].Elevation;

        if (x >= size && z >= size)
            return (neighbors.NE != null)
                ? neighbors.NE[x - size, z - size].Elevation
                : neighbors.Center[size - 1, size - 1].Elevation;

        if (x >= size && z < 0)
            return (neighbors.SE != null)
                ? neighbors.SE[x - size, size + z].Elevation
                : neighbors.Center[size - 1, 0].Elevation;

        return neighbors.Center[0, 0].Elevation;
    }

    public void GenerateGeometry()
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

                // ANALYSIS: We use GetBlendedElevation instead of the raw height.
                // This "bevels" the edges of your steps by averaging neighbor heights.
                float h = heightCache1D[(x + 1) * cacheStride + (z + 1)] * elevationStepHeight;

                vertices[i] = new Vector3(gx * tileSize, h, gz * tileSize);
                uvs[i] = new Vector2(gx * invSize, gz * invSize);
                i++;
            }
        }

        // 2. SKIRT GENERATION

        // 2. SKIRTS
        // Note: We use the exact same cache indices for the top of the skirts
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

    public void ConstructMesh(Mesh targetMesh, int[] tris)
    {
        targetMesh.Clear();
        targetMesh.SetVertices(vertices);
        targetMesh.SetUVs(0, uvs);
        targetMesh.SetNormals(normals);

        if (lastTriangleCount != tris.Length)
        {
            targetMesh.SetTriangles(tris, 0);
            lastTriangleCount = tris.Length;
        }
        targetMesh.UploadMeshData(false);
    }
}
