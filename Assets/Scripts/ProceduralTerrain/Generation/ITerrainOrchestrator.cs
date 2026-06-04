using ProceduralTerrain.Processing.Chunk.Data;
using UnityEngine;

namespace ProceduralTerrain.Generation
{
    /// <summary>
    /// Coordinates terrain runtime lifecycle and chunk generation flow.
    /// </summary>
    public interface ITerrainOrchestrator
    {
        /// <summary>Builds initial terrain around the current camera chunk.</summary>
        void BuildTerrain();

        /// <summary>Resets runtime state and clears generated world data.</summary>
        void ResetGeneratorState();

        /// <summary>Updates the tracked camera chunk position.</summary>
        void UpdateCurrentCameraPosition();

        /// <summary>Releases runtime resources.</summary>
        void Destroy();

        /// <summary>Returns neighboring sampled data for a chunk coordinate.</summary>
        NeighborStruct GetNeighborGrids(Vector2Int chunkCoord);

        /// <summary>Gets cached or generated triangle indices for the given mesh resolution.</summary>
        int[] GetPrecalculatedTriangles(int resolution);
    }
}
