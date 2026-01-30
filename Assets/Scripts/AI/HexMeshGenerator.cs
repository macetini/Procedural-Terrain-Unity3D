using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexMeshGenerator : MonoBehaviour
{
    public float Size = 1.0f;

    [ContextMenu("Generate Hex Mesh")]
    public void GenerateHexMesh() => GenerateHexMesh(Size);

    public void GenerateHexMesh(float size)
    {
        Mesh mesh = new() { name = "Hexagon" };

        // 1. Define the 7 vertices (Center + 6 Corners)
        Vector3[] vertices = new Vector3[7];
        vertices[0] = Vector3.zero; // Center

        for (int i = 0; i < 6; i++)
        {
            // Pointy top: angle starts at 30 degrees (PI/6)
            float angleDeg = 60 * i + 30;
            float angleRad = Mathf.PI / 180 * angleDeg;

            vertices[i + 1] = new Vector3(
                size * Mathf.Cos(angleRad),
                0,
                size * Mathf.Sin(angleRad)
            );
        }

        // 2. Define the 6 Triangles
        int[] triangles = new int[18];
        for (int i = 0; i < 6; i++)
        {
            triangles[i * 3] = 0; // Center
            triangles[i * 3 + 1] = i + 1; // Current Corner
            triangles[i * 3 + 2] = (i == 5) ? 1 : i + 2; // Next Corner
        }

        // 3. Finalize Mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals(); // Crucial for lighting!

        GetComponent<MeshFilter>().mesh = mesh;
    }
}
