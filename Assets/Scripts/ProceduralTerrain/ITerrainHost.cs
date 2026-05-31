using System.Collections;
using System.Collections.Generic;
using ProceduralTerrain.Processing.Chunk.Data;
using ProceduralTerrain.Settings;
using UnityEngine;

namespace ProceduralTerrain
{
    public interface ITerrainHost
    {
        // Settings
        TerrainSettings terrain { get; }
        NoiseSettings noise { get; }
        CameraSettings cameraConfig { get; }
        LODSettings lod { get; }
        DebugSettings debug { get; }

        // Refs
        TerrainChunk chunkPrefab { get; }
        Transform transform { get; }

        // Unity lifecycle
        bool isActiveAndEnabled { get; }
        Coroutine StartCoroutine(IEnumerator routine);
        void StopAllCoroutines();

        // Scene query (non-generic wrapper so interface stays non-generic)
        void GetChunkChildren(List<TerrainChunk> results);

        // Data delegation
        ChunkNeighborStruct GetNeighborGrids(Vector2Int chunkCoord);
        int[] GetPrecalculatedTriangles(int resolution);
    }
}
