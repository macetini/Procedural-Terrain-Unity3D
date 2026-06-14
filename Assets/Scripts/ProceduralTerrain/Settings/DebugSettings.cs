namespace ProceduralTerrain.Settings
{
    [System.Serializable]
    public class DebugSettings
    {
        public bool cleanupLogs = false;
        public bool showLodRanges = false;
        public bool showChunkLodBounds = false;
        public bool showNormals = false;
        public int orphanSweepPeriod = 3; // Run orphan sweep every N cleanup cycles (0 = always, -1 = never)

        public void ClampValues()
        {
            // -1 = never, 0 = always, >0 = every N cycles. All valid; clamp only extreme low end.
            if (orphanSweepPeriod < -1)
            {
                orphanSweepPeriod = -1;
            }
        }
    }
}
