using UnityEngine;

namespace ProceduralTerrain.Utils
{
    public sealed class TerrainNoise
    {
        private readonly int seed;
        private readonly float scale;
        private readonly int octaves;
        private readonly float persistence;
        private readonly float lacunarity;
        private readonly int maxSteps;

        public TerrainNoise(
            int seed,
            float scale,
            int octaves,
            float persistence,
            float lacunarity,
            int maxSteps
        )
        {
            this.seed = seed;
            this.scale = scale;
            this.octaves = octaves;
            this.persistence = persistence;
            this.lacunarity = lacunarity;
            this.maxSteps = maxSteps;
        }
        
        public int GetElevation(int x, int z)
        {
            float total = 0;
            var frequency = scale;
            var amplitude = 1f;
            float maxValue = 0; // Used for normalizing

            for (var i = 0; i < octaves; i++)
            {
                total += GetValueNoise(x * frequency, z * frequency) * amplitude;

                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            // Normalize the result to 0..1
            // (total / maxValue) gives us -1..1 range.
            var normalized = ((total / maxValue) + 1f) * 0.5f;

            // Apply a "Power" curve to flatten valleys and sharpen peaks (Mars style)
            // This is optional but very customizable!
            normalized = Mathf.Pow(normalized, 1.2f);

            return Mathf.Clamp(Mathf.FloorToInt(normalized * (maxSteps + 1)), 0, maxSteps);
        }

        private float GetValueNoise(float x, float z)
        {
            var iX = Mathf.FloorToInt(x);
            var iZ = Mathf.FloorToInt(z);
            
            var fX = x - iX;
            var fZ = z - iZ;

            var v1 = Hash(iX, iZ);
            var v2 = Hash(iX + 1, iZ);
            var v3 = Hash(iX, iZ + 1);
            var v4 = Hash(iX + 1, iZ + 1);

            var tX = Fade(fX);
            var tZ = Fade(fZ);

            return Lerp(Lerp(v1, v2, tX), Lerp(v3, v4, tX), tZ);
        }

        private float Hash(int x, int z)
        {
            // Deterministic Hash (Same in Java)
            var n = x + z * 57 + seed * 131;
            n = (n << 13) ^ n;
            return 1.0f - ((n * n * n * 15731 + 789221 + 1376312589) & 0x7fffffff) / 1073741824.0f;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10); // Perlin's SmootherStep

        private static float Lerp(float a, float b, float t) => a + t * (b - a);
    }
}