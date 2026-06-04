using ProceduralTerrain.Processing.Data;

namespace ProceduralTerrain.Utils
{
    public static class TerrainMath
    {
        // Moves the logic out of the manager to keep it pure
        public static void ClampNeighbor(ref TileSample a, ref TileSample b)
        {
            int diff = a.Elevation - b.Elevation;
            if (diff > 1)
            {
                b.Elevation = a.Elevation - 1;
            }
            else if (diff < -1)
            {
                b.Elevation = a.Elevation + 1;
            }
        }

        public static int[] GenerateTriangleIndices(int resolution)
        {
            int gridTris = (resolution - 1) * (resolution - 1) * 6;
            int skirtTris = (resolution - 1) * 4 * 6;
            int[] tris = new int[gridTris + skirtTris];

            int trisCount = GenerateGridTriangles(resolution, tris);
            GenerateSkirtTriangles(resolution, tris, trisCount);

            return tris;
        }

        private static int GenerateGridTriangles(int resolution, int[] tris)
        {
            int trisCount = 0;

            for (int x = 0; x < resolution - 1; x++)
            {
                for (int z = 0; z < resolution - 1; z++)
                {
                    int bl = x * resolution + z;
                    int tl = bl + 1;
                    int br = (x + 1) * resolution + z;
                    int tr = br + 1;
                    tris[trisCount++] = bl;
                    tris[trisCount++] = tl;
                    tris[trisCount++] = br;
                    tris[trisCount++] = tl;
                    tris[trisCount++] = tr;
                    tris[trisCount++] = br;
                }
            }
            return (resolution - 1) * (resolution - 1) * 6;
        }

        private static int GenerateSkirtTriangles(int resolution, int[] tris, int trisCount)
        {
            int southTris = GenerateSouthSkirtTriangles(resolution, tris, trisCount);
            int northTris = GenerateNorthSkirtTriangles(resolution, tris, trisCount + southTris);
            int westTris = GenerateWestSkirtTriangles(
                resolution,
                tris,
                trisCount + southTris + northTris
            );
            int eastTris = GenerateEastSkirtTriangles(
                resolution,
                tris,
                trisCount + southTris + northTris + westTris
            );

            return southTris + northTris + westTris + eastTris;
        }

        private static int GenerateSouthSkirtTriangles(int resolution, int[] tris, int trisCount)
        {
            int gridCount = resolution * resolution;
            int sStart = gridCount;
            for (int j = 0; j < resolution - 1; j++)
            {
                int gL = j * resolution;
                int gR = (j + 1) * resolution;
                int sL = sStart + j;
                int sR = sStart + j + 1;
                tris[trisCount++] = gL;
                tris[trisCount++] = sR;
                tris[trisCount++] = gR;
                tris[trisCount++] = gL;
                tris[trisCount++] = sL;
                tris[trisCount++] = sR;
            }
            return (resolution - 1) * 6;
        }

        private static int GenerateNorthSkirtTriangles(int resolution, int[] tris, int trisCount)
        {
            int gridCount = resolution * resolution;
            int nStart = gridCount + resolution;
            for (int j = 0; j < resolution - 1; j++)
            {
                int ngL = j * resolution + (resolution - 1);
                int ngR = (j + 1) * resolution + (resolution - 1);
                int nsL = nStart + j;
                int nsR = nStart + j + 1;
                tris[trisCount++] = ngL;
                tris[trisCount++] = ngR;
                tris[trisCount++] = nsR;
                tris[trisCount++] = ngL;
                tris[trisCount++] = nsR;
                tris[trisCount++] = nsL;
            }
            return (resolution - 1) * 6;
        }

        private static int GenerateWestSkirtTriangles(int resolution, int[] tris, int trisCount)
        {
            int gridCount = resolution * resolution;
            int wStart = gridCount + resolution * 2;
            for (int j = 0; j < resolution - 1; j++)
            {
                int wgB = j;
                int wgT = j + 1;
                int wsB = wStart + j;
                int wsT = wStart + j + 1;
                tris[trisCount++] = wgB;
                tris[trisCount++] = wsT;
                tris[trisCount++] = wgT;
                tris[trisCount++] = wgB;
                tris[trisCount++] = wsB;
                tris[trisCount++] = wsT;
            }
            return (resolution - 1) * 6;
        }

        private static int GenerateEastSkirtTriangles(int resolution, int[] tris, int trisCount)
        {
            int gridCount = resolution * resolution;
            int eStart = gridCount + resolution * 3;
            for (int j = 0; j < resolution - 1; j++)
            {
                int egB = (resolution - 1) * resolution + j;
                int egT = (resolution - 1) * resolution + j + 1;
                int esB = eStart + j;
                int esT = eStart + j + 1;
                tris[trisCount++] = egB;
                tris[trisCount++] = egT;
                tris[trisCount++] = esT;
                tris[trisCount++] = egB;
                tris[trisCount++] = esT;
                tris[trisCount++] = esB;
            }
            return (resolution - 1) * 6;
        }
    }
}
