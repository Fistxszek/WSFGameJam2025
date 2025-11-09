using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity; // Add FMOD namespace

public class DogMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 90f;
    
    [Header("Sprint settings")]
    private float _currentSpeedMulti = 1;
    [SerializeField] private float _slowWalkSpeed;
    [SerializeField] private float _sprintLength;
    
    [Tooltip("Optional: Parameter name to control speed (e.g., 'Speed' or 'Intensity')")]
    [SerializeField] private string speedParameterName = "Speed";
    [SerializeField] private bool useSpeedParameter = false;
    
    [SerializeField] private Transform Gate;
    [SerializeField] private Animator _animator;
    
    private Rigidbody2D rb;
    private bool isMoving = false;
    private float rotationDirection = 0f;
    private bool _leftActive;
    private bool _rightActive;
    
    // FMOD instance
    private FMOD.Studio.EventInstance runningAudioInstance;
    private bool audioIsPlaying = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Create FMOD event instance
        if (!FMODEvents.Instance.DogRun.IsNull)
        {
            runningAudioInstance = RuntimeManager.CreateInstance(FMODEvents.Instance.DogRun);
            RuntimeManager.AttachInstanceToGameObject(runningAudioInstance, transform);
        }
    }

    private void OnEnable()
    {
        StartCoroutine(EnableInputEvents());
    }

    private void PlayMumbleSfx(InputAction.CallbackContext context)
    {
        AudioManager.Instance.PlayOneShot(FMODEvents.Instance.DziadMumble);
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
        
        
        input.Movement.StartStop.started += PlayMumbleSfx;
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
        
        
        input.Movement.StartStop.started -= PlayMumbleSfx;
    }

    private void OnDestroy()
    {
        // Clean up FMOD instance
        if (runningAudioInstance.isValid())
        {
            runningAudioInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            runningAudioInstance.release();
        }
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
            Vector2 forwardDirection = transform.up;
            var speed = moveSpeed * _currentSpeedMulti;
            rb.linearVelocity = forwardDirection * speed;
            _animator.SetBool("isRunning", true);
            
            // Start audio if not already playing
            if (!audioIsPlaying && runningAudioInstance.isValid())
            {
                runningAudioInstance.start();
                audioIsPlaying = true;
            }
            
            // Update speed parameter if enabled
            if (useSpeedParameter && audioIsPlaying && !string.IsNullOrEmpty(speedParameterName))
            {
                runningAudioInstance.setParameterByName(speedParameterName, _currentSpeedMulti);
            }
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            _animator.SetBool("isRunning", false);
            
            // Stop audio if playing
            if (audioIsPlaying && runningAudioInstance.isValid())
            {
                runningAudioInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                audioIsPlaying = false;
            }
        }
    }

    // Rest of your existing methods...
    public void OnRotateLeft(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            rotationDirection = 1f;
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

    public void OnRotateRight(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            rotationDirection = -1f;
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
        Vector2 direction = Gate.position - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    public void OnSprintEnabled(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            _currentSpeedMulti = 2; 
            ShowRightMessageAndExpression.Instance.ChangeMessageSprite(MsgType.Push);
        }
        else if (context.canceled)
        {
            _currentSpeedMulti = 1;
        }
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
        yield return new WaitForSeconds(_sprintLength);
        rotationSpeed = baseRotSpeed;
    }
}
