using UnityEngine;

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

        // 1. Grid
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

        // 2. Skirts
        int gridCount = resolution * resolution;
        int sStart = gridCount;
        int nStart = gridCount + resolution;
        int wStart = gridCount + resolution * 2;
        int eStart = gridCount + resolution * 3;

        for (int j = 0; j < resolution - 1; j++)
        {
            // South
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

            // North
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

            // West
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

            // East
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
        return tris;
    }
}
