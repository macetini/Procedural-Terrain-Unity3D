using System.Collections.Generic;
using UnityEngine;

namespace ProceduralTerrain.Processing.Data
{
    public struct NeighborStruct
    {
        private TileSample[,] center,
            west,
            south,
            southWest,
            east,
            north,
            northWest,
            northEast,
            southEast;

        public static NeighborStruct GetNeighborGrids(
            Vector2Int coord,
            Dictionary<Vector2Int, TileSample[,]> tileMap
        )
        {
            NeighborStruct neighbors = new();

            // Cardinal
            tileMap.TryGetValue(coord, out neighbors.center);
            tileMap.TryGetValue(coord + Vector2Int.left, out neighbors.west);
            tileMap.TryGetValue(coord + Vector2Int.down, out neighbors.south);
            tileMap.TryGetValue(coord + Vector2Int.right, out neighbors.east);
            tileMap.TryGetValue(coord + Vector2Int.up, out neighbors.north);

            // Diagonals
            tileMap.TryGetValue(coord + new Vector2Int(-1, -1), out neighbors.southWest);
            tileMap.TryGetValue(coord + new Vector2Int(-1, 1), out neighbors.northWest);
            tileMap.TryGetValue(coord + new Vector2Int(1, 1), out neighbors.northEast);
            tileMap.TryGetValue(coord + new Vector2Int(1, -1), out neighbors.southEast);

            return neighbors;
        }

        public readonly float GetElevation(int x, int z, int chunkSize)
        {
            if (center == null)
            {
                return 0f;
            }

            if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
            {
                return center[x, z].Elevation;
            }

            // Determine Directional Indices (-1, 0, or 1)
            var dirX = GetDirectionIndex(x, chunkSize);
            var dirZ = GetDirectionIndex(z, chunkSize);

            // Map to Local Neighbor Coordinates
            var nx = MapCoordinate(x, chunkSize);
            var nz = MapCoordinate(z, chunkSize);

            // Retrieve Neighbor Grid
            var targetGrid = GetNeighborByDir(dirX, dirZ);

            if (targetGrid != null)
            {
                return targetGrid[nx, nz].Elevation;
            }

            // Fallback: Clamp to Center's nearest edge
            var fx = Mathf.Clamp(x, 0, chunkSize - 1);
            var fz = Mathf.Clamp(z, 0, chunkSize - 1);

            return center[fx, fz].Elevation;
        }

        private static int GetDirectionIndex(int val, int limit)
        {
            if (val < 0)
            {
                return -1;
            }

            return val >= limit ? 1 : 0;
        }

        private static int MapCoordinate(int val, int limit)
        {
            if (val < 0)
            {
                return val + limit;
            }

            if (val >= limit)
            {
                return val - limit;
            }

            return val;
        }

        private readonly TileSample[,] GetNeighborByDir(int dx, int dz)
        {
            // Switch expressions are much cleaner than nested ternaries
            return (dx, dz) switch
            {
                (-1, 0) => west,
                (1, 0) => east,
                (0, -1) => south,
                (0, 1) => north,
                (-1, -1) => southWest,
                (-1, 1) => northWest,
                (1, 1) => northEast,
                (1, -1) => southEast,
                _ => null,
            };
        }
    }
}
