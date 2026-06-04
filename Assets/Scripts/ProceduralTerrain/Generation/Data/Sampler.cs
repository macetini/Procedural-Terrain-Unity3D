using System.Collections.Generic;
using ProceduralTerrain.Processing.Data;
using ProceduralTerrain.Utils;
using UnityEngine;

namespace ProceduralTerrain.Generation.Data
{
    internal class Sampler
    {
        private readonly Dictionary<Vector2Int, TileSample[,]> tileMap;
        private readonly int chunkSize;
        private readonly TerrainNoise noise;
        private readonly Stack<TileSample[,]> arrayPool = new();

        public Sampler(
            Dictionary<Vector2Int, TileSample[,]> tileMap,
            int chunkSize,
            TerrainNoise noise
        ) => (this.tileMap, this.chunkSize, this.noise) = (tileMap, chunkSize, noise);

        public void ClearAll()
        {
            tileMap.Clear();
            arrayPool.Clear();
        }

        public bool HasTile(Vector2Int coord) => tileMap.ContainsKey(coord);

        public void RemoveTile(Vector2Int coord)
        {
            if (tileMap.TryGetValue(coord, out var data))
            {
                arrayPool.Push(data);
                tileMap.Remove(coord);
            }
        }

        public void GetTileKeysNonAlloc(List<Vector2Int> targetList)
        {
            targetList.Clear();
            foreach (var key in tileMap.Keys)
                targetList.Add(key);
        }

        public void GenerateRawData(Vector2Int coord)
        {
            if (tileMap.ContainsKey(coord))
            {
                return;
            }

            TileSample[,] data = RentArray();
            int offsetX = coord.x * chunkSize;
            int offsetZ = coord.y * chunkSize;

            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    int elevation = noise.GetElevation(offsetX + x, offsetZ + z);
                    data[x, z] = new TileSample(x, z, elevation);
                }
            }
            tileMap[coord] = data;
        }

        private TileSample[,] RentArray()
        {
            return arrayPool.Count > 0 ? arrayPool.Pop() : new TileSample[chunkSize, chunkSize];
        }
    }
}
