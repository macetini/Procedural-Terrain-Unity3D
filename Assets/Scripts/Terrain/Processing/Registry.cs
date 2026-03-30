using System.Collections.Generic;
using Assets.Scripts.Terrain.Chunk;
using UnityEngine;

namespace Assets.Scripts.Terrain.Processing
{
    internal class Registry
    {
        private readonly Dictionary<Vector2Int, TerrainChunk> activeChunks = new();

        // Registry Helpers
        public bool HasActiveChunk(Vector2Int coord) => activeChunks.ContainsKey(coord);

        public bool TryGetActiveChunk(Vector2Int coord, out TerrainChunk chunk) =>
            activeChunks.TryGetValue(coord, out chunk);

        public void RegisterChunk(Vector2Int coord, TerrainChunk chunk)
        {
            if (activeChunks.TryGetValue(coord, out TerrainChunk existingChunk))
            {
                if (existingChunk == chunk)
                {
                    return;
                }

                if (existingChunk != null)
                {
                    existingChunk.CallDestroy();
                }
            }

            activeChunks[coord] = chunk;
        }

        public void UnregisterChunk(Vector2Int coord) => activeChunks.Remove(coord);

        // Property to let the Generator see the keys for culling/cleanup
        public Dictionary<Vector2Int, TerrainChunk>.KeyCollection ActiveChunkKeys =>
            activeChunks.Keys;

        public void ClearAll()
        {
            foreach (var chunk in activeChunks.Values)
            {
                if (chunk != null)
                {
                    chunk.CallDestroy();
                }
            }
            activeChunks.Clear();
        }

        public void GetActiveKeysNonAlloc(List<Vector2Int> targetList)
        {
            targetList.Clear();
            foreach (var key in activeChunks.Keys)
            {
                targetList.Add(key);
            }
        }
    }
}
