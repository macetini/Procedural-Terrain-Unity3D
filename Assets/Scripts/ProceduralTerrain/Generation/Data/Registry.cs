using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.ProceduralTerrain.Generation.Data
{
    internal class Registry
    {
        private readonly Dictionary<Vector2Int, TerrainChunk> activeChunks = new();
        private System.Action<TerrainChunk> onChunkDispose;

        public void SetChunkDisposeAction(System.Action<TerrainChunk> action) =>
            onChunkDispose = action;

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
                    DisposeChunk(existingChunk);
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
                    DisposeChunk(chunk);
                }
            }
            activeChunks.Clear();
        }

        private void DisposeChunk(TerrainChunk chunk)
        {
            if (onChunkDispose != null)
                onChunkDispose(chunk);
            else
                chunk.CallDestroy();
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
