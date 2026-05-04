using System.Collections.Generic;
using SSHexMap.Terrain.Data;
using SSHexMap.Terrain.Utils;
using UnityEngine;

namespace SSHexMap.Terrain.Processing
{
    internal class Sampler
    {
        private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap;
        private readonly int chunkSize;
        private readonly TerrainNoise noise;
        private readonly Stack<TileMeshStruct[,]> arrayPool = new();

        public Sampler(
            Dictionary<Vector2Int, TileMeshStruct[,]> tileMap,
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

            TileMeshStruct[,] data = RentArray();
            int offsetX = coord.x * chunkSize;
            int offsetZ = coord.y * chunkSize;

            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    int elevation = noise.GetElevation(offsetX + x, offsetZ + z);
                    data[x, z] = new TileMeshStruct(x, z, elevation);
                }
            }
            tileMap[coord] = data;
        }

        private TileMeshStruct[,] RentArray()
        {
            return arrayPool.Count > 0 ? arrayPool.Pop() : new TileMeshStruct[chunkSize, chunkSize];
        }
    }
}

