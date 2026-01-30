using UnityEngine;

public class TileMeshData
{
    public enum EdgeDirection
    {
        None,
        EAST,
        WEST,
        NORTH,
        SOUTH,
    }

    [Header("Edge Direction")]
    public bool isEdge;
    public EdgeDirection edgeDir = EdgeDirection.None;

    public int X { get; private set; }
    public int Y { get; private set; }
    public int Elevation { get; set; }

    public Vector2Int LayerPosition => new(X, Y);
    public Vector3 WorldPosition => new(X, Elevation, Y);

    public TileMeshData(int x, int y, int elevation)
    {
        X = x;
        Y = y;
        Elevation = elevation;
    }
}
