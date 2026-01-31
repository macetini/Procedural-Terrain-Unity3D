using UnityEngine;

public struct TileMeshStruct
{
    public int X;
    public int Z;
    public int Elevation;

    public TileMeshStruct(int x, int z, int elevation)
    {
        X = x;
        Z = z;
        Elevation = elevation;
    }
}
