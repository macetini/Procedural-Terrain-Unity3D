using ProceduralTerrain.Processing.Data;

namespace ProceduralTerrain.Utils
{
    public static class TerrainMath
    {
        // Moves the logic out of the manager to keep it pure
        public static void ClampNeighbor(ref TileSample a, ref TileSample b)
        {
            var diff = a.Elevation - b.Elevation;
            b.Elevation = diff switch
            {
                > 1 => a.Elevation - 1,
                < -1 => a.Elevation + 1,
                _ => b.Elevation
            };
        }

        public static int[] GenerateTriangleIndices(int resolution)
        {
            var gridTris = (resolution - 1) * (resolution - 1) * 6;
            var skirtTris = (resolution - 1) * 4 * 6;
            var tris = new int[gridTris + skirtTris];

            var trisCount = GenerateGridTriangles(resolution, tris);
            GenerateSkirtTriangles(resolution, tris, trisCount);

            return tris;
        }

        private static int GenerateGridTriangles(int resolution, int[] tris)
        {
            var trisCount = 0;

            for (var x = 0; x < resolution - 1; x++)
            {
                for (var z = 0; z < resolution - 1; z++)
                {
                    var bl = x * resolution + z;
                    var tl = bl + 1;
                    var br = (x + 1) * resolution + z;
                    var tr = br + 1;
                    
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

        private static void GenerateSkirtTriangles(int resolution, int[] tris, int trisCount)
        {
            var southTris = GenerateSouthSkirtTriangles(resolution, tris, trisCount);
            var northTris = GenerateNorthSkirtTriangles(resolution, tris, trisCount + southTris);
            var westTris = GenerateWestSkirtTriangles(
                resolution,
                tris,
                trisCount + southTris + northTris
            );
            
            GenerateEastSkirtTriangles(
                resolution,
                tris,
                trisCount + southTris + northTris + westTris
            );
        }

        private static int GenerateSouthSkirtTriangles(int resolution, int[] tris, int trisCount)
        {
            var gridCount = resolution * resolution;
            for (var j = 0; j < resolution - 1; j++)
            {
                var gL = j * resolution;
                var gR = (j + 1) * resolution;
                var sL = gridCount + j;
                var sR = gridCount + j + 1;
                
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
            var gridCount = resolution * resolution;
            var nStart = gridCount + resolution;
            for (var j = 0; j < resolution - 1; j++)
            {
                var ngL = j * resolution + (resolution - 1);
                var ngR = (j + 1) * resolution + (resolution - 1);
                var nsL = nStart + j;
                var nsR = nStart + j + 1;
                
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
            var gridCount = resolution * resolution;
            var wStart = gridCount + resolution * 2;
            for (var j = 0; j < resolution - 1; j++)
            {
                var wgT = j + 1;
                var wsB = wStart + j;
                var wsT = wStart + j + 1;
                
                tris[trisCount++] = j;
                tris[trisCount++] = wsT;
                tris[trisCount++] = wgT;
                tris[trisCount++] = j;
                tris[trisCount++] = wsB;
                tris[trisCount++] = wsT;
            }
            return (resolution - 1) * 6;
        }

        private static void GenerateEastSkirtTriangles(int resolution, int[] tris, int trisCount)
        {
            var gridCount = resolution * resolution;
            var eStart = gridCount + resolution * 3;
            for (var j = 0; j < resolution - 1; j++)
            {
                var egB = (resolution - 1) * resolution + j;
                var egT = (resolution - 1) * resolution + j + 1;
                var esB = eStart + j;
                var esT = eStart + j + 1;
                
                tris[trisCount++] = egB;
                tris[trisCount++] = egT;
                tris[trisCount++] = esT;
                tris[trisCount++] = egB;
                tris[trisCount++] = esT;
                tris[trisCount++] = esB;
            }
        }
    }
}
