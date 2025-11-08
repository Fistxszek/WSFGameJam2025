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
    [SerializeField] private float _slowWalkSpeed;
    [SerializeField] private float _sprintLength;

    [SerializeField] private Transform Gate;

    [SerializeField] private Animator _animator;

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
        
        input.Movement.Left.performed += OnRotateLeft;
        input.Movement.Left.canceled += OnRotateLeft;
    
        input.Movement.Right.performed += OnRotateRight;
        input.Movement.Right.canceled += OnRotateRight;
        
        input.Movement.StartStop.performed += OnToggleMovement;
        
        input.Movement.Sprint.started += OnSprintEnabled;
        input.Movement.Sprint.canceled += OnSprintEnabled;

        input.Movement.SlowWalk.started += OnSlowWalkEnabled;
        input.Movement.SlowWalk.canceled += OnSlowWalkEnabled;
    }
    private void OnDisable()
    {
        var input = GameManager.Instance?.controls;
        if (input == null)
            return;
        
        input.Movement.Left.performed -= OnRotateLeft;
        input.Movement.Right.performed -= OnRotateRight;
        input.Movement.Left.canceled -= OnRotateLeft;
        input.Movement.Right.canceled -= OnRotateRight;
        
        input.Movement.StartStop.performed -= OnToggleMovement;
        
        input.Movement.Sprint.started -= OnSprintEnabled;
        input.Movement.Sprint.canceled -= OnSprintEnabled;
        
        input.Movement.SlowWalk.started -= OnSlowWalkEnabled;
        input.Movement.SlowWalk.canceled -= OnSlowWalkEnabled;
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
            _animator.SetBool("isRunning", true);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            _animator.SetBool("isRunning", false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("jezioro"))
        {
            _animator.SetBool("isSwimming", true);
            _currentSpeedMulti = 0.5f;
            Debug.Log("p³ywanie");
        }

    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("jezioro"))
        {
            _animator.SetBool("isSwimming", false);
            _currentSpeedMulti = 1;
            Debug.Log("NIEp³ywanie");
        }

    }

    private bool _leftActive;
    private bool _rightActive;
    // Called from Input Action for rotating left
    public void OnRotateLeft(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            rotationDirection = 1f; // Positive rotation (counterclockwise)
            ShowRightMessageAndExpression.Instance.ChangeMessageSprite(MsgType.Left);
            _leftActive = true;
        }
        else if (context.canceled)
        {
            if (!_rightActive)
                rotationDirection = 0f;
            _leftActive = false;
        }
    }

    // Called from Input Action for rotating right
    public void OnRotateRight(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            rotationDirection = -1f; // Negative rotation (clockwise)
            ShowRightMessageAndExpression.Instance.ChangeMessageSprite(MsgType.Right);
            _rightActive = true;
        }
        else if (context.canceled)
        {
            if (!_leftActive)
                rotationDirection = 0f;
            _rightActive = false;
        }
    }

    // Called from Input Action for toggling movement
    public void OnToggleMovement(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            isMoving = !isMoving;
            rotationDirection = 0;
            
            if (!isMoving)
                ShowRightMessageAndExpression.Instance.ChangeMessageSprite(MsgType.Stay);
            else
                ShowRightMessageAndExpression.Instance.ChangeMessageSprite(MsgType.Go);
        }
    }
    
    void FaceTarget()
    {
        // Calculate direction to target
        Vector2 direction = Gate.position - transform.position;
    
        // Calculate angle in degrees
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    
        // Apply rotation (subtract 90 if your sprite faces up by default)
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private float baseRotSpeed;

    public void OnSprintEnabled(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            _currentSpeedMulti = 2; 
        }
        else if (context.canceled)
        {
            _currentSpeedMulti = 1;
        }
        // if (context.started)
        // {
        //     baseRotSpeed = rotationSpeed;
        //     rotationSpeed *= 4;
        //     ShowRightMessageAndExpression.Instance.ChangeMessageSprite(MsgType.Push);
        // }
        // else if (context.canceled)
        // {
        //     rotationSpeed = baseRotSpeed;
        // }
    }
    public void OnSlowWalkEnabled(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            _currentSpeedMulti = _slowWalkSpeed; 
        }
        else if (context.canceled)
        {
            _currentSpeedMulti = 1;
        }
    }

    private IEnumerator CommandDelay()
    {
        var baseRotSpeed = rotationSpeed;
        rotationSpeed *= 4;
        // _currentSpeedMulti = _speedMulti;
        yield return new WaitForSeconds(_sprintLength);
        // _currentSpeedMulti = 1;
        rotationSpeed = baseRotSpeed;
    }
}
