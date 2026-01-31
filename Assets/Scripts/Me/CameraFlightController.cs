using UnityEngine;
using UnityEngine.InputSystem; // Required for the new system

public class CameraFlightController : MonoBehaviour
{
    [Header("Movement Speeds")]
    public float normalSpeed = 20f;
    public float fastSpeed = 100f;
    public float warpSpeed = 500f;

    [Header("Look Sensitivity")]
    public float sensitivity = 0.1f; // New system uses pixel deltas, so lower is better

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Vector3 rot = transform.localRotation.eulerAngles;
        rotationX = rot.y;
        rotationY = -rot.x;
    }

    void Update()
    {
        // --- 1. ROTATION (Mouse Delta) ---
        /*if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            rotationX += mouseDelta.x * sensitivity;
            rotationY += mouseDelta.y * sensitivity;
            rotationY = Mathf.Clamp(rotationY, -90f, 90f);
            transform.localRotation = Quaternion.Euler(-rotationY, rotationX, 0);
        }*/

        // --- 2. SPEED MODIFIERS ---
        float currentSpeed = normalSpeed;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftShiftKey.isPressed)
                currentSpeed = fastSpeed;
            if (Keyboard.current.leftCtrlKey.isPressed)
                currentSpeed = warpSpeed;

            // --- 3. MOVEMENT (WASD + EQ) ---
            Vector3 moveDir = Vector3.zero;

            if (Keyboard.current.wKey.isPressed)
                moveDir += transform.forward;
            if (Keyboard.current.sKey.isPressed)
                moveDir -= transform.forward;
            if (Keyboard.current.aKey.isPressed)
                moveDir -= transform.right;
            if (Keyboard.current.dKey.isPressed)
                moveDir += transform.right;
            if (Keyboard.current.eKey.isPressed)
                moveDir += Vector3.up;
            if (Keyboard.current.qKey.isPressed)
                moveDir -= Vector3.up;
            if (Keyboard.current.leftArrowKey.isPressed)
                moveDir = Vector3.left;
            if (Keyboard.current.rightArrowKey.isPressed)
                moveDir = Vector3.right;
            if (Keyboard.current.upArrowKey.isPressed)
                moveDir = Vector3.up;
            if (Keyboard.current.downArrowKey.isPressed)
                moveDir = Vector3.down;

            // Stop all movement when Space is pressed

            transform.position += moveDir.normalized * currentSpeed * Time.deltaTime;

            // Unlock cursor
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState =
                    (Cursor.lockState == CursorLockMode.Locked)
                        ? CursorLockMode.None
                        : CursorLockMode.Locked;
            }
        }
    }
}
