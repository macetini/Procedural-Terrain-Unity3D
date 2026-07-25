using System.Collections;
using System.Collections.Generic;
using ProceduralTerrain.Processing.Data;
using ProceduralTerrain.Settings;
using UnityEngine;

namespace ProceduralTerrain.Builder
{
    public interface ITerrainHost
    {
        // Settings
        TerrainSettings Terrain { get; }
        NoiseSettings Noise { get; }
        CameraSettings CameraConfig { get; }
        LODSettings LOD { get; }
        DebugSettings Debug { get; }
        int ChunkLayer { get; }

        // Refs
        TerrainChunk ChunkPrefab { get; }
        Transform Transform { get; }
        Transform ChunkParent { get; }

        // Unity lifecycle
        bool IsActiveAndEnabled { get; }
        Coroutine StartCoroutine(IEnumerator routine);
        void StopAllCoroutines();

        // Scene query (non-generic wrapper so interface stays non-generic)
        void GetChunkChildren(List<TerrainChunk> results);

        // Data delegation
        NeighborStruct GetNeighborGrids(Vector2Int chunkCoord);
        int[] GetPrecalculatedTriangles(int resolution);
    }
}
