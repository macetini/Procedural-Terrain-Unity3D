using System.Collections.Generic;
using Assets.Scripts.Terrain.Generation.Processing.Chunk;
using UnityEngine;

namespace Assets.Scripts.Terrain.Generation
{
    internal class CleanupState
    {
        public readonly List<Vector2Int> VisibilityKeysSnapshot = new();
        public readonly List<TerrainChunk> SceneChunksSnapshot = new();
        public readonly HashSet<Vector2Int> SeenCoords = new();
        public int CleanupPassCounter;

        public void ResetForRebuild()
        {
            CleanupPassCounter = 0;
        }

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
