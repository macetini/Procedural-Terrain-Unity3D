using ProceduralTerrain.Effects;
using ProceduralTerrain.Processing;
using ProceduralTerrain.Settings;
using UnityEngine;

namespace ProceduralTerrain.Runtime
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class TerrainChunk : MonoBehaviour
    {
        public const string NAME = "TerrainChunk:";

        [Header("Settings")]
        public Material terrainMaterial;

        [Header("Debug Settings")]
        public bool showChunkLodBounds = false;

        public bool showNormals = false;
        public int debugNormalLength = 5;

        public bool showCollider = false;

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
        private MeshCollider colliderReference;

        // Data
        private ITerrainHost generator;
        private Vector2Int chunkCoord;

        // Calculations
        private bool wasVisibleLastCheck = false; // Track state change

        private MeshGenerator chunkGenerator;

        void Awake()
        {
            EnsureReferences();
            colliderReference.convex = false;

            chunkGenerator = new MeshGenerator(this);
        }

        private void EnsureReferences()
        {
            if (rendererReference == null)
            {
                rendererReference = GetComponent<MeshRenderer>();
            }

            if (filterReference == null)
            {
                filterReference = GetComponent<MeshFilter>();
            }

            if (colliderReference == null)
            {
                colliderReference = GetComponent<MeshCollider>();
            }
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

            if (colliderReference != null)
            {
                colliderReference.enabled = true;
            }

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

#if UNITY_EDITOR
            string local = $"{NAME} ({chunkCoord.x:D3}, {chunkCoord.y:D3})";
            gameObject.name = local;
#endif

            if (terrainMaterial != null && rendererReference != null)
            {
                rendererReference.material = terrainMaterial;
                showNormals = generator.Debug.showNormals;
                showCollider = generator.Debug.showColliders;
            }

            chunkGenerator.Init(generator, chunkCoord);
        }

        public void SyncColliderMesh()
        {
            if (
                colliderReference != null
                && filterReference != null
                && filterReference.sharedMesh != null
            )
            {
                colliderReference.sharedMesh = null; // Clear old mesh first
                colliderReference.sharedMesh = filterReference.sharedMesh; // Assign new mesh
            }
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

            if (colliderReference != null && colliderReference.enabled != finalShowState)
            {
                colliderReference.enabled = finalShowState;
            }

            wasVisibleLastCheck = finalShowState;
        }

        // ------------------------------------------------------------------------------------------------
        // -------------------------------------------- [Effects] -----------------------------------------
        // ------------------------------------------------------------------------------------------------

        public void StartFadeIn()
        {
            if (fadeEffect != null)
            {
                fadeEffect.Play();
            }
        }

        // ------------------------------------------------------------------------------------------------
        // -------------------------------------------- [Gizmos] ------------------------------------------
        // ------------------------------------------------------------------------------------------------

        void OnDrawGizmos()
        {
            if (generator != null && generator.Debug != null)
            {
                EnsureReferences();

                DrawChunkBoundsGizmos();
                DrawColliderBoundsGizmos();
                DrawNormalGizmos();
            }
        }

        private void DrawChunkBoundsGizmos()
        {
            if (
                (generator.Debug.showChunkLodBounds || showChunkLodBounds)
                && chunkGenerator != null
                && chunkGenerator.CurrentStep >= 0
            )
            {
                var settings = chunkGenerator.Settings;
                Vector3 center = transform.position + settings.VisibilityBoundsOffset;

                Gizmos.color = GetLodGizmoColor(chunkGenerator.CurrentStep, generator.LOD);
                Gizmos.DrawWireCube(center, settings.VisibilityBoundsSize);
            }
        }

        private static Color GetLodGizmoColor(int currentStep, LODSettings lod)
        {
            if (currentStep == lod.lod0Step)
            {
                return Color.green;
            }

            if (currentStep == lod.lod1Step)
            {
                return Color.yellow;
            }

            if (currentStep == lod.lod2Step)
            {
                return new Color(1f, 0.45f, 0.1f, 1f);
            }

            return Color.gray;
        }

        private void DrawColliderBoundsGizmos()
        {
            if ((generator.Debug.showColliders || showCollider) && colliderReference != null)
            {
                Gizmos.color = Color.red;
                Bounds colliderBounds = colliderReference.bounds;
                Gizmos.DrawWireCube(colliderBounds.center, colliderBounds.size);
            }
        }

        private void DrawNormalGizmos()
        {
            if (
                (generator.Debug.showNormals || showNormals)
                && filterReference != null
                && filterReference.sharedMesh != null
            )
            {
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
                        // Draw the normal line (DebugNormalLength is the length of the line)
                        Gizmos.DrawLine(worldV, worldV + worldN * debugNormalLength);
                    }
                }
            }
        }
    }
}
