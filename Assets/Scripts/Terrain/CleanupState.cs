using System.Collections.Generic;
using Assets.Scripts.Terrain.Chunk;
using UnityEngine;

namespace Assets.Scripts.Terrain
{
    internal class CleanupState
    {
        public readonly List<Vector2Int> VisibilityKeysSnapshot = new();
        public readonly List<TerrainChunk> SceneChunksSnapshot = new();
        public readonly HashSet<Vector2Int> SeenCoords = new();

        public void ResetForRebuild() { }

        public void BeginCleanupPass()
        {
            VisibilityKeysSnapshot.Clear();
        }

        public void BeginSceneSweep()
        {
            SceneChunksSnapshot.Clear();
            SeenCoords.Clear();
        }
    }
}
