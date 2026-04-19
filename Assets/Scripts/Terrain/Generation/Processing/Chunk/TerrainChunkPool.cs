using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Terrain.Generation.Processing.Chunk
{
    internal class TerrainChunkPool
    {
        private readonly Stack<TerrainChunk> pool = new();
        private readonly TerrainChunk prefab;
        private readonly Transform parent;
        private readonly int maxSize;

        public int Count => pool.Count;

        /// <summary>
        /// Calculates the maximum number of chunks visible in a square grid
        /// around the camera: (viewDistance * 2 + 1)².
        /// </summary>
        public static int CalculateMaxSize(int viewDistanceChunks)
        {
            int side = viewDistanceChunks * 2 + 1;
            return side * side;
        }

        public TerrainChunkPool(TerrainChunk prefab, Transform parent, int maxSize)
        {
            this.prefab = prefab;
            this.parent = parent;
            this.maxSize = maxSize;
        }

        public TerrainChunk Get(Vector3 position)
        {
            while (pool.Count > 0)
            {
                var chunk = pool.Pop();
                if (chunk == null)
                    continue; // destroyed externally, skip

                chunk.transform.SetPositionAndRotation(position, Quaternion.identity);
                chunk.gameObject.SetActive(true);
                return chunk;
            }

            return Object.Instantiate(prefab, position, Quaternion.identity, parent);
        }

        public void Return(TerrainChunk chunk)
        {
            if (chunk == null)
                return;
            chunk.PrepareForPool();

            if (pool.Count >= maxSize)
            {
                Object.Destroy(chunk.gameObject);
                return;
            }

            chunk.gameObject.SetActive(false);
            pool.Push(chunk);
        }

        public void Clear()
        {
            while (pool.Count > 0)
            {
                var chunk = pool.Pop();
                if (chunk != null)
                    Object.Destroy(chunk.gameObject);
            }
        }
    }
}
