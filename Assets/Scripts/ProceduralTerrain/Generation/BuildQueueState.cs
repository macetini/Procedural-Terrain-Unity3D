using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralTerrain.Generation
{
    internal class BuildQueueState
    {
        public readonly Queue<Vector2Int> Queue = new();
        public readonly HashSet<Vector2Int> QueueHash = new();
        public readonly List<Vector2Int> SortBuffer = new();
        public bool IsProcessing;
        public Vector2Int SortOrigin;
        public readonly Comparison<Vector2Int> DistanceComparison;

        public BuildQueueState()
        {
            DistanceComparison = (a, b) =>
            {
                int distA = Mathf.Abs(a.x - SortOrigin.x) + Mathf.Abs(a.y - SortOrigin.y);
                int distB = Mathf.Abs(b.x - SortOrigin.x) + Mathf.Abs(b.y - SortOrigin.y);

                return distA.CompareTo(distB);
            };
        }

        public void Clear()
        {
            Queue.Clear();
            QueueHash.Clear();
            SortBuffer.Clear();
            IsProcessing = false;
        }
    }
}
