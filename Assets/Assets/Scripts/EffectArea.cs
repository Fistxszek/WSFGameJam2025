using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ZoneEffectSimple : MonoBehaviour
{
    [Header("Ustawienia strefy")]
    public string playerTag = "Player";
    public Transform playerTransform; // opcjonalne; jeśli puste — użyje cachedPlayer
    [Range(0, 100)] public float chancePercent = 30f;
    public float interval = 5f;

    [Header("Efekt")]
    [Tooltip("Nazwa skryptu na graczu, który ma zostać tymczasowo wyłączony.")]
    public string scriptNameToDisable = "PlayerMovement";

    [Tooltip("Cel ruchu. Jeśli puste, zostanie wyszukany obiekt o nazwie 'bird'.")]
    public Transform birdTarget;
    public float moveSpeed = 3f;
    public float stopDistance = 0.2f;
    public float effectDuration = 3f; // maks czas trwania efektu (opcjonalny)

    [Header("Mash (ucieczka)")]
    [Min(0f)] public float mashPerPress = 0.2f;
    [Min(0f)] public float mashDecayPerSecond = 0.25f;
    public KeyCode mashKey = KeyCode.E;

    [Header("UI (opcjonalne)")]
    public Slider[] mashSliders;
    public UnityEvent<float> OnMashValueChanged;

    [Header("Łagodne przejęcie")]
    public bool smoothTakeover = true;
    [Tooltip("Czas najazdu (sek) od 0 do pełnej prędkości po rozpoczęciu efektu.")]
    [Min(0f)] public float takeoverEaseIn = 0.35f;
    [Tooltip("Krzywa wzrostu prędkości (X: 0..1 czas, Y: 0..1 mnożnik).")]
    public AnimationCurve takeoverCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("Szybkość wygładzania obrotu (większa = szybszy obrót).")]
    [Min(0f)] public float rotateLerpSpeed = 12f;

    [Header("Debug")]
    public bool debugLogs = true;

    // Stan
    private bool playerInside = false;
    private bool isEffectActive = false;

    private Coroutine effectRoutine;
    private Coroutine moveRoutine;

    private GameObject cachedPlayer;
    private Behaviour disabledScript;

    // Pasek ucieczki [0..1]
    private float mashValue = 0f;

    private void Start()
    {
        if (birdTarget == null)
        {
            var found = GameObject.Find("bird");
            if (found != null) birdTarget = found.transform;
        }

        ConfigureSliders();
        PushMashToUI();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInside = true;
        cachedPlayer = other.gameObject;
        if (playerTransform == null) playerTransform = cachedPlayer.transform;

        if (effectRoutine == null)
            effectRoutine = StartCoroutine(EffectLoop());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInside = false;

        if (effectRoutine != null)
        {
            StopCoroutine(effectRoutine);
            effectRoutine = null;
        }
    }

    private IEnumerator EffectLoop()
    {
        while (playerInside && cachedPlayer != null)
        {
            yield return new WaitForSeconds(interval);

            if (!playerInside || cachedPlayer == null) break;

            if (Random.value <= (chancePercent / 100f))
            {
                ApplyEffect(cachedPlayer);
            }
        }

        effectRoutine = null;
    }

    private void ApplyEffect(GameObject player)
    {
        if (isEffectActive) return;

        if (debugLogs) Debug.Log($"[Zone] Trafienie! Wyłączam '{scriptNameToDisable}' i przejmuję sterowanie. (mash {mashKey} by uciec)");

        // Reset mash
        mashValue = 0f;
        isEffectActive = true;
        PushMashToUI();

        // Wyłącz wskazany skrypt sterowania
        disabledScript = FindAndDisableScript(player);

        // Rusz z łagodnym „ramp-upem”
        if (moveRoutine == null)
            moveRoutine = StartCoroutine(MovePlayerToBirdWithMash(player));
    }

    private Behaviour FindAndDisableScript(GameObject player)
    {
        if (string.IsNullOrWhiteSpace(scriptNameToDisable)) return null;

        var behaviours = player.GetComponents<MonoBehaviour>();
        foreach (var b in behaviours)
        {
            if (b == null) continue;
            if (b.GetType().Name == scriptNameToDisable)
            {
                var asBehaviour = b as Behaviour;
                if (asBehaviour != null)
                {
                    asBehaviour.enabled = false;
                    if (debugLogs) Debug.Log($"[Zone] Wyłączono {asBehaviour.GetType().Name}.");
                    return asBehaviour;
                }
            }
        }
        return null;
    }

    private IEnumerator MovePlayerToBirdWithMash(GameObject player)
    {
        float elapsed = 0f;            // czas trwania całego efektu
        float takeoverT = 0f;          // 0→1: postęp łagodnego przejęcia
        var rb = player != null ? player.GetComponent<Rigidbody2D>() : null;

        // zatrzymaj momentalnie bieżącą prędkość RB (żeby nie „szarpało”)
        if (rb != null) rb.linearVelocity = Vector2.zero;

        while (player != null && isEffectActive)
        {
            // --- Mashowanie ---
            if (Input.GetKeyDown(mashKey))
            {
                mashValue = Mathf.Min(1f, mashValue + mashPerPress);
                PushMashToUI();
                if (debugLogs) Debug.Log($"[Zone] Mash {mashKey}! value={mashValue:0.00}");
            }

            if (mashDecayPerSecond > 0f)
            {
                float old = mashValue;
                mashValue = Mathf.Max(0f, mashValue - mashDecayPerSecond * Time.deltaTime);
                if (!Mathf.Approximately(old, mashValue))
                    PushMashToUI();
            }

            if (mashValue >= 0.99f)
            {
                if (debugLogs) Debug.Log("[Zone] Ucieczka! Efekt przerwany przez mash.");
                break;
            }

            // --- Łagodny ramp-up prędkości + miękki obrót ---
            float speedMul = 1f;
            if (smoothTakeover && takeoverEaseIn > 0f)
            {
                takeoverT = Mathf.Clamp01(takeoverT + Time.deltaTime / takeoverEaseIn);
                speedMul = Mathf.Clamp01(takeoverCurve.Evaluate(takeoverT));
            }

            // --- Ruch i obrót do celu (jeśli jest) ---
            if (birdTarget != null)
            {
                Vector2 from = player.transform.position;
                Vector2 to = birdTarget.position;
                Vector2 dir = to - from;

                // miękki obrót gracza (PO PLAYERZE, nie po strefie)
                if (playerTransform != null && rotateLerpSpeed > 0f && dir.sqrMagnitude > 0.0001f)
                {
                    float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f; // jeśli sprite „patrzy w górę”
                    float currentZ = playerTransform.eulerAngles.z;
                    float newZ = Mathf.LerpAngle(currentZ, targetAngle, rotateLerpSpeed * Time.deltaTime);
                    playerTransform.rotation = Quaternion.Euler(0f, 0f, newZ);
                }

                if (dir.sqrMagnitude <= (stopDistance * stopDistance))
                {
                    if (debugLogs) Debug.Log("[Zone] Dotarto do 'bird'.");
                    break;
                }

                float currentSpeed = moveSpeed * speedMul;
                Vector2 step = dir.normalized * currentSpeed * Time.deltaTime;

                if (rb != null)
                    rb.MovePosition(rb.position + step);
                else
                    player.transform.position = (Vector2)player.transform.position + step;
            }

            // --- Limit czasu (opcjonalny) ---
            elapsed += Time.deltaTime;
            if (effectDuration > 0f && elapsed >= effectDuration)
            {
                if (debugLogs) Debug.Log("[Zone] Minął maksymalny czas efektu. Przywracam kontrolę.");
                break;
            }

            yield return null;
        }

        RestoreAfterEffect();
        moveRoutine = null;
    }

    private void RestoreAfterEffect()
    {
        if (disabledScript != null)
        {
            disabledScript.enabled = true;
            if (debugLogs) Debug.Log($"[Zone] Skrypt {disabledScript.GetType().Name} został przywrócony.");
            disabledScript = null;
        }

        isEffectActive = false;
        mashValue = 0f;
        PushMashToUI();
    }

    // ---------- UI Helpers ----------
    private void ConfigureSliders()
    {
        if (mashSliders == null) return;
        foreach (var s in mashSliders)
        {
            if (s == null) continue;
            s.minValue = 0f;
            s.maxValue = 1f;
            s.wholeNumbers = false;
        }
    }

    private void PushMashToUI()
    {
        if (mashSliders != null)
        {
            for (int i = 0; i < mashSliders.Length; i++)
            {
                var s = mashSliders[i];
                if (s == null) continue;
                s.value = mashValue;
            }
        }

        OnMashValueChanged?.Invoke(mashValue);
    }
}
