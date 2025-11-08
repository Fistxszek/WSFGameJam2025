using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class FlockingSheep : MonoBehaviour
{
    [Header("Tags & References")]
    public string threatTag = "Player";
    public string allyTag = "Sheep";
    public Transform gateTarget;

    [Header("Collider Setup")]
    [Tooltip("Trigger collider (Is Trigger = true) - for fear spreading detection")]
    public Collider2D triggerCollider;

    [Header("Flee Behavior - Speed")]
    public float minFleeSpeed = 3f;
    public float maxFleeSpeed = 8f;
    public float panicSpeedDistance = 3f;
    public float calmSpeedDistance = 12f;
    public bool smoothStop = true;

    [Header("Flee Duration Control")]
    public float keepRunningDistance = 8f;
    public float safeDistance = 15f;
    public float stopDelay = 2f;

    [Header("Flocking")]
    public float flockRadius = 3f;
    public float separationWeight = 1.5f;
    public float alignmentWeight = 1f;
    public float cohesionWeight = 1f;

    [Header("Dog Control (Close = More Control)")]
    public float minRandomness = 2f;
    public float maxRandomness = 40f;
    public float minDogDistance = 2f;
    public float maxDogDistance = 10f;
    public float closeSteeringSpeed = 180f;
    public float farSteeringSpeed = 45f;

    [Header("Gate Magnetism")]
    public float gateAttractionStartDistance = 15f;
    public float gateAttractionForceDistance = 5f;
    public float gateCaptureRadius = 2f;
    [Range(0f, 1f)]
    public float maxGateInfluence = 0.9f;
    
    [Header("Group Following")]
    public float groupFollowCheckRadius = 5f;
    [Range(0f, 1f)]
    public float groupFollowMagnetismBoost = 0.3f;

    [Header("Leader System")]
    public float leaderFollowRadius = 8f;
    public float leaderStickiness = 2f;
    public float disperseDistance = 12f;
    [Range(0f, 1f)]
    public float leaderFollowWeight = 0.7f;

    [Header("Non-Alerted Movement")]
    public float chillMoveSpeed = 2f;
    public float chillUpdateInterval = 0.5f;

    [Header("Alert")]
    public float alertRange = 3f;
    public float alertCooldown = 1f;
    
    [Header("Visual Rotation")]
    public float rotationSpeed = 360f;
    [Tooltip("Offset if your sprite doesn't face up by default (e.g., 90 if facing right)")]
    public float spriteDirectionOffset = 0f;
    
    [Header("Animation")]
    public Animator animator;
    public string speedParameterName = "Speed";
    public string isMovingParameterName = "IsMoving";
    [Tooltip("Speed at which animation plays at 1x speed")]
    public float normalAnimationSpeed = 5f;
    
    [Header("Debug")]
    public bool debugLogs = false;
    public bool showSpeedDebug = false;

    public bool Alerted { get; private set; } = false;
    public bool CapturedByGate { get; private set; } = false;
    
    private Rigidbody2D rb;
    private Coroutine movementRoutine;
    private bool canAlert = true;
    private Transform dogTransform;
    private Vector2 currentDirection;
    private float currentSpeed;
    private List<FlockingSheep> nearbyFlock = new List<FlockingSheep>();
    
    private static List<FlockingSheep> allSheep = new List<FlockingSheep>();
    private static FlockingSheep currentLeader = null;
    private bool isLeader = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Auto-assign Animator if not set
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }
        
        if (triggerCollider == null)
        {
            Collider2D[] colliders = GetComponents<Collider2D>();
            foreach (var col in colliders)
            {
                if (col.isTrigger && triggerCollider == null)
                    triggerCollider = col;
            }
        }
        
        if (triggerCollider == null)
        {
            Debug.LogError($"{name}: Need both a physics collider (non-trigger) and a trigger collider! " +
                          "Add two CircleCollider2D components: one with 'Is Trigger' checked, one without.", this);
        }
        
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: triggerCollider should have 'Is Trigger' enabled!", this);
        }
        
        allSheep.Add(this);
    }

    void OnDestroy()
    {
        allSheep.Remove(this);
        
        if (isLeader && currentLeader == this)
        {
            currentLeader = null;
        }
    }

    void Start()
    {
        StartChillMovement();
    }

    void FixedUpdate()
    {
        if (!CapturedByGate && gateTarget != null)
        {
            float distanceToGate = Vector2.Distance(transform.position, gateTarget.position);
            if (distanceToGate <= gateCaptureRadius)
            {
                CaptureAtGate();
            }
        }
    }

    private void CaptureAtGate()
    {
        CapturedByGate = true;
        Alerted = false;
        
        if (isLeader)
        {
            isLeader = false;
            if (currentLeader == this)
                currentLeader = null;
        }
        
        if (movementRoutine != null)
            StopCoroutine(movementRoutine);
        
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        
        currentSpeed = 0f;
        UpdateAnimator();
        
        if (debugLogs)
            Debug.Log($"{name} captured at gate!", this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (CapturedByGate) return;

        if (other.CompareTag(threatTag))
        {
            dogTransform = other.transform;
            AlertAndRun();
            
            if (debugLogs)
                Debug.Log($"{name} - Detected dog trigger", this);
        }

        if (other.CompareTag(allyTag))
        {
            FlockingSheep ally = other.GetComponent<FlockingSheep>();
            if (ally != null && ally.Alerted && !Alerted)
            {
                if (debugLogs)
                    Debug.Log($"{name} caught fear from {ally.name} via trigger!", this);

                dogTransform = ally.dogTransform;
                AlertAndRun();
            }
        }
    }

    private void StartChillMovement()
    {
        if (CapturedByGate) return;
        
        if (movementRoutine != null)
            StopCoroutine(movementRoutine);
        
        movementRoutine = StartCoroutine(ChillMovementRoutine());
    }

    private IEnumerator ChillMovementRoutine()
    {
        while (!Alerted && !CapturedByGate)
        {
            if (dogTransform != null)
            {
                Vector2 awayFromDog = ((Vector2)transform.position - (Vector2)dogTransform.position).normalized;
                
                if (gateTarget != null)
                {
                    Vector2 towardsGate = ((Vector2)gateTarget.position - (Vector2)transform.position).normalized;
                    currentDirection = (awayFromDog * 0.7f + towardsGate * 0.3f).normalized;
                }
                else
                {
                    currentDirection = awayFromDog;
                }

                currentSpeed = chillMoveSpeed;
                
                if (rb != null)
                    rb.linearVelocity = currentDirection * currentSpeed;
                
                UpdateVisualRotation();
                UpdateAnimator();
            }
            else
            {
                currentSpeed = 0f;
                
                if (rb != null)
                    rb.linearVelocity = Vector2.zero;
                
                UpdateAnimator();
            }

            yield return new WaitForSeconds(chillUpdateInterval);
        }
    }

    private void AlertAndRun()
    {
        if (Alerted || CapturedByGate) return;

        Alerted = true;
        
        if (dogTransform != null)
        {
            Vector2 initialFleeDirection = ((Vector2)transform.position - (Vector2)dogTransform.position).normalized;
            currentDirection = initialFleeDirection;
        }

        if (movementRoutine != null)
            StopCoroutine(movementRoutine);
        
        movementRoutine = StartCoroutine(AlertedFleeRoutine());

        if (canAlert)
            StartCoroutine(AlertNearbyAllies());
    }

    private void UpdateLeaderStatus()
    {
        if (dogTransform == null) return;
        
        // If no leader exists, first alerted sheep becomes leader
        if (currentLeader == null && Alerted && !CapturedByGate)
        {
            SetAsLeader();
            return;
        }
        
        // If current leader is captured, reassign
        if (currentLeader != null && currentLeader.CapturedByGate)
        {
            currentLeader = null;
            
            // Find new leader: closest alerted sheep to dog
            FlockingSheep closestSheep = null;
            float closestDist = float.MaxValue;
            
            foreach (var sheep in allSheep)
            {
                if (sheep.Alerted && !sheep.CapturedByGate)
                {
                    float dist = Vector2.Distance(sheep.transform.position, dogTransform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestSheep = sheep;
                    }
                }
            }
            
            if (closestSheep != null)
                closestSheep.SetAsLeader();
        }
    }

    private void SetAsLeader()
    {
        if (currentLeader != null)
            currentLeader.isLeader = false;
        
        currentLeader = this;
        isLeader = true;
        
        if (debugLogs)
            Debug.Log($"{name} is now the LEADER", this);
    }

    private IEnumerator AlertedFleeRoutine()
    {
        float safeTimer = 0f;
        UpdateLeaderStatus();
        
        while (!CapturedByGate)
        {
            float distanceToDog = dogTransform != null ? 
                Vector2.Distance(transform.position, dogTransform.position) : float.MaxValue;
            
            UpdateLeaderStatus();
            
            if (distanceToDog < keepRunningDistance)
            {
                safeTimer = 0f;
            }
            else if (distanceToDog >= safeDistance)
            {
                safeTimer += Time.deltaTime;
                
                if (safeTimer >= stopDelay)
                {
                    if (debugLogs)
                        Debug.Log($"{name} - Safe for {stopDelay}s, stopping flee");
                    break;
                }
            }
            else
            {
                safeTimer += Time.deltaTime * 0.5f;
            }
            
            Vector2 primaryFleeDir = Vector2.zero;
            if (dogTransform != null)
            {
                primaryFleeDir = ((Vector2)transform.position - (Vector2)dogTransform.position).normalized;
            }
            
            UpdateNearbyFlock();
            Vector2 flockingInfluence = CalculateFlockingDirection();
            
            // Calculate leader-following behavior
            Vector2 leaderInfluence = CalculateLeaderFollowing(distanceToDog);
            
            float distanceToDogNorm = Mathf.InverseLerp(minDogDistance, maxDogDistance, distanceToDog);
            float flockingWeight = Mathf.Lerp(0.2f, 0.5f, distanceToDogNorm);
            
            // Leader influence gets stronger when dog is close
            float leaderWeight = isLeader ? 0f : Mathf.Lerp(leaderFollowWeight, 0.2f, distanceToDogNorm);
            
            // Blend flee, flocking, and leader-following
            Vector2 combinedDirection = primaryFleeDir;
            combinedDirection = Vector2.Lerp(combinedDirection, flockingInfluence, flockingWeight);
            combinedDirection = Vector2.Lerp(combinedDirection, leaderInfluence, leaderWeight);
            combinedDirection = combinedDirection.normalized;
            
            combinedDirection = SmoothSteerTowards(currentDirection, combinedDirection, distanceToDog);
            combinedDirection = ApplyGateMagnetism(combinedDirection);
            
            currentDirection = combinedDirection;
            currentSpeed = CalculatePanicSpeed();
            
            if (rb != null && !CapturedByGate)
                rb.linearVelocity = currentDirection * currentSpeed;
            else if (!CapturedByGate)
                transform.position += (Vector3)(currentDirection * currentSpeed * Time.deltaTime);
            
            UpdateVisualRotation();
            UpdateAnimator();
            
            yield return null;
        }

        if (!CapturedByGate)
        {
            if (isLeader)
            {
                isLeader = false;
                if (currentLeader == this)
                    currentLeader = null;
            }
            
            if (rb != null)
            {
                if (smoothStop)
                    yield return SmoothStop();
                else
                    rb.linearVelocity = Vector2.zero;
            }

            movementRoutine = null;
            Alerted = false;
            currentSpeed = 0f;
            UpdateAnimator();
        }
    }

    private void UpdateVisualRotation()
    {
        if (currentDirection.sqrMagnitude < 0.01f) return; // Don't rotate if not moving
        
        // Calculate target angle from movement direction
        float targetAngle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
        targetAngle -= 90f + spriteDirectionOffset; // Adjust for sprite facing up by default
        
        // Create target rotation
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
        
        // Smoothly rotate towards target
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, 
            targetRotation, 
            rotationSpeed * Time.deltaTime
        );
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        
        bool isMoving = currentSpeed > 0.1f;
        
        // Set IsMoving boolean parameter
        animator.SetBool("isRunning", isMoving);
        
        // // Calculate animation speed multiplier based on current movement speed
        // float speedMultiplier = currentSpeed / normalAnimationSpeed;
        // speedMultiplier = Mathf.Clamp(speedMultiplier, 0f, 3f); // Cap at 3x speed
        //
        // // Set Speed float parameter
        // animator.SetFloat("Speed", speedMultiplier);
    }

    private Vector2 CalculateLeaderFollowing(float distanceToDog)
    {
        // Leader doesn't follow itself
        if (isLeader || currentLeader == null || currentLeader.CapturedByGate)
            return currentDirection;
        
        float distanceToLeader = Vector2.Distance(transform.position, currentLeader.transform.position);
        
        // If dog is close, stick tight to leader
        if (distanceToDog < minDogDistance * 1.5f)
        {
            if (distanceToLeader > 1f) // Stay very close
            {
                Vector2 towardsLeader = ((Vector2)currentLeader.transform.position - (Vector2)transform.position).normalized;
                return towardsLeader;
            }
        }
        
        // If dog is far and outside leader radius, disperse
        if (distanceToDog > disperseDistance && distanceToLeader > leaderFollowRadius)
        {
            // Return to wandering - don't actively follow
            return currentDirection;
        }
        
        // Medium distance: try to stay in leader's cluster
        if (distanceToLeader > leaderFollowRadius * 0.5f)
        {
            Vector2 towardsLeader = ((Vector2)currentLeader.transform.position - (Vector2)transform.position).normalized;
            
            // Blend with current direction to avoid sudden jerks
            return Vector2.Lerp(currentDirection, towardsLeader, leaderStickiness * Time.deltaTime).normalized;
        }
        
        // Already close to leader, match leader's direction
        return Vector2.Lerp(currentDirection, currentLeader.currentDirection, 0.5f);
    }

    private float CalculatePanicSpeed()
    {
        if (dogTransform == null) return minFleeSpeed;

        float distanceToDog = Vector2.Distance(transform.position, dogTransform.position);
        float normalizedDistance = Mathf.InverseLerp(panicSpeedDistance, calmSpeedDistance, distanceToDog);
        float panicLevel = 1f - normalizedDistance;
        float speed = Mathf.Lerp(minFleeSpeed, maxFleeSpeed, panicLevel);
        
        return speed;
    }

    private int CountNearbyCapturedSheep()
    {
        int count = 0;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, groupFollowCheckRadius);
        
        foreach (var hit in hits)
        {
            if (hit != null && hit.gameObject != gameObject && hit.CompareTag(allyTag))
            {
                FlockingSheep sheep = hit.GetComponent<FlockingSheep>();
                if (sheep != null && sheep.CapturedByGate)
                {
                    count++;
                }
            }
        }
        
        return count;
    }

    private Vector2 ApplyGateMagnetism(Vector2 currentFleeDir)
    {
        if (gateTarget == null) return currentFleeDir;

        float distanceToGate = Vector2.Distance(transform.position, gateTarget.position);
        
        if (distanceToGate > gateAttractionStartDistance)
            return currentFleeDir;
        
        float normalizedDistance = Mathf.InverseLerp(gateAttractionStartDistance, gateAttractionForceDistance, distanceToGate);
        float attractionStrength = Mathf.Pow(1f - normalizedDistance, 2f);
        
        // Check if nearby group members reached the gate
        int capturedCount = CountNearbyCapturedSheep();
        float groupBoost = capturedCount > 0 ? groupFollowMagnetismBoost * Mathf.Min(capturedCount, 3) : 0f;
        
        float finalInfluence = Mathf.Clamp01(maxGateInfluence + groupBoost);
        attractionStrength *= finalInfluence;
        
        if (debugLogs && groupBoost > 0)
            Debug.Log($"{name} - {capturedCount} nearby captured! Boost: {groupBoost:F2}", this);
        
        Vector2 towardsGate = ((Vector2)gateTarget.position - (Vector2)transform.position).normalized;
        
        return Vector2.Lerp(currentFleeDir, towardsGate, attractionStrength).normalized;
    }

    private Vector2 SmoothSteerTowards(Vector2 currentDir, Vector2 targetDir, float distanceToDog)
    {
        float normalizedDistance = Mathf.InverseLerp(minDogDistance, maxDogDistance, distanceToDog);
        float currentSteeringSpeed = Mathf.Lerp(closeSteeringSpeed, farSteeringSpeed, normalizedDistance);
        
        float currentAngle = Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
        float angleDiff = Mathf.DeltaAngle(currentAngle, targetAngle);
        
        float correction = Mathf.MoveTowards(0f, angleDiff, currentSteeringSpeed * Time.deltaTime);
        float newAngle = currentAngle + correction;
        
        return new Vector2(Mathf.Cos(newAngle * Mathf.Deg2Rad), Mathf.Sin(newAngle * Mathf.Deg2Rad));
    }

    private void UpdateNearbyFlock()
    {
        nearbyFlock.Clear();
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, flockRadius);
        
        foreach (var hit in hits)
        {
            if (hit != null && hit.gameObject != gameObject && hit.CompareTag(allyTag))
            {
                FlockingSheep sheep = hit.GetComponent<FlockingSheep>();
                if (sheep != null && sheep.Alerted && !sheep.CapturedByGate)
                {
                    nearbyFlock.Add(sheep);
                }
            }
        }
    }

    private Vector2 CalculateFlockingDirection()
    {
        if (nearbyFlock.Count == 0)
            return currentDirection;

        Vector2 separation = Vector2.zero;
        Vector2 alignment = Vector2.zero;
        Vector2 cohesion = Vector2.zero;

        foreach (var sheep in nearbyFlock)
        {
            Vector2 offset = (Vector2)transform.position - (Vector2)sheep.transform.position;
            float distance = offset.magnitude;
            
            if (distance > 0)
                separation += offset.normalized / distance;
            
            // Weight leader's direction more heavily
            float alignmentWeight = sheep.isLeader ? 2f : 1f;
            alignment += sheep.currentDirection * alignmentWeight;
            
            cohesion += (Vector2)sheep.transform.position;
        }

        if (nearbyFlock.Count > 0)
        {
            alignment /= nearbyFlock.Count;
            
            // If leader is nearby, use leader as cohesion target
            if (currentLeader != null && Vector2.Distance(transform.position, currentLeader.transform.position) < flockRadius)
            {
                cohesion = (Vector2)currentLeader.transform.position - (Vector2)transform.position;
            }
            else
            {
                cohesion = (cohesion / nearbyFlock.Count) - (Vector2)transform.position;
            }
            
            cohesion = cohesion.normalized;
        }

        Vector2 flockDir = (separation * separationWeight + 
                           alignment * alignmentWeight + 
                           cohesion * cohesionWeight).normalized;
        
        return Vector2.Lerp(currentDirection, flockDir, 0.5f).normalized;
    }

    private IEnumerator SmoothStop()
    {
        float t = 0f;
        Vector2 startVel = rb.linearVelocity;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            rb.linearVelocity = Vector2.Lerp(startVel, Vector2.zero, t);
            
            // Update current speed for animation
            currentSpeed = rb.linearVelocity.magnitude;
            UpdateAnimator();
            
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;
        currentSpeed = 0f;
        UpdateAnimator();
    }

    private IEnumerator AlertNearbyAllies()
    {
        canAlert = false;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, alertRange);
        foreach (var hit in hits)
        {
            if (hit != null && hit.CompareTag(allyTag) && hit.gameObject != gameObject)
            {
                FlockingSheep ally = hit.GetComponent<FlockingSheep>();
                if (ally != null && !ally.Alerted && !ally.CapturedByGate)
                {
                    ally.dogTransform = dogTransform;
                    ally.AlertAndRun();
                }
            }
        }

        yield return new WaitForSeconds(alertCooldown);
        canAlert = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, alertRange);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, flockRadius);
        
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, groupFollowCheckRadius);
        
        if (gateTarget != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(gateTarget.position, gateAttractionStartDistance);
            
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            Gizmos.DrawWireSphere(gateTarget.position, gateAttractionForceDistance);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(gateTarget.position, gateCaptureRadius);
        }
        
        if (Application.isPlaying && dogTransform != null && Alerted)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(dogTransform.position, panicSpeedDistance);
            
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(dogTransform.position, keepRunningDistance);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(dogTransform.position, safeDistance);
            
            // Draw disperse distance
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(dogTransform.position, disperseDistance);
        }
        
        if (Application.isPlaying && !CapturedByGate)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)currentDirection * 2f);
        }
        
        // Draw leader connections
        if (Application.isPlaying && currentLeader != null && !isLeader && Alerted)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.5f);
            Gizmos.DrawLine(transform.position, currentLeader.transform.position);
        }
        
        // Highlight leader
        if (Application.isPlaying && isLeader)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 1f);
            
            Gizmos.color = new Color(1f, 0f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, leaderFollowRadius);
        }
    }
}
