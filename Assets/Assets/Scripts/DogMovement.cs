using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class DogMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 90f; // Degrees per second
    
    private Rigidbody2D rb;
    private bool isMoving = false;
    private float rotationDirection = 0f; // -1 for left, 1 for right, 0 for no rotation
    [Header("Sprint settings")]
    private float _currentSpeedMulti = 1;
    [SerializeField] private float _speedMulti;
    [SerializeField] private float _sprintLength;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        StartCoroutine(EnableInputEvents());
    }

    private IEnumerator EnableInputEvents()
    {
        while (!GameManager.Instance)
            yield return null;
        var input = GameManager.Instance?.controls;
        if (input == null)
            yield break;
        
        input.Movement.Left.started += OnRotateLeft;
        input.Movement.Left.canceled += OnRotateLeft;
    
        input.Movement.Right.started += OnRotateRight;
        input.Movement.Right.canceled += OnRotateRight;
        
        input.Movement.StartStop.performed += OnToggleMovement;
        input.Movement.Sprint.performed += OnSprintEnabled;
    }
    private void OnDisable()
    {
        var input = GameManager.Instance?.controls;
        if (input == null)
            return;
        
        input.Movement.Left.started -= OnRotateLeft;
        input.Movement.Right.started -= OnRotateRight;
        input.Movement.Left.canceled -= OnRotateLeft;
        input.Movement.Right.canceled -= OnRotateRight;
        
        input.Movement.StartStop.performed -= OnToggleMovement;
        input.Movement.Sprint.performed -= OnSprintEnabled;
    }

    private void FixedUpdate()
    {
        // Apply rotation if button is held
        if (rotationDirection != 0f)
        {
            float rotation = rotationDirection * rotationSpeed * Time.fixedDeltaTime;
            transform.Rotate(0f, 0f, rotation);
        }
        
        // Move forward in current direction if moving is enabled
        if (isMoving)
        {
            Vector2 forwardDirection = transform.up; // In 2D, transform.up is the forward direction
            var speed = moveSpeed * _currentSpeedMulti;
            rb.linearVelocity = forwardDirection * speed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    // Called from Input Action for rotating left
    public void OnRotateLeft(InputAction.CallbackContext context)
    {
        if (!isMoving )
            return;
        
        if (context.started)
        {
            rotationDirection = 1f; // Positive rotation (counterclockwise)
        }
        else if (context.canceled)
        {
            rotationDirection = 0f;
        }
    }

    // Called from Input Action for rotating right
    public void OnRotateRight(InputAction.CallbackContext context)
    {
        if (!isMoving )
            return;
        
        if (context.started)
        {
            rotationDirection = -1f; // Negative rotation (clockwise)
        }
        else if (context.canceled)
        {
            rotationDirection = 0f;
        }
    }

    // Called from Input Action for toggling movement
    public void OnToggleMovement(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            isMoving = !isMoving;
        }
    }

    public void OnSprintEnabled(InputAction.CallbackContext context)
    {
        if (context.performed)
            StartCoroutine(WaitForSprintReset());
    }

    private IEnumerator WaitForSprintReset()
    {
        _currentSpeedMulti = _speedMulti;
        yield return new WaitForSeconds(_sprintLength);
        _currentSpeedMulti = 1;
    }
}
