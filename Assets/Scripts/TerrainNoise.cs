using UnityEngine;

public static class TerrainNoise
{
    private static int seed;
    private static float scale;
    private static int octaves;
    private static float persistence;
    private static float lacunarity;
    private static int maxSteps;

    public static void Init(
        int seed,
        float scale,
        int octaves,
        float persistence,
        float lacunarity,
        int maxSteps
    )
    {
        TerrainNoise.seed = seed;
        TerrainNoise.scale = scale;
        TerrainNoise.octaves = octaves;
        TerrainNoise.persistence = persistence;
        TerrainNoise.lacunarity = lacunarity;
        TerrainNoise.maxSteps = maxSteps;
    }

    /// <summary>
    /// The core Mirrored Elevation function
    /// </summary>
    /// <param name="scale">Base frequency (e.g., 0.02f)</param>
    /// <param name="octaves">How many layers of detail (e.g., 4)</param>
    /// <param name="persistence">How much each octave's amplitude diminishes (e.g., 0.5f)</param>
    /// <param name="lacunarity">How much the frequency increases per octave (e.g., 2.0f)</param>
    public static int GetElevation(int x, int z)
    {
        float total = 0;
        float frequency = scale;
        float amplitude = 1f;
        float maxValue = 0; // Used for normalizing

        for (int i = 0; i < octaves; i++)
        {
            total += GetValueNoise(x * frequency, z * frequency) * amplitude;

            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        // Normalize the result to 0..1
        // (total / maxValue) gives us -1..1 range.
        float normalized = ((total / maxValue) + 1f) * 0.5f;

        // Apply a "Power" curve to flatten valleys and sharpen peaks (Mars style)
        // This is optional but very customizable!
        normalized = Mathf.Pow(normalized, 1.2f);

        return Mathf.Clamp(Mathf.FloorToInt(normalized * (maxSteps + 1)), 0, maxSteps);
    }

    private static float GetValueNoise(float x, float z)
    {
        int iX = Mathf.FloorToInt(x);
        int iZ = Mathf.FloorToInt(z);
        float fX = x - iX;
        float fZ = z - iZ;

        float v1 = Hash(iX, iZ);
        float v2 = Hash(iX + 1, iZ);
        float v3 = Hash(iX, iZ + 1);
        float v4 = Hash(iX + 1, iZ + 1);

        float tX = Fade(fX);
        float tZ = Fade(fZ);

        return Lerp(Lerp(v1, v2, tX), Lerp(v3, v4, tX), tZ);
    }

    private static float Hash(int x, int z)
    {
        // Deterministic Hash (Same in Java)
        int n = x + z * 57 + seed * 131;
        n = (n << 13) ^ n;
        return 1.0f - ((n * n * n * 15731 + 789221 + 1376312589) & 0x7fffffff) / 1073741824.0f;
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10); // Perlin's SmootherStep

    private static float Lerp(float a, float b, float t) => a + t * (b - a);
}
