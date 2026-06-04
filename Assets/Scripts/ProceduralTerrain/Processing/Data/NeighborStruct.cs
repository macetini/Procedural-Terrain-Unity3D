using System.Collections.Generic;
using UnityEngine;

namespace ProceduralTerrain.Processing.Data
{
    public struct NeighborStruct
    {
        public TileSample[,] Center,
            West,
            South,
            SouthWest,
            East,
            North,
            NorthWest,
            NorthEast,
            SouthEast;

        public static NeighborStruct GetNeighborGrids(
            Vector2Int coord,
            Dictionary<Vector2Int, TileSample[,]> tileMap
        )
        {
            NeighborStruct neighbors = new();

            // Cardinal
            tileMap.TryGetValue(coord, out neighbors.Center);
            tileMap.TryGetValue(coord + Vector2Int.left, out neighbors.West);
            tileMap.TryGetValue(coord + Vector2Int.down, out neighbors.South);
            tileMap.TryGetValue(coord + Vector2Int.right, out neighbors.East);
            tileMap.TryGetValue(coord + Vector2Int.up, out neighbors.North);

            // Diagonals
            tileMap.TryGetValue(coord + new Vector2Int(-1, -1), out neighbors.SouthWest);
            tileMap.TryGetValue(coord + new Vector2Int(-1, 1), out neighbors.NorthWest);
            tileMap.TryGetValue(coord + new Vector2Int(1, 1), out neighbors.NorthEast);
            tileMap.TryGetValue(coord + new Vector2Int(1, -1), out neighbors.SouthEast);

            return neighbors;
        }

        public readonly float GetElevation(int x, int z, int chunkSize)
        {
            if (Center == null)
            {
                return 0f;
            }

            if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
            {
                return Center[x, z].Elevation;
            }

            // Determine Directional Indices (-1, 0, or 1)
            int dirX = GetDirectionIndex(x, chunkSize);
            int dirZ = GetDirectionIndex(z, chunkSize);

            // Map to Local Neighbor Coordinates
            int nx = MapCoordinate(x, chunkSize);
            int nz = MapCoordinate(z, chunkSize);

            // Retrieve Neighbor Grid
            TileSample[,] targetGrid = GetNeighborByDir(dirX, dirZ);

            if (targetGrid != null)
            {
                return targetGrid[nx, nz].Elevation;
            }

            // Fallback: Clamp to Center's nearest edge
            int fx = Mathf.Clamp(x, 0, chunkSize - 1);
            int fz = Mathf.Clamp(z, 0, chunkSize - 1);

            return Center[fx, fz].Elevation;
        }

        private static int GetDirectionIndex(int val, int limit)
        {
            if (val < 0)
            {
                return -1;
            }

            if (val >= limit)
            {
                return 1;
            }

            return 0;
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
                (-1, 0) => West,
                (1, 0) => East,
                (0, -1) => South,
                (0, 1) => North,
                (-1, -1) => SouthWest,
                (-1, 1) => NorthWest,
                (1, 1) => NorthEast,
                (1, -1) => SouthEast,
                _ => null,
            };
        }
    }
}
