using System.Collections.Generic;
using UnityEngine;

public class HexGridGenerator : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridRadius = 10;
    public float hexSize = 1.0f;
    public float terraceHeight = 0.5f;

    [Header("Noise Settings")]
    public float noiseScale = 0.1f;
    public int elevationSteps = 5;

    [Header("References")]
    public GameObject hexPrefab;

    private Dictionary<Vector2Int, HexCell> grid = new Dictionary<Vector2Int, HexCell>();

    void Start()
    {
        GenerateGrid();
    }

    void GenerateGrid()
    {
        // Axial Coordinate loop for a hexagonal shape
        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            int r1 = Mathf.Max(-gridRadius, -q - gridRadius);
            int r2 = Mathf.Min(gridRadius, -q + gridRadius);

            for (int r = r1; r <= r2; r++)
            {
                CreateCell(q, r);
            }
        }
    }

    void CreateCell(int q, int r)
    {
        // 1. Calculate Elevation using Noise
        // We multiply by gridRadius to offset so we don't always sample at (0,0)
        float noiseVal = Mathf.PerlinNoise((q + 100) * noiseScale, (r + 100) * noiseScale);

        // 2. Quantize/Step the height for the "Digital" look
        int elevation = Mathf.FloorToInt(noiseVal * elevationSteps);

        // 3. Create the Data
        HexCell cellData = new(q, r, elevation);
        grid.Add(new Vector2Int(q, r), cellData);

        // 4. Spawn the Visuals
        GameObject go = Instantiate(hexPrefab, transform);
        Vector3 worldPos = HexMath.HexToWorld(q, r, elevation, terraceHeight);
        go.transform.localPosition = worldPos;
        go.name = $"Hex_{q}_{r}";

        // 5. Initialize the Mesh (If using the Custom Mesh Generator)
        var meshGen = go.GetComponent<HexMeshGenerator>();
        if (meshGen != null)
        {
            meshGen.GenerateHexMesh(hexSize);
        }
    }
}
