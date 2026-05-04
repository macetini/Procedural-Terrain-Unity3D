using UnityEngine;

namespace SSHexMap.Terrain.Settings
{
    [System.Serializable]
    public class CameraSettings
    {
        public Camera reference;
        public float frustumPadding = 5.0f;
        public int viewDistanceChunks = 3;
    }
}

