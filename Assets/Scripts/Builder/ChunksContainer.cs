using System.Collections.Generic;
using UnityEngine;

namespace ProceduralTerrain.Builder
{
    public class ChunksContainer : MonoBehaviour
    {
        public const string GameObjectName = "ChunksContainer";

        public void GetChunks(List<TerrainChunk> results) => GetComponentsInChildren(true, results);
    }
}
