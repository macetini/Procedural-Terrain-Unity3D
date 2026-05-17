namespace Assets.Scripts.ProceduralTerrain.Settings
{
    [System.Serializable]
    public class DebugSettings
    {
        public bool cleanupLogs = false;
        public int orphanSweepPeriod = 3; // Run orphan sweep every N cleanup cycles (0 = always, -1 = never)
    }
}
