using UnityEngine;

namespace Assets.Scripts.Terrain
{
    internal class RuntimeState
    {
        public Vector2Int CurrentCameraPosition = Vector2Int.zero;
        public Plane[] CameraPlanes;
        public float ChunkBoundSize;
        public bool WorldMonitoringActive = true;
    }
}
