using System.Collections.Generic;
using UnityEngine;

public class TerrainDataMap
{
    private readonly Dictionary<Vector2Int, TileMeshStruct[,]> _map = new();
    private readonly HashSet<Vector2Int> _sanitizedSet = new();

    private readonly int chunkSize;
    private readonly float noiseScale;
    private readonly int maxElevation;

    public TerrainDataMap(int chunkSize, float noiseScale, int maxElevation)
    {
        this.chunkSize = chunkSize;
        this.noiseScale = noiseScale;
        this.maxElevation = maxElevation;
    }
}
