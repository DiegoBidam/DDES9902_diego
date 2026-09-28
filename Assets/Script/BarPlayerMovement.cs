using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class BarPlayerMovement : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float mouseSensitivity = 0.12f;

    private CharacterController controller;
    private Transform view;
    private float pitch;
    private float verticalSpeed;
    private Vector3 spawnPosition;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        Camera camera = GetComponentInChildren<Camera>();
        if (camera == null)
        {
            Debug.LogError("Player 아래에 Main Camera가 필요합니다.");
            enabled = false;
            return;
        }

        view = camera.transform;
        spawnPosition = transform.position;
    }

    void Update()
    {
        Vector2 movement = Vector2.zero;
        Vector2 look = Vector2.zero;
        bool clicked = false;
        bool escape = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null)
        {
            movement.x = (keyboard.dKey.isPressed ? 1 : 0)
                       - (keyboard.aKey.isPressed ? 1 : 0);
            movement.y = (keyboard.wKey.isPressed ? 1 : 0)
                       - (keyboard.sKey.isPressed ? 1 : 0);
            escape = keyboard.escapeKey.wasPressedThisFrame;
        }

        if (mouse != null)
        {
            look = mouse.delta.ReadValue() * mouseSensitivity;
            clicked = mouse.leftButton.wasPressedThisFrame;
        }
#else
        movement = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        look = new Vector2(
            Input.GetAxis("Mouse X"),
            Input.GetAxis("Mouse Y")
        ) * mouseSensitivity * 15f;

        clicked = Input.GetMouseButtonDown(0);
        escape = Input.GetKeyDown(KeyCode.Escape);
#endif

        if (escape)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (clicked && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            look = Vector2.zero;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
            view.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        else
        {
            movement = Vector2.zero;
        }

        movement = Vector2.ClampMagnitude(movement, 1f);

        if (controller.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;

        verticalSpeed += Physics.gravity.y * Time.deltaTime;

        Vector3 velocity =
            (transform.right * movement.x +
             transform.forward * movement.y) * moveSpeed;

        velocity.y = verticalSpeed;
        controller.Move(velocity * Time.deltaTime);

        if (transform.position.y < -10f)
        {
            controller.enabled = false;
            transform.position = spawnPosition;
            controller.enabled = true;
            verticalSpeed = 0f;
        }
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}