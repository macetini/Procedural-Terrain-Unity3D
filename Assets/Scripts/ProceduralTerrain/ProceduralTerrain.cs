using System.Collections.Generic;
using ProceduralTerrain.Generation;
using ProceduralTerrain.Processing.Chunk.Data;
using ProceduralTerrain.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProceduralTerrain
{
    public class ProceduralTerrain : MonoBehaviour, ITerrainHost
    {
        [Header("Terrain Settings")]
        public TerrainSettings terrain = new();

        [Header("Noise Settings")]
        public NoiseSettings noise = new();

        [Header("Camera Settings")]
        public CameraSettings cameraConfig = new();

        [Header("LOD Settings")]
        public LODSettings lod = new();

        [Header("Debug")]
        public DebugSettings debug = new();

        [Header("Prefabs")]
        public TerrainChunk chunkPrefab;

        private ITerrainOrchestrator dataGenerator;

        // ITerrainHost explicit implementations (Unity serializes fields, not auto-properties)
        TerrainSettings ITerrainHost.terrain => terrain;
        NoiseSettings ITerrainHost.noise => noise;
        CameraSettings ITerrainHost.cameraConfig => cameraConfig;
        LODSettings ITerrainHost.lod => lod;
        DebugSettings ITerrainHost.debug => debug;
        TerrainChunk ITerrainHost.chunkPrefab => chunkPrefab;

        public void GetChunkChildren(List<TerrainChunk> results) =>
            GetComponentsInChildren(true, results);

        public ChunkNeighborStruct GetNeighborGrids(Vector2Int chunkCoord)
        {
            if (dataGenerator == null)
            {
                Debug.LogError("[TerrainGenerator] Data generator is not initialized.", this);
                return default;
            }
            return dataGenerator.GetNeighborGrids(chunkCoord);
        }

        public int[] GetPrecalculatedTriangles(int resolution)
        {
            if (dataGenerator == null)
            {
                Debug.LogError("[TerrainGenerator] Data generator is not initialized.", this);
                return System.Array.Empty<int>();
            }
            return dataGenerator.GetPrecalculatedTriangles(resolution);
        }

        void OnValidate()
        {
            NormalizeSettings();
        }

        void Awake()
        {
            if (!ValidateAndNormalizeSettings())
            {
                enabled = false;
                return;
            }
            dataGenerator = new TerrainDataGenerator(this);
        }

        void OnDestroy()
        {
            if (dataGenerator != null)
            {
                dataGenerator.Destroy();
            }
        }

        void Start()
        {
            if (dataGenerator == null)
            {
                return;
            }
            dataGenerator.BuildTerrain();
        }

        void Update()
        {
            if (dataGenerator == null)
            {
                return;
            }

            dataGenerator.UpdateCurrentCameraPosition();

#if UNITY_EDITOR
            // Dev-only hotkey: rebuilds the full terrain.
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            {
                HandleDebugRebuild();
            }
#endif
        }

        private void HandleDebugRebuild()
        {
            Debug.Log("Rebuilding terrain.", this);
            dataGenerator.ResetGeneratorState();
            dataGenerator.BuildTerrain();
        }

        private void NormalizeSettings()
        {
            terrain?.ClampValues();
            noise?.ClampValues();
            cameraConfig?.ClampValues();
            lod?.ClampValues(terrain != null ? terrain.chunkSize : 1);
            debug?.ClampValues();
        }

        private bool ValidateAndNormalizeSettings()
        {
            NormalizeSettings();

            if (chunkPrefab == null)
            {
                Debug.LogError(
                    "[TerrainGenerator] Chunk prefab is not assigned. Assign it under Prefabs > Chunk Prefab in the Inspector.",
                    this
                );
                return false;
            }

            if (cameraConfig == null || cameraConfig.reference == null)
            {
                Debug.LogError(
                    "[TerrainGenerator] Camera reference is not assigned. Assign it under Camera Settings > Reference in the Inspector.",
                    this
                );
                return false;
            }

            return true;
        }
    }
}
