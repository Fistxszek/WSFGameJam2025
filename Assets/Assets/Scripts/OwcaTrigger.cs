using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class FleeAndAlert2D : MonoBehaviour
{
    [Header("Tagi")]
    public string threatTag = "Player";     // co powoduje strach
    public string allyTag = "Sheep";      // kto może zostać ostrzeżony

    [Header("Ucieczka")]
    public float fleeSpeed = 5f;
    public float fleeDuration = 1.5f;
    public bool smoothStop = true;

    [Header("Reakcja na strach")]
    public float alertRange = 3f;         // zasięg „straszenia” innych owiec
    public float alertCooldown = 1f;      // jak często może straszyć inne

    [Header("Debug")]
    public bool debugLogs = false;

    public bool Alerted = false;

    private Rigidbody2D rb;
    private Coroutine fleeRoutine;
    public bool canAlert = true;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Jeśli to wilk → ucieczka i stan alarmu
        if (other.CompareTag(threatTag))
        {
            Vector2 fleeDir = (transform.position - other.transform.position).normalized;
            AlertAndRun(fleeDir);
        }

        // Jeśli to inna owca → zarażenie strachem
        if (other.CompareTag(allyTag))
        {
            FleeAndAlert2D ally = other.GetComponent<FleeAndAlert2D>();
            if (ally != null && ally.Alerted && !Alerted)
            {
                // ta owca „zaraziła się” strachem
                if (debugLogs)
                    Debug.Log($"{name} przestraszyła się od {ally.name}", this);

                AlertAndRun((transform.position - ally.transform.position).normalized);
            }
        }
    }

    private void AlertAndRun(Vector2 direction)
    {
        Alerted = true;
        Run(direction);

        // Rozgłoś alarm (jeśli może)
        if (canAlert)
            StartCoroutine(AlertNearbyAllies());
    }

    public void Run(Vector2 direction)
    {
        if (fleeRoutine != null)
            StopCoroutine(fleeRoutine);

        fleeRoutine = StartCoroutine(FleeRoutine(direction));
    }

    private IEnumerator FleeRoutine(Vector2 direction)
    {
        float elapsed = 0f;
        while (elapsed < fleeDuration)
        {
            elapsed += Time.deltaTime;
            if (rb != null)
                rb.linearVelocity = direction * fleeSpeed;
            else
                transform.position += (Vector3)(direction * fleeSpeed * Time.deltaTime);
            yield return null;
        }

        if (rb != null)
        {
            if (smoothStop)
                yield return SmoothStop();
            else
                rb.linearVelocity = Vector2.zero;
        }

        fleeRoutine = null;
    }

    private IEnumerator SmoothStop()
    {
        float t = 0f;
        Vector2 startVel = rb.linearVelocity;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            rb.linearVelocity = Vector2.Lerp(startVel, Vector2.zero, t);
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;
    }

    private IEnumerator AlertNearbyAllies()
    {
        canAlert = false;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, alertRange);
        foreach (var hit in hits)
        {
            if (hit != null && hit.CompareTag(allyTag))
            {
                FleeAndAlert2D ally = hit.GetComponent<FleeAndAlert2D>();
                if (ally != null && !ally.Alerted)
                {
                    Vector2 dir = (ally.transform.position - transform.position).normalized;
                    ally.AlertAndRun(dir);
                }
            }
        }

        yield return new WaitForSeconds(alertCooldown);
        canAlert = true;
        Alerted = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, alertRange);
    }
}
