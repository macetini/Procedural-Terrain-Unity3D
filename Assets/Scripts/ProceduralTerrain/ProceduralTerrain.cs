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

        private ITerrainOrchestrator orchestrator;

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
            if (orchestrator == null)
            {
                Debug.LogError(
                    "[ProceduralTerrain] Runtime orchestrator is not initialized.",
                    this
                );
                return default;
            }
            return orchestrator.GetNeighborGrids(chunkCoord);
        }

        public int[] GetPrecalculatedTriangles(int resolution)
        {
            if (orchestrator == null)
            {
                Debug.LogError(
                    "[ProceduralTerrain] Runtime orchestrator is not initialized.",
                    this
                );
                return System.Array.Empty<int>();
            }
            return orchestrator.GetPrecalculatedTriangles(resolution);
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
            orchestrator = new TerrainDataGenerator(this);
        }

        void OnDestroy()
        {
            if (orchestrator != null)
            {
                orchestrator.Destroy();
            }
        }

        void Start()
        {
            if (orchestrator == null)
            {
                return;
            }
            orchestrator.BuildTerrain();
        }

        void Update()
        {
            if (orchestrator == null)
            {
                return;
            }

            orchestrator.UpdateCurrentCameraPosition();

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
            orchestrator.ResetGeneratorState();
            orchestrator.BuildTerrain();
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
                    "[ProceduralTerrain] Chunk prefab is not assigned. Assign it under Prefabs > Chunk Prefab in the Inspector.",
                    this
                );
                return false;
            }

            if (cameraConfig == null || cameraConfig.reference == null)
            {
                Debug.LogError(
                    "[ProceduralTerrain] Camera reference is not assigned. Assign it under Camera Settings > Reference in the Inspector.",
                    this
                );
                return false;
            }

            return true;
        }
    }
}
