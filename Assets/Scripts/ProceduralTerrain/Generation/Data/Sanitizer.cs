using System.Collections.Generic;
using ProceduralTerrain.Processing.Chunk.Data;
using ProceduralTerrain.Utils;
using UnityEngine;

namespace ProceduralTerrain.Generation.Data
{
    internal class Sanitizer
    {
        // Data
        private readonly Dictionary<Vector2Int, TileMeshStruct[,]> tileMap;
        private readonly int chunkSize;
        private readonly HashSet<Vector2Int> sanitizedSet = new();

        // Methods
        public bool IsSanitized(Vector2Int coord) => sanitizedSet.Contains(coord);

        public void Validate(Vector2Int coord) => sanitizedSet.Add(coord);

        public void Invalidate(Vector2Int coord) => sanitizedSet.Remove(coord);

        public void ClearAll() => sanitizedSet.Clear();

        public Sanitizer(Dictionary<Vector2Int, TileMeshStruct[,]> tileMap, int chunkSize) =>
            (this.tileMap, this.chunkSize) = (tileMap, chunkSize);

        public void SanitizeCurrentTileMeshData(Vector2Int cameraOrigin, int dataRadius)
        {
            for (int x = -dataRadius; x <= dataRadius; x++)
            {
                for (int z = -dataRadius; z <= dataRadius; z++)
                {
                    Vector2Int coord = new(cameraOrigin.x + x, cameraOrigin.y + z);
                    if (!sanitizedSet.Contains(coord)) // We only need to sanitize if the mesh hasn't been built yet
                    {
                        SanitizeGlobalChunk(coord);
                        sanitizedSet.Add(coord);
                    }
                }
            }
        }

        public void SanitizeGlobalChunk(Vector2Int coord)
        {
            if (!tileMap.TryGetValue(coord, out TileMeshStruct[,] currentData))
                return;

            tileMap.TryGetValue(coord + Vector2Int.right, out TileMeshStruct[,] eastData);
            tileMap.TryGetValue(coord + Vector2Int.up, out TileMeshStruct[,] northData);

            int edge = chunkSize - 1;

            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    ref TileMeshStruct current = ref currentData[x, z];

                    // East neighbor clamping
                    if (x < edge)
                    {
                        TerrainMath.ClampNeighbor(ref current, ref currentData[x + 1, z]);
                    }
                    else if (eastData != null)
                    {
                        TerrainMath.ClampNeighbor(ref current, ref eastData[0, z]);
                    }

                    // North neighbor clamping
                    if (z < edge)
                    {
                        TerrainMath.ClampNeighbor(ref current, ref currentData[x, z + 1]);
                    }
                    else if (northData != null)
                    {
                        TerrainMath.ClampNeighbor(ref current, ref northData[x, 0]);
                    }
                }
            }
        }
    }
}
