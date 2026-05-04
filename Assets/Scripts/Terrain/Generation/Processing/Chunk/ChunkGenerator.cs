using UnityEngine;

namespace Assets.Scripts.Terrain.Generation.Processing.Chunk
{
    public class ChunkGenerator
    {
        private const string MESH_IDENTIFIER = "TerrainChunk_";

        private readonly ChunkDataProcessor processor = new();

        private ProceduralTerrain generator;
        private Vector2Int chunkCoord;
        private ChunkSettings settings;
        private Transform cameraTransform;
        private string meshName;

        private readonly TerrainChunk chunk;

        public int CurrentStep { get; private set; } = -1;
        public bool IsMeshReady { get; private set; } = false;
        public ChunkSettings Settings => settings;

        public ChunkGenerator(TerrainChunk chunk)
        {
            this.chunk = chunk;
        }

        public void Reset()
        {
            CurrentStep = -1;
            IsMeshReady = false;
        }

        public void Init(ProceduralTerrain generator, Vector2Int chunkCoord)
        {
            this.generator = generator;
            this.chunkCoord = chunkCoord;
            settings = ChunkSettings.FromGenerator(generator);
            cameraTransform = generator.cameraConfig.reference.transform;

            chunk.RendererReference.enabled = false;
            IsMeshReady = false;

            meshName = $"{MESH_IDENTIFIER}{chunkCoord.x}_{chunkCoord.y}";

            if (chunk.terrainMaterial != null)
            {
                // Use sharedMaterial to allow the GPU to batch all chunks together
                chunk.RendererReference.sharedMaterial = chunk.terrainMaterial;
            }

            processor.Init(
                settings,
                coord => generator.GetNeighborGrids(coord),
                resolution => generator.GetPrecalculatedTriangles(resolution)
            );
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
            Vector3 center = chunk.transform.position + new Vector3(halfSize, 0, halfSize);

            Vector3 diff = center - cameraTransform.position;
            float sqrDist = diff.sqrMagnitude;

            if (sqrDist > settings.SqrLodDistance2)
            {
                return generator.lod.step2; // LOD 2 (low)
            }
            if (sqrDist > settings.SqrLodDistance1)
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

            IsMeshReady = true;
            FinalizeMesh(mesh);
        }

        private Mesh CreateRawMesh()
        {
            var filter = chunk.FilterReference;
            if (filter.sharedMesh == null)
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

            if (!chunk.RendererReference.enabled)
                chunk.RendererReference.enabled = true;
        }
    }
}
