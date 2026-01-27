using System;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
public class ObserverController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created\
    Rigidbody rigidBody;

    [SerializeField] ObserverInputSystem observerControls;
    Vector2 moveDirection = Vector2.zero;
    Vector2 lookDirection = Vector2.zero;
    Vector2 HorizontalMoveDirection = Vector2.zero;
    InputAction move;
    InputAction look;
    InputAction upDown;

    [SerializeField]
    float moveSpeed = 10.0f;

    [SerializeField]
    float lookSensitivity = 30.0f;

    float xRotation = 0.0f;
    float yRotation = 0.0f;

    [SerializeField]
    Transform orientation;

    Camera observerCamera;

    private bool isPaused = false;

    private void Awake()
    {
        observerControls = new ObserverInputSystem();
    }
    void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        GameObject cameraHolder = GameObject.Find("CameraHolder");
        if (cameraHolder != null)
        {
            observerCamera = cameraHolder.GetComponentInChildren<Camera>();
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        // Check for ESC key to pause/unpause using new Input System
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }

        // Only read input if not paused
        if (!isPaused)
        {
            moveDirection = move.ReadValue<Vector2>();
            lookDirection = look.ReadValue<Vector2>();
            HorizontalMoveDirection = upDown.ReadValue<Vector2>();
        }
        else
        {
            // Clear movement when paused
            moveDirection = Vector2.zero;
            lookDirection = Vector2.zero;
            HorizontalMoveDirection = Vector2.zero;
        }
    }

    private void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void FixedUpdate()
    {
        // Only move and turn if not paused
        if (!isPaused)
        {
            Turn();
            Move();
        }
    }

    void OnEnable()
    {
        EnableInputs();
    }

    private void OnDisable()
    {
        DisableInputs();
    }

    private void EnableInputs()
    {
        observerControls.Enable();
        move = observerControls.Player.Move;
        move.Enable();
        look = observerControls.Player.Look;
        look.Enable();
        upDown = observerControls.Player.UpDown;
        upDown.Enable();
    }

    private void DisableInputs()
    {
        observerControls.Disable();
        move.Disable();
        look.Disable();
        upDown.Disable();
    }
    
    private void Turn()
    {
        float mouseX = lookDirection.x * Time.deltaTime * lookSensitivity;
        float mouseY = lookDirection.y * Time.deltaTime * lookSensitivity;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Math.Clamp(xRotation, -90.0f, 90.0f);

        observerCamera.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0.0f);
        orientation.rotation = Quaternion.Euler(0.0f, yRotation, 0.0f);
    }

    private void Move()
    {
        transform.Translate(orientation.forward * moveSpeed * Time.deltaTime * moveDirection.y, Space.World);
        transform.Translate(orientation.right * moveSpeed * Time.deltaTime * moveDirection.x, Space.World);
        transform.Translate(orientation.up * moveSpeed * Time.deltaTime * HorizontalMoveDirection.y, Space.World);
    }
}
