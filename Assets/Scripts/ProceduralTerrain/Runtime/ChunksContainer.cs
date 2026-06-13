using System.Collections.Generic;
using UnityEngine;

namespace ProceduralTerrain.Runtime
{
    public class ChunksContainer : MonoBehaviour
    {
        public const string GameObjectName = "ChunksContainer";

        private readonly List<TerrainChunk> chunkBuffer = new();
        private bool lastDebugNormals;

        public void GetChunks(List<TerrainChunk> results) => GetComponentsInChildren(true, results);

        public void SyncDebugNormals(bool debugNormals)
        {
            if (debugNormals == lastDebugNormals)
            {
                return;
            }

            lastDebugNormals = debugNormals;
            GetChunks(chunkBuffer);

            for (int i = 0; i < chunkBuffer.Count; i++)
            {
                if (chunkBuffer[i] != null)
                {
                    chunkBuffer[i].DebugNormals = debugNormals;
                }
            }

            chunkBuffer.Clear();
        }
    }
}
