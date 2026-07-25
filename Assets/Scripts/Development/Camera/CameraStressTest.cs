using UnityEngine;

namespace ProceduralTerrain.Development.Camera
{
    /// <summary>
    /// Autonomous camera movement for long-running terrain stress tests.
    /// Attach to the camera GameObject, press Play and leave it running.
    /// The camera drifts across X/Z using a Lissajous-style path and
    /// slowly rotates around Y so the frustum sweeps in all directions.
    /// </summary>
    public class CameraStressTest : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Horizontal speed on X axis (world units / second).")]
        public float speedX = 200f;

        [Tooltip("Horizontal speed on Z axis (world units / second).")]
        public float speedZ = 130f;

        [Tooltip("Maximum distance the camera will travel from its start position on each axis.")]
        public float travelRadius = 100000f;

        [Header("Rotation")]
        [Tooltip("Y-axis rotation speed in degrees / second.")]
        public float rotationSpeedY = 15f;

        [Header("Control")]
        [Tooltip("Run automatically when the scene starts.")]
        public bool runOnStart = true;

        private Vector3 startPosition;
        private float elapsedTime;
        private bool running;

        private void Start()
        {
            startPosition = transform.position;

            if (runOnStart)
            {
                StartTest();
            }
        }

        private void Update()
        {
            if (!running)
            {
                return;
            }

            elapsedTime += Time.deltaTime;

            // Lissajous path keeps the camera covering a wide area
            // without repeating on a simple straight line.
            var offsetX = Mathf.Sin(elapsedTime * speedX / travelRadius) * travelRadius;
            var offsetZ = Mathf.Sin(elapsedTime * speedZ / travelRadius) * travelRadius;

            transform.position = new Vector3(
                startPosition.x + offsetX,
                startPosition.y,
                startPosition.z + offsetZ
            );

            transform.Rotate(Vector3.up, rotationSpeedY * Time.deltaTime, Space.World);
        }

        [ContextMenu("Start Test")]
        public void StartTest()
        {
            startPosition = transform.position;
            elapsedTime = 0f;
            running = true;
        }

        [ContextMenu("Stop Test")]
        public void StopTest()
        {
            running = false;
        }

        [ContextMenu("Reset Position")]
        public void ResetPosition()
        {
            running = false;
            transform.position = startPosition;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
            var center = Application.isPlaying ? startPosition : transform.position;
            Gizmos.DrawWireCube(
                new Vector3(center.x, center.y, center.z),
                new Vector3(travelRadius * 2f, 1f, travelRadius * 2f)
            );
        }
#endif
    }
}
