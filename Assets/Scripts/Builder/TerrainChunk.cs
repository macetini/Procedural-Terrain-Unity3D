using ProceduralTerrain.Effects;
using ProceduralTerrain.Processing;
using ProceduralTerrain.Settings;
using UnityEngine;

namespace ProceduralTerrain.Builder
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class TerrainChunk : MonoBehaviour
    {
        private const string Name = "TerrainChunk:";

        [Header("Settings")]
        public Material terrainMaterial;

        [Header("Debug Settings")]
        public bool showChunkLodBounds;

        public bool showNormals;
        public int debugNormalLength = 10;

        public bool showColliderBounds;

        [Header("Effects")]
        public TerrainFadeEffect fadeEffect;

        public bool IsVisible { get; private set; } = true;
        public int CurrentStep => chunkGenerator.CurrentStep;
        public Vector2Int ChunkCoord { get; private set; }

        public ITerrainHost Generator { get; private set; }

        internal MeshRenderer RendererReference { get; private set; }

        internal MeshFilter FilterReference { get; private set; }

        // References
        private MeshCollider colliderReference;

        // Data

        // Calculations
        private bool wasVisibleLastCheck; // Track state change

        private MeshGenerator chunkGenerator;

        private void Awake()
        {
            EnsureReferences();
            colliderReference.convex = false;

            chunkGenerator = new MeshGenerator(this);
        }

        private void EnsureReferences()
        {
            if (!RendererReference)
            {
                RendererReference = GetComponent<MeshRenderer>();
            }

            if (!FilterReference)
            {
                FilterReference = GetComponent<MeshFilter>();
            }

            if (!colliderReference)
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

            if (colliderReference)
            {
                colliderReference.enabled = true;
            }

            if (fadeEffect)
            {
                fadeEffect.ResetEffect();
            }
        }

        private void OnDestroy()
        {
            if (FilterReference != null && FilterReference.sharedMesh != null)
            {
                Destroy(FilterReference.sharedMesh);
            }
        }

        public void InitBuild(ITerrainHost generator, Vector2Int chunkCoord)
        {
            this.Generator = generator;
            this.ChunkCoord = chunkCoord;

#if UNITY_EDITOR
            var local = $"{Name} ({chunkCoord.x:D3}, {chunkCoord.y:D3})";
            gameObject.name = local;
#endif

            if (terrainMaterial && RendererReference)
            {
                RendererReference.material = terrainMaterial;
                showNormals = generator.Debug.showNormals;
            }

            chunkGenerator.Init(generator, chunkCoord);
        }

        public void SyncColliderMesh()
        {
            var canSyncCollider = colliderReference
                && FilterReference
                && FilterReference.sharedMesh;

            if (!canSyncCollider)
            {
                return;
            }
            
            colliderReference.sharedMesh = null; // Force recook when reusing same mesh instance.
            colliderReference.sharedMesh = FilterReference.sharedMesh; // Assign new mesh
        }

        public void UpdateLOD(bool force = false)
        {
            chunkGenerator.UpdateLOD(force);
        }

        public void UpdateVisibility(Plane[] planes)
        {
            var settings = chunkGenerator.Settings;
            var worldCenter = transform.position + settings.VisibilityBoundsOffset;
            Bounds checkBounds = new(worldCenter, settings.VisibilityBoundsSize);

            // 1. Calculate logical visibility (Frustum check)
            var frustumVisible = GeometryUtility.TestPlanesAABB(planes, checkBounds);
            IsVisible = frustumVisible;

            var finalShowState = frustumVisible && chunkGenerator.IsMeshReady;
            var becameVisible = finalShowState && !wasVisibleLastCheck;

            if (fadeEffect && becameVisible)
            {
                fadeEffect.Play();
            }

            if (RendererReference.enabled != finalShowState)
            {
                RendererReference.enabled = finalShowState;
            }

            UpdateColliderVisibility(finalShowState, becameVisible);

            wasVisibleLastCheck = finalShowState;
        }

        private void UpdateColliderVisibility(bool finalShowState, bool becameVisible)
        {
            if (!colliderReference)
            {
                return;
            }

            var isMeshMissingOrStale = !colliderReference.sharedMesh
                || !FilterReference
                || colliderReference.sharedMesh != FilterReference.sharedMesh;

            var colliderMeshMissingOrStale = finalShowState && isMeshMissingOrStale;

            if (becameVisible || colliderMeshMissingOrStale)
            {
                SyncColliderMesh();
            }

            if (colliderReference.enabled != finalShowState)
            {
                colliderReference.enabled = finalShowState;
            }
        }

        // ------------------------------------------------------------------------------------------------
        // -------------------------------------------- [Effects] -----------------------------------------
        // ------------------------------------------------------------------------------------------------

        public void StartFadeIn()
        {
            if (fadeEffect)
            {
                fadeEffect.Play();
            }
        }

        // ------------------------------------------------------------------------------------------------
        // -------------------------------------------- [Gizmos] ------------------------------------------
        // ------------------------------------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (Generator is not { Debug: not null }) return;
            
            EnsureReferences();

            DrawChunkBoundsGizmos();
            DrawColliderGizmo();
            DrawNormalGizmos();
        }

        private void DrawChunkBoundsGizmos()
        {
            var showBounds = Generator.Debug.showChunkLodBounds || showChunkLodBounds;
            var hasValidStep = chunkGenerator is { CurrentStep: >= 0 };

            if (!showBounds || !hasValidStep)
            {
                return;
            }
            
            var settings = chunkGenerator.Settings;
            var center = transform.position + settings.VisibilityBoundsOffset;

            Gizmos.color = GetLodGizmoColor(chunkGenerator.CurrentStep, Generator.LOD);
            Gizmos.DrawWireCube(center, settings.VisibilityBoundsSize);
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

            return currentStep == lod.lod2Step ? new Color(1f, 0.45f, 0.1f, 1f) : Color.gray;
        }

        private void DrawColliderGizmo()
        {
            if (!Generator.Debug.showColliderBounds && !showColliderBounds)
            {
                return;
            }

            if (colliderReference == null || !colliderReference.enabled)
            {
                return;
            }

            var colliderMesh = colliderReference.sharedMesh;
            if (colliderMesh == null)
            {
                return;
            }

            var bounds = colliderMesh.bounds;
            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            Gizmos.matrix = previousMatrix;
        }

        private void DrawNormalGizmos()
        {
            if (!IsVisible)
            {
                return;
            }

            if (chunkGenerator == null || chunkGenerator.CurrentStep <= 0)
            {
                return;
            }

            var showNormalsEnabled = Generator.Debug.showNormals || showNormals;
            var hasMesh = FilterReference != null && FilterReference.sharedMesh != null;

            if (!showNormalsEnabled || !hasMesh)
            {
                return;
            }

            var mesh = FilterReference.sharedMesh;
            var verts = mesh.vertices;
            var norms = mesh.normals;

            if (verts == null || norms == null)
            {
                return;
            }

            Gizmos.color = Color.blue;
            // We only loop through the grid vertices (ignore the skirt for clarity)
            var resolution =
                (chunkGenerator.Settings.ChunkSize / chunkGenerator.CurrentStep) + 1;
            var gridCount = resolution * resolution;
            var drawCount = Mathf.Min(gridCount, verts.Length, norms.Length);

            for (var i = 0; i < drawCount; i++)
            {
                // Transform the local vertex position to world space
                var worldV = transform.TransformPoint(verts[i]);
                // Transform the normal to world space
                var worldN = transform.TransformDirection(norms[i]);
                // Draw the normal line (DebugNormalLength is the length of the line)
                Gizmos.DrawLine(worldV, worldV + worldN * debugNormalLength);
            }
        }
    }
}
