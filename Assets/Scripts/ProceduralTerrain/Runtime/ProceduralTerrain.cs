using System.Collections.Generic;
using ProceduralTerrain.Generation;
using ProceduralTerrain.Processing.Data;
using ProceduralTerrain.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProceduralTerrain.Runtime
{
    public class ProceduralTerrain : MonoBehaviour, ITerrainHost
    {
        [Header("References")]
        public TerrainChunk chunkPrefab;
        public ChunksContainer chunksContainer;

        [Header("Terrain Settings")]
        public TerrainSettings terrain = new();

        [Header("Noise Settings")]
        public NoiseSettings noise = new();

        [Header("Camera Settings")]
        public CameraSettings cameraConfig = new();

        [Header("LOD Settings")]
        public LODSettings lod = new();

        [Header("Debug Settings")]
        public DebugSettings debug = new();

        private ITerrainOrchestrator orchestrator;

        // ITerrainHost explicit implementations (Unity serializes fields, not auto-properties)
        TerrainSettings ITerrainHost.Terrain => terrain;
        NoiseSettings ITerrainHost.Noise => noise;
        CameraSettings ITerrainHost.CameraConfig => cameraConfig;
        LODSettings ITerrainHost.LOD => lod;
        DebugSettings ITerrainHost.Debug => debug;
        TerrainChunk ITerrainHost.ChunkPrefab => chunkPrefab;
        Transform ITerrainHost.Transform => transform;
        Transform ITerrainHost.ChunkParent => chunksContainer.transform;

        bool ITerrainHost.IsActiveAndEnabled => isActiveAndEnabled;

        public void GetChunkChildren(List<TerrainChunk> results) =>
            chunksContainer.GetChunks(results);

        public NeighborStruct GetNeighborGrids(Vector2Int chunkCoord)
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

            EnsureChunksContainer();
            orchestrator = new TerrainDataGenerator(this);
        }

        void OnDestroy()
        {
            orchestrator?.Destroy();
            orchestrator = null;
        }

        void OnDrawGizmosSelected()
        {
            if (debug == null || !debug.showLodRanges)
            {
                return;
            }

            if (cameraConfig == null || cameraConfig.reference == null || terrain == null)
            {
                return;
            }

            DrawLodRangeGizmo(lod.lod1Distance, new Color(1f, 0.84f, 0.2f, 1f));
            DrawLodRangeGizmo(lod.lod2Distance, new Color(1f, 0.45f, 0.1f, 1f));
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

#if UNITY_EDITOR
        private void HandleDebugRebuild()
        {
            Debug.Log("Rebuilding terrain.", this);
            orchestrator.ResetGeneratorState();
            orchestrator.BuildTerrain();
        }
#endif

        private void DrawLodRangeGizmo(float chunkDistance, Color color)
        {
            float chunkWorldSize = terrain.chunkSize * terrain.tileSize;
            float worldDistance = chunkDistance * chunkWorldSize;
            float maxHeight = terrain.maxElevationStepsCount * terrain.elevationStepHeight;

            Vector3 center = cameraConfig.reference.transform.position;
            center.y += maxHeight * 0.5f;

            Vector3 size = new(worldDistance * 2f, maxHeight, worldDistance * 2f);

            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
        }

        private void EnsureChunksContainer()
        {
            if (chunksContainer != null)
            {
                return;
            }

            chunksContainer = GetComponentInChildren<ChunksContainer>(true);
            if (chunksContainer != null)
            {
                return;
            }

            var containerObject = new GameObject(
                ChunksContainer.GameObjectName,
                typeof(ChunksContainer)
            );
            containerObject.transform.SetParent(transform, false);
            chunksContainer = containerObject.GetComponent<ChunksContainer>();
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
