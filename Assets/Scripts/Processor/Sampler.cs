using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Processor
{
    internal class Sampler
    {
        private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap;
        private readonly int chunkSize;

        public Sampler(Dictionary<Vector2Int, TileMeshStruct[,]> tileMap, int chunkSize) =>
            (this.tileMap, this.chunkSize) = (tileMap, chunkSize);

        public void ClearAll() => tileMap.Clear();

        public bool HasTile(Vector2Int coord) => tileMap.ContainsKey(coord);

        public void RemoveTile(Vector2Int coord) => tileMap.Remove(coord);

        public void GenerateRawData(Vector2Int coord)
        {
            if (tileMap.ContainsKey(coord))
            {
                return;
            }

            TileMeshStruct[,] data = new TileMeshStruct[chunkSize, chunkSize];
            int offsetX = coord.x * chunkSize;
            int offsetZ = coord.y * chunkSize;

            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    int elevation = TerrainNoise.GetElevation(offsetX + x, offsetZ + z);
                    data[x, z] = new TileMeshStruct(x, z, elevation);
                }
            }
            tileMap.Add(coord, data);
        }
    }
}
