using ProceduralTerrain.Builder;
using UnityEngine;

namespace ProceduralTerrain.Processing
{
    public class MeshGenerator
    {
        private const string MeshIdentifier = "TerrainChunk_";
        
        private readonly DataProcessor processor = new();

        private ITerrainHost host;
        private Vector2Int chunkCoord;
        private RuntimeSettings settings;
        private Transform cameraTransform;
        private string meshName;

        private readonly TerrainChunk chunk;

        public int CurrentStep { get; private set; } = -1;
        public bool IsMeshReady { get; private set; }
        public RuntimeSettings Settings => settings;

        public MeshGenerator(TerrainChunk chunk)
        {
            this.chunk = chunk;
        }

        public void Reset()
        {
            CurrentStep = -1;
            IsMeshReady = false;
        }

        public void Init(ITerrainHost hostLoc, Vector2Int chunkCoordLoc)
        {
            this.host = hostLoc;
            this.chunkCoord = chunkCoordLoc;
            settings = RuntimeSettings.FromHost(hostLoc);
            if (hostLoc.CameraConfig != null && hostLoc.CameraConfig.reference)
            {
                cameraTransform = hostLoc.CameraConfig.reference.transform;
            }
            else
            {
                // Defensive fallback to avoid hard null-reference crashes on misconfigured scenes.
                cameraTransform = chunk.transform;
                Debug.LogWarning(
                    "[ChunkGenerator] Camera reference is missing. Falling back to chunk transform for LOD distance.",
                    chunk
                );
            }

            chunk.RendererReference.enabled = false;
            IsMeshReady = false;

            meshName = $"{MeshIdentifier}{chunkCoordLoc.x}_{chunkCoordLoc.y}";

            if (chunk.terrainMaterial)
            {
                // Use sharedMaterial to allow the GPU to batch all chunks together
                chunk.RendererReference.sharedMaterial = chunk.terrainMaterial;
            }

            processor.Init(
                settings,
                hostLoc.GetNeighborGrids,
                hostLoc.GetPrecalculatedTriangles
            );
            UpdateLOD(true);
        }

        public void UpdateLOD(bool force = false)
        {
            var targetStep = GetTargetStep();

            // Only rebuild if the LOD changed OR we are forcing it (initial build)
            if (targetStep == CurrentStep && !force) return;
            
            CurrentStep = targetStep;
            BuildProceduralMesh();
        }

        private int GetTargetStep() =>
            LodSelector.GetStep(
                settings,
                chunk.transform.position,
                cameraTransform,
                host.LOD.lod0Step,
                host.LOD.lod1Step,
                host.LOD.lod2Step
            );

        private void BuildProceduralMesh()
        {
            processor.BuildMeshData(CurrentStep, chunkCoord);
            processor.GenerateGeometryData();
            processor.CalculateNormals();

            var mesh = CreateRawMesh();
            processor.PopulateMesh(mesh);

            FinalizeMesh(mesh);
            SyncChunkPresentation();

            IsMeshReady = true;
        }

        private Mesh CreateRawMesh()
        {
            var filter = chunk.FilterReference;
            if (!filter.sharedMesh)
            {
                filter.sharedMesh = new Mesh { name = meshName };
                filter.sharedMesh.MarkDynamic();
            }
            else
            {
                filter.sharedMesh.name = meshName;
            }

            return filter.sharedMesh;
        }

        private void FinalizeMesh(Mesh mesh)
        {
            var maxHeight = settings.MaxElevationStep * settings.ElevationStepHeight;

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
        }

        private void SyncChunkPresentation()
        {
            if (!chunk.RendererReference.enabled)
            {
                chunk.RendererReference.enabled = true;
            }

            chunk.SyncColliderMesh();
        }
    }
}
