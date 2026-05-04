using UnityEngine;

namespace SSHexMap.Terrain.Generation
{
    internal class RuntimeState
    {
        public Vector2Int CurrentCameraPosition = Vector2Int.zero;
        public readonly Plane[] CameraPlanes = new Plane[6];
        public float ChunkBoundSize;
        public bool WorldMonitoringActive = true;
    }
}

