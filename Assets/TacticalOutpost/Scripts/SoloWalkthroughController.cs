using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SoloWalkthroughController : MonoBehaviour
{
    public float walkSpeed = 4.5f, sprintSpeed = 7f, crouchSpeed = 2.2f, jumpHeight = .6f, gravity = -20f, mouseSensitivity = .12f;
    CharacterController controller;
    Transform view;
    Vector3 start;
    float verticalSpeed, pitch;
    const float StandingHeight = 1.8f;

    void Awake() { controller = GetComponent<CharacterController>(); view = GetComponentInChildren<Camera>().transform; start = transform.position; }
    void Start() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (Mouse.current.leftButton.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        if (Keyboard.current.rKey.wasPressedThisFrame) { controller.enabled = false; transform.position = start; verticalSpeed = 0; controller.enabled = true; }
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            pitch = Mathf.Clamp(pitch - Mouse.current.delta.y.ReadValue() * mouseSensitivity, -85, 85);
            transform.Rotate(0, Mouse.current.delta.x.ReadValue() * mouseSensitivity, 0);
            view.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
        bool crouch = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
        float targetHeight = crouch ? 1.15f : StandingHeight;
        if (targetHeight > controller.height)
        {
            Vector3 bottom = transform.position + Vector3.up * controller.radius;
            if (Physics.CheckCapsule(bottom, bottom + Vector3.up * (targetHeight - controller.radius * 2), controller.radius, ~0, QueryTriggerInteraction.Ignore)) targetHeight = controller.height;
        }
        float oldHeight = controller.height;
        controller.height = Mathf.MoveTowards(oldHeight, targetHeight, Time.deltaTime * 8);
        controller.center = new Vector3(0, controller.height * .5f, 0);
        transform.position += Vector3.up * ((controller.height - oldHeight) * .5f);
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y++;
        if (Keyboard.current.sKey.isPressed) input.y--;
        if (Keyboard.current.dKey.isPressed) input.x++;
        if (Keyboard.current.aKey.isPressed) input.x--;
        input = Vector2.ClampMagnitude(input, 1);
        if (controller.isGrounded) { if (verticalSpeed < 0) verticalSpeed = -2; if (Keyboard.current.spaceKey.wasPressedThisFrame && !crouch) verticalSpeed = Mathf.Sqrt(jumpHeight * -2 * gravity); }
        verticalSpeed += gravity * Time.deltaTime;
        float speed = crouch ? crouchSpeed : (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed ? sprintSpeed : walkSpeed);
        controller.Move(((transform.right * input.x + transform.forward * input.y) * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
    }
    void OnDisable() { if (Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }
}
