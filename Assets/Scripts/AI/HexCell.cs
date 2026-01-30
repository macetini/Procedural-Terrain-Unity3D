using UnityEngine;

public enum HexType
{
    Flat,
    Ramp,
}

public class HexCell
{
    public int Q;
    public int R;
    public int S => -Q - R;

    public int Elevation;
    public HexType Type = HexType.Flat;
    public int RampDirection;

    public HexCell(int Q, int R, int elevation)
    {
        this.Q = Q;
        this.R = R;

        Elevation = elevation;
    }

    public bool IsWalkableFrom(HexCell other, int maxClimb)
    {
        if (Type == HexType.Ramp || other.Type == HexType.Ramp)
        {
            return true;
        }

        return Mathf.Abs(Elevation - other.Elevation) <= maxClimb;
    }
}
