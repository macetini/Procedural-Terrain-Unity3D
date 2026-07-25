using System;
using ProceduralTerrain.Processing.Data;
using UnityEngine;

namespace ProceduralTerrain.Processing
{
    internal class DataProcessor
    {
        // Terrain data
        private Func<Vector2Int, NeighborStruct> neighborProvider;
        private Func<int, int[]> triangleProvider;
        private NeighborStruct neighbors;

        // Terrain settings
        private int chunkSize;
        private float tileSize;
        private float elevationStepHeight;
        private float skirtDepth;

        // Mesh data
        private Vector3[] vertices;
        private Vector2[] uvs;
        private Vector3[] normals;
        private float[] heightCache1D; // Reused height cache array

        // Calculations
        private int resolution;
        private int resolutionStep;
        private float chunkBoundSize;

        // Flags
        private int lastTriangleCount = -1;

        public void Init(
            RuntimeSettings settings,
            Func<Vector2Int, NeighborStruct> neighborProvider,
            Func<int, int[]> triangleProvider
        )
        {
            this.neighborProvider = neighborProvider;
            this.triangleProvider = triangleProvider;

            chunkSize = settings.ChunkSize;
            tileSize = settings.TileSize;
            chunkBoundSize = chunkSize * tileSize;

            elevationStepHeight = settings.ElevationStepHeight;
            skirtDepth = settings.SkirtDepth;

            lastTriangleCount = -1;
        }

        public void BuildMeshData(int resolutionStepLocal, Vector2Int chunkCoord)
        {
            InitializeNeighbors(chunkCoord);
            InitializeResolution(resolutionStepLocal);
            InitializeMeshData();
            InitializeHeightCache();
            FillHeightCache();
        }

        private void InitializeNeighbors(Vector2Int chunkCoord)
        {
            neighbors = neighborProvider(chunkCoord);
        }

        private void InitializeResolution(int resolutionStep)
        {
            this.resolutionStep = resolutionStep;
            resolution = chunkSize / resolutionStep + 1;
        }

        private void InitializeMeshData()
        {
            var gridVertCount = resolution * resolution;
            var totalVerts = gridVertCount + (resolution * 4);

            if (vertices != null && vertices.Length == totalVerts)
            {
                return;
            }
            
            vertices = new Vector3[totalVerts];
            uvs = new Vector2[totalVerts];
            normals = new Vector3[totalVerts];
        }

        private void InitializeHeightCache()
        {
            var cacheRes = resolution + 2;
            var totalCacheSize = cacheRes * cacheRes;
            if (heightCache1D == null || heightCache1D.Length != totalCacheSize)
            {
                heightCache1D = new float[totalCacheSize];
            }
        }

        private void FillHeightCache()
        {
            var cacheStride = resolution + 2;
            for (var x = -1; x <= resolution; x++)
            {
                var rowOffset = (x + 1) * cacheStride;
                for (var z = -1; z <= resolution; z++)
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
            var i = 0;
            var invSize = 1f / chunkSize;
            var cacheStride = resolution + 2;

            for (var x = 0; x < resolution; x++)
            {
                var gx = x * resolutionStep;
                for (var z = 0; z < resolution; z++)
                {
                    var gz = z * resolutionStep;

                    var h = heightCache1D[(x + 1) * cacheStride + z + 1] * elevationStepHeight;

                    vertices[i] = new Vector3(gx * tileSize, h, gz * tileSize);
                    uvs[i] = new Vector2(gx * invSize, gz * invSize);
                    i++;
                }
            }
        }

        private void GenerateSkirtVertices()
        {
            var skirtIdx = resolution * resolution;

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
            for (var x = 0; x < resolution; x++)
            {
                var h = heightCache1D[(x + 1) * (resolution + 2) + 1] * elevationStepHeight;
                vertices[skirtIdx++] = new Vector3(
                    x * resolutionStep * tileSize,
                    h - skirtDepth,
                    0
                );
            }
        }

        private void GenerateSkirtVerticesNorth(int skirtIdx)
        {
            for (var x = 0; x < resolution; x++)
            {
                var h =
                    heightCache1D[(x + 1) * (resolution + 2) + resolution] * elevationStepHeight;
                vertices[skirtIdx++] = new Vector3(
                    x * resolutionStep * tileSize,
                    h - skirtDepth,
                    chunkBoundSize
                );
            }
        }

        private void GenerateSkirtVerticesWest(int skirtIdx)
        {
            for (var z = 0; z < resolution; z++)
            {
                var h = heightCache1D[1 * (resolution + 2) + z + 1] * elevationStepHeight;
                vertices[skirtIdx++] = new Vector3(
                    0,
                    h - skirtDepth,
                    z * resolutionStep * tileSize
                );
            }
        }

        private void GenerateSkirtVerticesEast(int skirtIdx)
        {
            for (var z = 0; z < resolution; z++)
            {
                var h =
                    heightCache1D[resolution * (resolution + 2) + z + 1] * elevationStepHeight;
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
            var vScale = elevationStepHeight;
            var hDist = 2.0f * tileSize * resolutionStep;
            var stride = resolution + 2;

            for (var x = 0; x < resolution; x++)
            {
                var row = (x + 1) * stride;
                for (var z = 0; z < resolution; z++)
                {
                    var idx = x * resolution + z;
                    var cz = z + 1;

                    // Sample 4 directions from the padded height cache
                    var hL = heightCache1D[row - stride + cz];
                    var hR = heightCache1D[row + stride + cz];
                    var hB = heightCache1D[row + cz - 1];
                    var hF = heightCache1D[row + cz + 1];

                    // Standard Sobel-filter style normal generation
                    Vector3 normal = new(hL - hR, 2.0f * (hDist / vScale), hB - hF);
                    normals[idx] = normal.normalized;
                }
            }
        }

        private void CalculateNormalsSkirt()
        {
            var centerX = chunkBoundSize * 0.5f;
            for (var n = resolution * resolution; n < vertices.Length; n++)
            {
                var dir = (
                    vertices[n] - new Vector3(centerX, vertices[n].y, centerX)
                ).normalized;

                normals[n] = new Vector3(dir.x, 0, dir.z);
            }
        }

        public void PopulateMesh(Mesh targetMesh)
        {
            targetMesh.Clear();
            targetMesh.SetVertices(vertices);
            targetMesh.SetUVs(0, uvs);
            targetMesh.SetNormals(normals);

            var tris = triangleProvider(resolution);
            if (lastTriangleCount != tris.Length)
            {
                targetMesh.SetTriangles(tris, 0);
                lastTriangleCount = tris.Length;
            }

            targetMesh.UploadMeshData(false);
        }
    }
}
