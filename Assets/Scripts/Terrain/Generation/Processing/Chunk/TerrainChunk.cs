using Assets.Scripts.Terrain.Effects;
using UnityEngine;

namespace Assets.Scripts.Terrain.Generation.Processing.Chunk
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TerrainChunk : MonoBehaviour
    {
        private const string MESH_IDENTIFIER = "TerrainChunk_";

        [Header("Settings")]
        public Material terrainMaterial;

        [Header("Debug Settings")]
        public bool DebugNormals = false;

        [Header("Effects")]
        public TerrainFadeEffect fadeEffect;

        public bool IsVisible { get; private set; } = true;
        public int CurrentStep { get; private set; } = -1;
        public Vector2Int ChunkCoord => chunkCoord;
        public ProceduralTerrain Generator => generator;

        // References
        private MeshRenderer rendererReference;
        private MeshFilter filterReference;

        // Data
        private ProceduralTerrain generator;
        private Vector2Int chunkCoord;
        private ChunkSettings settings;

        // Calculations
        private bool wasVisibleLastCheck = false; // Track state change
        private bool isMeshReady = false; // Prevents "Blips" before the first build
        private string meshName;

        private readonly ChunkDataProcessor processor = new();

        void Awake()
        {
            rendererReference = GetComponent<MeshRenderer>();
            filterReference = GetComponent<MeshFilter>();
        }

        public void CallDestroy() // WARNING: Not optimized. Should only be only used during the development phase.
        {
            Destroy(gameObject);
        }

        public void PrepareForPool()
        {
            CurrentStep = -1;
            isMeshReady = false;
            wasVisibleLastCheck = false;
            IsVisible = true;

            if (fadeEffect != null)
                fadeEffect.ResetEffect();
        }

        void OnDestroy()
        {
            if (filterReference != null && filterReference.sharedMesh != null)
            {
                Destroy(filterReference.sharedMesh);
            }
        }

        public void InitBuild(ProceduralTerrain generator, Vector2Int chunkCoord)
        {
            this.generator = generator;
            this.chunkCoord = chunkCoord;
            settings = ChunkSettings.FromGenerator(generator);

            rendererReference.enabled = false;
            isMeshReady = false;

            meshName = MESH_IDENTIFIER + chunkCoord.x + "_" + chunkCoord.y;

            if (terrainMaterial != null)
            {
                // Use sharedMaterial to allow the GPU to batch all chunks together
                rendererReference.sharedMaterial = terrainMaterial;
            }

            processor.Init(generator);
            UpdateLOD(true);
        }

        public void UpdateLOD(bool force = false)
        {
            int targetStep = GetTargetStep();

            // Only rebuild if the LOD changed OR we are forcing it (initial build)
            if (targetStep != CurrentStep || force)
            {
                CurrentStep = targetStep;
                BuildProceduralMesh();
            }
        }

        private int GetTargetStep()
        {
            // Calculate center for more accurate LOD switching
            float halfSize = settings.ChunkBoundSize * 0.5f;
            Vector3 center = transform.position + new Vector3(halfSize, 0, halfSize);

            float dist = Vector3.Distance(
                center,
                generator.cameraConfig.reference.transform.position
            );

            if (dist > generator.lod.distance2)
            {
                return generator.lod.step2; // LOD 2 (low)
            }
            if (dist > generator.lod.distance1)
            {
                return generator.lod.step1; // LOD 1 (medium)
            }
            return generator.lod.step0; // LOD 0 (full detail)
        }

        private void BuildProceduralMesh()
        {
            processor.BuildMeshData(CurrentStep, chunkCoord);
            processor.GenerateGeometryData();
            processor.CalculateNormals();

            Mesh mesh = CreateRawMesh();
            processor.PopulateMesh(mesh);

            isMeshReady = true;
            FinalizeMesh(mesh);
        }

        private Mesh CreateRawMesh()
        {
            if (filterReference.sharedMesh == null)
            {
                filterReference.sharedMesh = new Mesh { name = meshName };
                filterReference.sharedMesh.MarkDynamic();
            }
            else
            {
                filterReference.sharedMesh.name = meshName;
            }
            return filterReference.sharedMesh;
        }

        private void FinalizeMesh(Mesh mesh)
        {
            float maxHeight = settings.MaxElevationStep * settings.ElevationStepHeight;

            // We center the bounds and apply the public frustumPadding
            Vector3 center = new(
                settings.ChunkBoundSize * 0.5f,
                maxHeight * 0.5f,
                settings.ChunkBoundSize * 0.5f
            );
            Vector3 size = new(
                settings.ChunkBoundSize + settings.FrustumPadding,
                maxHeight + settings.SkirtDepth + settings.FrustumPadding,
                settings.ChunkBoundSize + settings.FrustumPadding
            );
            mesh.bounds = new Bounds(center, size);

            if (!rendererReference.enabled)
                rendererReference.enabled = true;
        }

        public void UpdateVisibility(Plane[] planes)
        {
            float halfSize = settings.ChunkBoundSize * 0.5f;
            float height = settings.MaxElevationStep * settings.ElevationStepHeight;

            // Use world space center
            Vector3 worldCenter =
                transform.position + new Vector3(halfSize, height * 0.5f, halfSize);
            Vector3 size = new(
                settings.ChunkBoundSize + settings.FrustumPadding,
                height + settings.SkirtDepth + settings.FrustumPadding,
                settings.ChunkBoundSize + settings.FrustumPadding
            );
            Bounds checkBounds = new(worldCenter, size);

            // 1. Calculate logical visibility (Frustum check)
            bool frustumVisible = GeometryUtility.TestPlanesAABB(planes, checkBounds);
            IsVisible = frustumVisible;

            bool finalShowState = frustumVisible && isMeshReady;

            if (fadeEffect != null && finalShowState && !wasVisibleLastCheck)
            {
                fadeEffect.Play();
            }

            if (rendererReference.enabled != finalShowState)
            {
                rendererReference.enabled = finalShowState;
            }

            wasVisibleLastCheck = finalShowState;
        }

        // ------------------------------------------------------------------------------------------------
        // -------------------------------------------- [Effects] -----------------------------------------
        // ------------------------------------------------------------------------------------------------

        public void StartFadeIn()
        {
            if (fadeEffect != null)
                fadeEffect.Play();
        }

        // ------------------------------------------------------------------------------------------------
        // -------------------------------------------- [Gizmos] ------------------------------------------
        // ------------------------------------------------------------------------------------------------

        void OnDrawGizmosSelected()
        {
            if (!DebugNormals || generator == null)
            {
                return;
            }

            Mesh mesh = filterReference.sharedMesh;
            if (mesh != null)
            {
                Vector3[] verts = filterReference.sharedMesh.vertices;
                Vector3[] norms = mesh.normals;

                Gizmos.color = Color.blue;
                // We only loop through the grid vertices (ignore the skirt for clarity)
                int resolution = (settings.ChunkSize / CurrentStep) + 1;
                int gridCount = resolution * resolution;

                for (int i = 0; i < gridCount; i++)
                {
                    // Transform the local vertex position to world space
                    Vector3 worldV = transform.TransformPoint(verts[i]);
                    // Transform the normal to world space
                    Vector3 worldN = transform.TransformDirection(norms[i]);
                    // Draw the normal line (0.5f is the length of the line)
                    Gizmos.DrawLine(worldV, worldV + worldN * 0.5f);
                }
            }
        }
    }
}
