[System.Serializable]
public class DebugSettings
{
    public bool cleanupLogs = false;
    public bool cleanupLogOnlyOnRemoval = true;
    public int orphanSweepPeriod = 3; // Run orphan sweep every N cleanup cycles (0 = always, -1 = never)
}
