public struct ChunkNeighborStruct
{
    public TileMeshStruct[,] Center,
        W,
        S,
        SW,
        E,
        N,
        NW,
        NE,
        SE;

    // A helper to make sure we have the critical center data
    public readonly bool IsValid => Center != null;
}
