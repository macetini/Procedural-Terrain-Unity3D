using UnityEngine;

namespace ProceduralTerrain.Settings
{
    [System.Serializable]
    public class CameraSettings
    {
        public Camera reference;
        public float frustumPadding = 5.0f;
        public int viewDistanceChunks = 3;

        public void ClampValues()
        {
            frustumPadding = Mathf.Max(0f, frustumPadding);
            viewDistanceChunks = Mathf.Max(0, viewDistanceChunks);
        }
    }
}
