using System.Collections.Generic;
using UnityEngine;

namespace ProceduralTerrain.Runtime
{
    public class ChunksContainer : MonoBehaviour
    {
        public const string GameObjectName = "ChunksContainer";

        public void GetChunks(List<TerrainChunk> results) => GetComponentsInChildren(true, results);
    }
}
