namespace ProceduralTerrain.Settings
{
    [System.Serializable]
    public class DebugSettings
    {
        public bool cleanupLogs;
        public bool showViewDistance;
        public bool showLodRanges;
        public bool showChunkLodBounds;
        public bool showNormals;
        public bool showColliderBounds;

        public void ClampValues()
        {
        }
    }
}
