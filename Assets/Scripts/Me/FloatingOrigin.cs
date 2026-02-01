using UnityEngine;

public class FloatingOrigin : MonoBehaviour
{
    [Tooltip("How far can we wander before we snap back? 5000 is safe.")]
    public float threshold = 5000f;

    void LateUpdate()
    {
        Vector3 cameraPos = transform.position;
        if (Mathf.Abs(cameraPos.x) > threshold || Mathf.Abs(cameraPos.z) > threshold)
        {
            ShiftWorld(cameraPos);
        }
    }

    private void ShiftWorld(Vector3 offset)
    {
        Vector3 shift = new(offset.x, 0, offset.z);

        GameObject[] rootObjects = UnityEngine
            .SceneManagement.SceneManager.GetActiveScene()
            .GetRootGameObjects();
        foreach (GameObject g in rootObjects)
        {
            g.transform.position -= shift;
        }

        Debug.Log(
            $"<color=cyan>[FloatingOrigin] World shifted by {shift}. Precision Restored.</color>"
        );
    }
}
