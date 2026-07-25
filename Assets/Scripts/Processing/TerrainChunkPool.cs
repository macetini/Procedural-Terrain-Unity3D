using System.Collections.Generic;
using ProceduralTerrain.Builder;
using UnityEngine;

namespace ProceduralTerrain.Processing
{
    internal class TerrainChunkPool : IChunkPool
    {
        private readonly Stack<TerrainChunk> pool = new();
        private readonly TerrainChunk prefab;
        private readonly Transform parent;
        private readonly int maxSize;
        private readonly int chunkLayerMask;

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

        public TerrainChunkPool(
            TerrainChunk prefab,
            Transform parent,
            int maxSize,
            int chunkLayerMask = 0
        )
        {
            this.prefab = prefab;
            this.parent = parent;
            this.maxSize = maxSize;
            this.chunkLayerMask = chunkLayerMask;
        }

        public TerrainChunk Get(Vector3 position)
        {
            while (pool.Count > 0)
            {
                var chunk = pool.Pop();
                if (chunk == null)
                {
                    continue; // destroyed externally, skip
                }

                chunk.transform.SetPositionAndRotation(position, Quaternion.identity);
                chunk.gameObject.layer = chunkLayerMask;
                chunk.gameObject.SetActive(true);
                return chunk;
            }

            var newChunk = Object.Instantiate(prefab, position, Quaternion.identity, parent);
            newChunk.gameObject.layer = chunkLayerMask;
            return newChunk;
        }

        public void Return(TerrainChunk chunk)
        {
            if (chunk == null)
            {
                Debug.LogWarning("Attempted to return a null chunk to the pool.", parent);
                return;
            }

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
                {
                    Object.Destroy(chunk.gameObject);
                }
            }
        }
    }
}
