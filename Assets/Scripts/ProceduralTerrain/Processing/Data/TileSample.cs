namespace ProceduralTerrain.Processing.Data
{
    public struct TileSample
    {
        public int X;
        public int Z;
        public int Elevation;

        public TileSample(int x, int z, int elevation)
        {
            X = x;
            Z = z;

            Elevation = elevation;
        }
    }
}
