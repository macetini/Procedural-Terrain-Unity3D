using Assets.Scripts.Terrain.Chunk;
using Assets.Scripts.Terrain.Chunk.Data;
using Assets.Scripts.Terrain.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Terrain
{
    public class TerrainGenerator : MonoBehaviour
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

        private TerrainController terrainController;

        public ChunkNeighborStruct GetNeighborGrids(Vector2Int chunkCoord) =>
            terrainController.GetNeighborGrids(chunkCoord);

        public int[] GetPrecalculatedTriangles(int resolution) =>
            terrainController.GetPrecalculatedTriangles(resolution);

        void Awake()
        {
            terrainController = new TerrainController(this);
        }

        void OnDestroy()
        {
            terrainController.Destroy();
        }

        void Start()
        {
            if (cameraConfig.reference == null)
            {
                Debug.LogError(
                    "[TerrainGenerator] Camera reference is not assigned. Assign it under Camera Settings > Reference in the Inspector.",
                    this
                );
                return;
            }
            terrainController.BuildTerrain();
        }

        void Update()
        {
            terrainController.UpdateCurrentCameraPosition();

            // WARNING: This will rebuild the whole terrain. Should only be used during development.
            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                HandleDebugRebuild();
            }
        }

        private void HandleDebugRebuild()
        {
            Debug.Log("Rebuilding terrain.");
            terrainController.ResetGeneratorState();
            terrainController.BuildTerrain();
        }
    }
}
