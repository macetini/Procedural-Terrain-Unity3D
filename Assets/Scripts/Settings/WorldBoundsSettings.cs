using UnityEngine;

namespace ProceduralTerrain.Settings
{
    [System.Serializable]
    public class WorldBoundsSettings
    {
        [Tooltip("Half-width of the playable area in chunks from the world origin. Set to 0 to disable bounds.")]
        [Min(0)]
        public int maxChunkRadius = 0;

        /// <summary>Returns true when bounds limiting is active.</summary>
        public bool IsEnabled => maxChunkRadius > 0;

        /// <summary>Clamps a chunk coordinate to within the configured radius.</summary>
        public Vector2Int Clamp(Vector2Int chunkCoord)
        {
            if (!IsEnabled)
            {
                return chunkCoord;
            }

            return new Vector2Int(
                Mathf.Clamp(chunkCoord.x, -maxChunkRadius, maxChunkRadius),
                Mathf.Clamp(chunkCoord.y, -maxChunkRadius, maxChunkRadius)
            );
        }

        /// <summary>Returns true when the given chunk coordinate is within bounds.</summary>
        public bool Contains(Vector2Int chunkCoord)
        {
            if (!IsEnabled)
            {
                return true;
            }

            return Mathf.Abs(chunkCoord.x) <= maxChunkRadius
                && Mathf.Abs(chunkCoord.y) <= maxChunkRadius;
        }
    }
}
