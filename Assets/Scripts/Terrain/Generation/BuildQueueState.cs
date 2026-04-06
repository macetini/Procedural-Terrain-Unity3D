using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Terrain.Generation
{
    internal class BuildQueueState
    {
        public readonly Queue<Vector2Int> Queue = new();
        public readonly HashSet<Vector2Int> QueueHash = new();
        public bool IsProcessing;

        public void Clear()
        {
            Queue.Clear();
            QueueHash.Clear();
            IsProcessing = false;
        }
    }
}
