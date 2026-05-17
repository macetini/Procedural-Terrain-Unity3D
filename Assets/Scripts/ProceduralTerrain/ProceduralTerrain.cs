using Assets.Scripts.ProceduralTerrain.Generation;
using SSHexMap.Terrain.Data;
using SSHexMap.Terrain.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.ProceduralTerrain
{
    public class ProceduralTerrain : MonoBehaviour
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

        private TerrainDataGenerator dataGenerator;

        public ChunkNeighborStruct GetNeighborGrids(Vector2Int chunkCoord) =>
            dataGenerator.GetNeighborGrids(chunkCoord);

        public int[] GetPrecalculatedTriangles(int resolution) =>
            dataGenerator.GetPrecalculatedTriangles(resolution);

        void Awake()
        {
            dataGenerator = new TerrainDataGenerator(this);
        }

        void OnDestroy()
        {
            dataGenerator.Destroy();
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
            dataGenerator.BuildTerrain();
        }

        void Update()
        {
            dataGenerator.UpdateCurrentCameraPosition();

#if UNITY_EDITOR
            // WARNING: This will rebuild the whole terrain. Should only be used during development.
            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                HandleDebugRebuild();
            }
#endif
        }

        private void HandleDebugRebuild()
        {
            Debug.Log("Rebuilding terrain.");
            dataGenerator.ResetGeneratorState();
            dataGenerator.BuildTerrain();
        }
    }
}
