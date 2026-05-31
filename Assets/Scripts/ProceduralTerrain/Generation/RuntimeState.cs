using UnityEngine;

namespace ProceduralTerrain.Generation
{
    internal class RuntimeState
    {
        public Vector2Int CurrentCameraPosition = Vector2Int.zero;
        public readonly Plane[] CameraPlanes = new Plane[6];
        public float ChunkBoundSize;
        public bool WorldMonitoringActive = true;

        /// <summary>
        /// Cooperative cancellation flag. Set before StopAllCoroutines so the build
        /// queue exits its loop cleanly at the next chunk boundary rather than being
        /// killed mid-step without cleanup.
        /// </summary>
        public bool IsBuildCancelled = false;
    }
}
