using Assets.Scripts.Terrain.Data;

namespace Assets.Scripts.Terrain.Utils
{
    public static class TerrainMath
    {
        // Moves the logic out of the manager to keep it pure
        public static void ClampNeighbor(ref TileMeshStruct a, ref TileMeshStruct b)
        {
            int diff = a.Elevation - b.Elevation;
            if (diff > 1)
                b.Elevation = a.Elevation - 1;
            else if (diff < -1)
                b.Elevation = a.Elevation + 1;
        }

        public static int[] GenerateTriangleIndices(int resolution)
        {
            int gridTris = (resolution - 1) * (resolution - 1) * 6;
            int skirtTris = (resolution - 1) * 4 * 6;
            int[] tris = new int[gridTris + skirtTris];
            int t = 0;

            t += GenerateGridTriangles(resolution, tris, t);
            t += GenerateSkirtTriangles(resolution, tris, t);

            return tris;
        }

        private static int GenerateGridTriangles(int resolution, int[] tris, int t)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                for (int z = 0; z < resolution - 1; z++)
                {
                    int bl = x * resolution + z;
                    int tl = bl + 1;
                    int br = (x + 1) * resolution + z;
                    int tr = br + 1;
                    tris[t++] = bl;
                    tris[t++] = tl;
                    tris[t++] = br;
                    tris[t++] = tl;
                    tris[t++] = tr;
                    tris[t++] = br;
                }
            }
            return (resolution - 1) * (resolution - 1) * 6;
        }

        private static int GenerateSkirtTriangles(int resolution, int[] tris, int t)
        {
            int southTris = GenerateSouthSkirtTriangles(resolution, tris, t);
            int northTris = GenerateNorthSkirtTriangles(resolution, tris, t + southTris);
            int westTris = GenerateWestSkirtTriangles(resolution, tris, t + southTris + northTris);
            int eastTris = GenerateEastSkirtTriangles(
                resolution,
                tris,
                t + southTris + northTris + westTris
            );

            return southTris + northTris + westTris + eastTris;
        }

        private static int GenerateSouthSkirtTriangles(int resolution, int[] tris, int t)
        {
            int gridCount = resolution * resolution;
            int sStart = gridCount;
            for (int j = 0; j < resolution - 1; j++)
            {
                int gL = j * resolution;
                int gR = (j + 1) * resolution;
                int sL = sStart + j;
                int sR = sStart + j + 1;
                tris[t++] = gL;
                tris[t++] = sR;
                tris[t++] = gR;
                tris[t++] = gL;
                tris[t++] = sL;
                tris[t++] = sR;
            }
            return (resolution - 1) * 6;
        }

        private static int GenerateNorthSkirtTriangles(int resolution, int[] tris, int t)
        {
            int gridCount = resolution * resolution;
            int nStart = gridCount + resolution;
            for (int j = 0; j < resolution - 1; j++)
            {
                int ngL = j * resolution + (resolution - 1);
                int ngR = (j + 1) * resolution + (resolution - 1);
                int nsL = nStart + j;
                int nsR = nStart + j + 1;
                tris[t++] = ngL;
                tris[t++] = ngR;
                tris[t++] = nsR;
                tris[t++] = ngL;
                tris[t++] = nsR;
                tris[t++] = nsL;
            }
            return (resolution - 1) * 6;
        }

        private static int GenerateWestSkirtTriangles(int resolution, int[] tris, int t)
        {
            int gridCount = resolution * resolution;
            int wStart = gridCount + resolution * 2;
            for (int j = 0; j < resolution - 1; j++)
            {
                int wgB = j;
                int wgT = j + 1;
                int wsB = wStart + j;
                int wsT = wStart + j + 1;
                tris[t++] = wgB;
                tris[t++] = wsT;
                tris[t++] = wgT;
                tris[t++] = wgB;
                tris[t++] = wsB;
                tris[t++] = wsT;
            }
            return (resolution - 1) * 6;
        }

        private static int GenerateEastSkirtTriangles(int resolution, int[] tris, int t)
        {
            int gridCount = resolution * resolution;
            int eStart = gridCount + resolution * 3;
            for (int j = 0; j < resolution - 1; j++)
            {
                int egB = (resolution - 1) * resolution + j;
                int egT = (resolution - 1) * resolution + j + 1;
                int esB = eStart + j;
                int esT = eStart + j + 1;
                tris[t++] = egB;
                tris[t++] = egT;
                tris[t++] = esT;
                tris[t++] = egB;
                tris[t++] = esT;
                tris[t++] = esB;
            }
            return (resolution - 1) * 6;
        }
    }
}
