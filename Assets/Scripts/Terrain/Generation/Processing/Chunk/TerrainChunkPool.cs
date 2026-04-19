using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Terrain.Generation.Processing.Chunk
{
    internal class TerrainChunkPool
    {
        private readonly Stack<TerrainChunk> pool = new();
        private readonly TerrainChunk prefab;
        private readonly Transform parent;

        public int Count => pool.Count;

        public TerrainChunkPool(TerrainChunk prefab, Transform parent)
        {
            this.prefab = prefab;
            this.parent = parent;
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
