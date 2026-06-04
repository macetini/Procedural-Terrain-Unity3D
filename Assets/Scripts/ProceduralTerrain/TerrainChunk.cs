using ProceduralTerrain.Effects;
using ProceduralTerrain.Processing.Chunk;
using UnityEngine;

namespace ProceduralTerrain
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TerrainChunk : MonoBehaviour
    {
        [Header("Settings")]
        public Material terrainMaterial;

        [Header("Debug Settings")]
        public bool DebugNormals = false;

        [Header("Effects")]
        public TerrainFadeEffect fadeEffect;

        public bool IsVisible { get; private set; } = true;
        public int CurrentStep => chunkGenerator.CurrentStep;
        public Vector2Int ChunkCoord => chunkCoord;
        public ITerrainHost Generator => generator;

        internal MeshRenderer RendererReference => rendererReference;
        internal MeshFilter FilterReference => filterReference;

        // References
        private MeshRenderer rendererReference;
        private MeshFilter filterReference;

        // Data
        private ITerrainHost generator;
        private Vector2Int chunkCoord;

        // Calculations
        private bool wasVisibleLastCheck = false; // Track state change

        private MeshGenerator chunkGenerator;

        void Awake()
        {
            rendererReference = GetComponent<MeshRenderer>();
            filterReference = GetComponent<MeshFilter>();
            chunkGenerator = new MeshGenerator(this);
        }

        public void CallDestroy() // Development fallback path; not pool-optimized.
        {
            Destroy(gameObject);
        }

        public void PrepareForPool()
        {
            chunkGenerator.Reset();
            wasVisibleLastCheck = false;
            IsVisible = true;

            if (fadeEffect != null)
            {
                fadeEffect.ResetEffect();
            }
        }

        void OnDestroy()
        {
            if (filterReference != null && filterReference.sharedMesh != null)
            {
                Destroy(filterReference.sharedMesh);
            }
        }

        public void InitBuild(ITerrainHost generator, Vector2Int chunkCoord)
        {
            this.generator = generator;
            this.chunkCoord = chunkCoord;
            chunkGenerator.Init(generator, chunkCoord);
        }

        public void UpdateLOD(bool force = false)
        {
            chunkGenerator.UpdateLOD(force);
        }

        public void UpdateVisibility(Plane[] planes)
        {
            var settings = chunkGenerator.Settings;
            Vector3 worldCenter = transform.position + settings.VisibilityBoundsOffset;
            Bounds checkBounds = new(worldCenter, settings.VisibilityBoundsSize);

            // 1. Calculate logical visibility (Frustum check)
            bool frustumVisible = GeometryUtility.TestPlanesAABB(planes, checkBounds);
            IsVisible = frustumVisible;

            bool finalShowState = frustumVisible && chunkGenerator.IsMeshReady;

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
                int resolution =
                    (chunkGenerator.Settings.ChunkSize / chunkGenerator.CurrentStep) + 1;
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
