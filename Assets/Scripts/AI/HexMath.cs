using UnityEngine;

public static class HexMath
{
    // Standard size (distance from center to a corner)
    public const float SIZE = 1.0f;
    public static readonly float WIDTH = Mathf.Sqrt(3) * SIZE;
    public static readonly float HEIGHT = 2f * SIZE;

    // Convert Hex (q, r) and Elevation (h) to Unity World Space
    public static Vector3 HexToWorld(int q, int r, int h, float stepHeight)
    {
        float x = SIZE * (Mathf.Sqrt(3) * q + Mathf.Sqrt(3) / 2f * r);
        float z = SIZE * (3.0f / 2.0f * r);
        float y = h * stepHeight; // This is your 'Terrace' level

        return new Vector3(x, y, z);
    }

    // Convert World Space back to Hex (Crucial for Mouse Clicks & Server Validation)
    public static Vector2Int WorldToHex(Vector3 worldPos)
    {
        float q = (Mathf.Sqrt(3) / 3f * worldPos.x - 1f / 3f * worldPos.z) / SIZE;
        float r = 2f / 3f * worldPos.z / SIZE;
        return RoundToHex(q, r);
    }

    private static Vector2Int RoundToHex(float q, float r)
    {
        float s = -q - r;
        int iQ = Mathf.RoundToInt(q);
        int iR = Mathf.RoundToInt(r);
        int iS = Mathf.RoundToInt(s);

        float qDiff = Mathf.Abs(iQ - q);
        float rDiff = Mathf.Abs(iR - r);
        float sDiff = Mathf.Abs(iS - s);

        if (qDiff > rDiff && qDiff > sDiff)
            iQ = -iR - iS;
        else if (rDiff > sDiff)
            iR = -iQ - iS;

        return new Vector2Int(iQ, iR);
    }

    /*
    public static float GetHeightAt(Vector3 pos, int elevation, HexType type, int rampDir)
    {
        if (type == HexType.Flat)
            return elevation;

        // If it's a ramp, we calculate progress from one side to the other
        // progress = 0.0 (bottom) to 1.0 (top)
        float progress = CalculateRampProgress(pos, rampDir);
        return elevation + progress;
    }
    */
}
