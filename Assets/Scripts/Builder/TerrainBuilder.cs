using System;
using System.Collections.Generic;
using ProceduralTerrain.Generation;
using ProceduralTerrain.Processing.Data;
using ProceduralTerrain.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProceduralTerrain.Builder
{
    public class TerrainBuilder : MonoBehaviour, ITerrainHost
    {
        [Header("References")] 
        public TerrainChunk chunkPrefab;
        public ChunksContainer chunksContainer;

#if UNITY_EDITOR
        [Header("Runtime")] 
        public bool buildOnStart ;
#endif

        [Header("Terrain")] 
        public TerrainSettings terrain = new();
        public NoiseSettings noise = new();

        [Header("Camera")] 
        public CameraSettings cameraConfig = new();
        public LODSettings lod = new();

        [Header("World Bounds")]
        public WorldBoundsSettings worldBounds = new();

        [Header("Debug")] 
        public DebugSettings debug = new();

        private ITerrainOrchestrator orchestrator;

        // ITerrainHost explicit implementations (Unity serializes fields, not auto-properties)
        TerrainSettings ITerrainHost.Terrain => terrain;
        NoiseSettings ITerrainHost.Noise => noise;
        
        CameraSettings ITerrainHost.CameraConfig => cameraConfig;
        WorldBoundsSettings ITerrainHost.WorldBounds => worldBounds;
        LODSettings ITerrainHost.LOD => lod;
        DebugSettings ITerrainHost.Debug => debug;
        TerrainChunk ITerrainHost.ChunkPrefab => chunkPrefab;
        
        Transform ITerrainHost.Transform => transform;
        Transform ITerrainHost.ChunkParent => chunksContainer.transform;
        int ITerrainHost.ChunkLayer => gameObject.layer;

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
                return Array.Empty<int>();
            }

            return orchestrator.GetPrecalculatedTriangles(resolution);
        }

        private void OnValidate()
        {
            NormalizeSettings();
        }

        private void Awake()
        {
            if (!ValidateAndNormalizeSettings())
            {
                enabled = false;
                return;
            }

            RecreateOrchestrator();
        }

        private void OnDestroy()
        {
            orchestrator?.Destroy();
            orchestrator = null;
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (!buildOnStart) return;
#endif

            orchestrator?.BuildTerrain();
        }

        public void ApplyBuildSettings(
            TerrainSettings terrainSettings, 
            NoiseSettings noiseSettings,
            bool rebuildTerrain = true)
        {
            if (terrainSettings == null)
            {
                throw new ArgumentNullException(nameof(terrainSettings));
            }
            if (noiseSettings == null)
            {
                throw new ArgumentNullException(nameof(noiseSettings));
            }

            terrain ??= new TerrainSettings();
            noise ??= new NoiseSettings();

            CopyTerrainSettings(terrainSettings, terrain);
            CopyNoiseSettings(noiseSettings, noise);
            NormalizeSettings();

            RecreateOrchestrator();

            if (rebuildTerrain && orchestrator != null)
            {
                orchestrator.BuildTerrain();
            }
        }

        private void Update()
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
            var worldDistance = chunkDistance * chunkWorldSize;
            float maxHeight = terrain.maxElevationStepsCount * terrain.elevationStepHeight;

            var center = cameraConfig.reference.transform.position;
            center.y += maxHeight * 0.5f;

            Vector3 size = new(worldDistance * 2f, maxHeight, worldDistance * 2f);

            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
        }

        private void EnsureChunksContainer()
        {
            if (chunksContainer)
            {
                return;
            }

            chunksContainer = GetComponentInChildren<ChunksContainer>(true);
            if (chunksContainer)
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
            lod?.ClampValues(terrain?.chunkSize ?? 1);
            debug?.ClampValues();
        }

        private bool ValidateAndNormalizeSettings()
        {
            NormalizeSettings();

            if (!chunkPrefab)
            {
                Debug.LogError(
                    "[ProceduralTerrain] Chunk prefab is not assigned. Assign it under Prefabs > Chunk Prefab in the Inspector.",
                    this
                );
                return false;
            }

            if (cameraConfig != null && cameraConfig.reference) return true;
            
            Debug.LogError(
                "[ProceduralTerrain] Camera reference is not assigned. Assign it under Camera Settings > Reference in the Inspector.",
                this
            );
            return false;

        }

        private void RecreateOrchestrator()
        {
            EnsureChunksContainer();
            orchestrator?.Destroy();
            orchestrator = new TerrainDataGenerator(this);
        }

        private static void CopyTerrainSettings(TerrainSettings source, TerrainSettings target)
        {
            target.chunkSize = source.chunkSize;
            target.tileSize = source.tileSize;
            target.elevationStepHeight = source.elevationStepHeight;
            target.maxElevationStepsCount = source.maxElevationStepsCount;
            target.skirtDepth = source.skirtDepth;
            target.orphanSweepPeriod = source.orphanSweepPeriod;
        }

        private static void CopyNoiseSettings(NoiseSettings source, NoiseSettings target)
        {
            target.seed = source.seed;
            target.scale = source.scale;
            target.octaves = source.octaves;
            target.persistence = source.persistence;
            target.lacunarity = source.lacunarity;
        }

        private void OnDrawGizmosSelected()
        {
            DrawViewDistanceGizmo();

            if (debug is not { showLodRanges: true })
            {
                return;
            }

            if (cameraConfig == null || !cameraConfig.reference || terrain == null)
            {
                return;
            }

            DrawLodRangeGizmo(lod.lod1Distance, new Color(1f, 0.84f, 0.2f, 1f));
            DrawLodRangeGizmo(lod.lod2Distance, new Color(1f, 0.45f, 0.1f, 1f));
        }

        private void DrawViewDistanceGizmo()
        {
            if (debug is not { showViewDistance: true })
            {
                return;
            }

            if (cameraConfig == null || !cameraConfig.reference || terrain == null)
            {
                return;
            }

            var chunkWorldSize = terrain.chunkSize * terrain.tileSize;
            var viewWorldRadius = (cameraConfig.viewDistanceChunks + 0.5f) * chunkWorldSize;
            var center = cameraConfig.reference.transform.position;

            Gizmos.color = new Color(0f, 0.8f, 1f, 1f);
            Gizmos.DrawWireCube(
                center,
                new Vector3(viewWorldRadius * 2f, 1f, viewWorldRadius * 2f)
            );
            
#if UNITY_EDITOR
            UnityEditor.Handles.color = new Color(0f, 0.8f, 1f, 1f);
            UnityEditor.Handles.Label(
                center + Vector3.right * viewWorldRadius,
                $"View: {cameraConfig.viewDistanceChunks} chunks\n({viewWorldRadius * 2f:F0} wu wide)"
            );
#endif
        }
    }
}