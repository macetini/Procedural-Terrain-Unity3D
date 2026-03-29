using UnityEngine;

namespace Assets.Scripts.Terrain.Config
{
    [System.Serializable]
    public class CameraSettings
    {
        public Camera reference;
        public float frustumPadding = 5.0f;
        public int viewDistanceChunks = 3;
    }
}
