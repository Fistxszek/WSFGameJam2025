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

    [Header("Sterowanie do wyłączenia (zamiast stringa)")]
    [Tooltip("Jeśli przypięte – wyłączy dokładnie TEN komponent (np. z obiektu gracza w scenie).")]
    public Behaviour disableThisExactComponent;

    [Tooltip("Jeśli przypięte – użyje TYLKO TYPU tego komponentu (np. z prefabu) i wyszuka go na graczu.")]
    public Behaviour componentTypeReference;

    [Tooltip("Szukaj komponentu również w dzieciach gracza.")]
    public bool searchInChildren = true;

    [Header("Efekt – cel (bird)")]
    [Tooltip("Jeśli ustawiony i aktywny w scenie – użyty jako cel.")]
    public Transform birdTarget;

    [Tooltip("Prefab ptaka. Jeśli nie ma istniejącego celu – można go zespawnować.")]
    public GameObject birdPrefab;

    [Tooltip("Tag istniejących ptaków w scenie do wyszukania (np. 'Bird').")]
    public string birdTag = "Bird";

    [Tooltip("Opcjonalny spawner, który potrafi spawnować ptaki w losowych miejscach.")]
    public RandomSpawner2D birdSpawner;

    [Tooltip("Jeśli brak celu – najpierw spróbuj znaleźć najbliższy z tagiem 'birdTag'.")]
    public bool preferNearestTaggedBird = true;

    [Tooltip("Jeśli nadal brak celu – zespawnuj nowego ptaka.")]
    public bool spawnIfNoBirdFound = true;

    [Header("Ruch do celu")]
    public float moveSpeed = 3f;
    public float stopDistance = 0.2f;
    public float effectDuration = 3f; // maks czas trwania efektu (opcjonalny)

    [Header("Mash (ucieczka)")]
    [Min(0f)] public float mashPerPress = 0.2f;
    [Min(0f)] public float mashDecayPerSecond = 0.25f;
    public KeyCode mashKey = KeyCode.E;

    [Header("UI – Mash bar (opcjonalne)")]
    public Slider[] mashSliders;
    public UnityEvent<float> OnMashValueChanged;

    [Header("Łagodne przejęcie")]
    public bool smoothTakeover = true;
    [Min(0f)] public float takeoverEaseIn = 0.35f;
    public AnimationCurve takeoverCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Min(0f)] public float rotateLerpSpeed = 12f;

    [Header("UI – Baner efektu")]
    public RectTransform effectBanner;
    public Vector2 bannerHiddenPos = new Vector2(-1000f, 0f);
    public Vector2 bannerShownPos = new Vector2(0f, 0f);
    [Min(0.01f)] public float bannerSlideDuration = 0.25f;
    public AnimationCurve bannerSlideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Min(0f)] public float bannerPulseAmplitude = 0.06f;
    [Min(0f)] public float bannerPulseFrequency = 3.0f;
    public bool bannerUseUnscaledTime = true;
    public CanvasGroup bannerCanvasGroup;

    [Header("Debug")]
    public bool debugLogs = true;

    // Stan
    private bool playerInside = false;
    private bool isEffectActive = false;

    private Coroutine rollRoutine;   // pętla losowań (pauzowana na czas efektu)
    private Coroutine moveRoutine;
    private Coroutine bannerPulseRoutine;
    private Coroutine bannerSlideRoutine;

    private GameObject cachedPlayer;
    private Behaviour disabledScript;  // rzeczywiście wyłączony komponent

    // Pasek ucieczki [0..1]
    private float mashValue = 0f;

    private void Start()
    {
        ConfigureSliders();
        PushMashToUI();

        if (effectBanner != null)
        {
            effectBanner.anchoredPosition = bannerHiddenPos;
            if (bannerCanvasGroup != null) bannerCanvasGroup.alpha = 0f;
            effectBanner.localScale = Vector3.one;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInside = true;
        cachedPlayer = other.gameObject;
        if (playerTransform == null) playerTransform = cachedPlayer.transform;

        StartRollLoopFresh(); // start losowania po wejściu (pełny reset timera)
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInside = false;
        StopRollLoop();

        if (moveRoutine != null) { StopCoroutine(moveRoutine); moveRoutine = null; }
        if (isEffectActive) RestoreAfterEffect(); // elegancko zakończ efekt, jeśli trwał
    }

    // --------- LOSOWANIE EFEKTU ---------

    private void StartRollLoopFresh()
    {
        StopRollLoop();
        rollRoutine = StartCoroutine(RollLoop(initialDelay: true));
    }

    private void StopRollLoop()
    {
        if (rollRoutine != null)
        {
            StopCoroutine(rollRoutine);
            rollRoutine = null;
        }
    }

    private IEnumerator RollLoop(bool initialDelay)
    {
        if (initialDelay)
            yield return new WaitForSeconds(interval);

        while (playerInside && cachedPlayer != null)
        {
            if (isEffectActive) { yield return null; continue; }

            yield return new WaitForSeconds(interval);
            if (!playerInside || cachedPlayer == null || isEffectActive) continue;

            if (Random.value <= (chancePercent / 100f))
            {
                StopRollLoop();       // pauza losowań na czas efektu
                ApplyEffect(cachedPlayer);
                yield break;          // wznowimy po RestoreAfterEffect()
            }
        }

        rollRoutine = null;
    }

    // --------- EFEKT ---------

    private void ApplyEffect(GameObject player)
    {
        if (isEffectActive) return;

        // Ustal cel (istniejący / najbliższy po tagu / spawn)
        if (!ResolveBirdTarget(player))
        {
            if (debugLogs) Debug.LogWarning("[Zone] Nie udało się ustalić celu (bird). Efekt anulowany.");
            StartRollLoopFresh();
            return;
        }

        if (debugLogs) Debug.Log($"[Zone] Trafienie! Cel: {birdTarget?.name ?? "null"} (mash {mashKey} by uciec)");

        // Reset mash
        mashValue = 0f;
        isEffectActive = true;
        PushMashToUI();

        // Wyłącz sterowanie na graczu (po referencji/typie)
        disabledScript = DisableTargetComponentOnPlayer(player);

        // UI baner – pokaż + pulsuj
        ShowEffectBanner();

        // Ruch
        if (moveRoutine == null)
            moveRoutine = StartCoroutine(MovePlayerToBirdWithMash(player));
    }

    /// <summary>
    /// Ustala cel (birdTarget) wg priorytetu:
    /// 1) przypięty Transform (aktywny),
    /// 2) najbliższy obiekt z tagiem,
    /// 3) spawn przez spawner,
    /// 4) lokalny spawn z prefabu.
    /// </summary>
    private bool ResolveBirdTarget(GameObject player)
    {
        // 1) Jeżeli już jest (i aktywny) – używamy
        if (birdTarget != null && birdTarget.gameObject.activeInHierarchy)
            return true;

        // 2) Szukaj najbliższego z tagiem
        if (preferNearestTaggedBird && !string.IsNullOrWhiteSpace(birdTag))
        {
            var nearest = FindNearestByTag(birdTag, player.transform.position);
            if (nearest != null) { birdTarget = nearest.transform; return true; }
        }

        // 3) Użyj spawnera (jeśli jest)
        if (spawnIfNoBirdFound && birdSpawner != null)
        {
            // jeśli spawner nie ma prefabu, a my mamy — podepnij
            if (birdSpawner.prefabToSpawn == null && birdPrefab != null)
                birdSpawner.prefabToSpawn = birdPrefab;

            var spawned = birdSpawner.SpawnOne(); // wymaga Twojej metody SpawnOne()
            if (spawned != null) { birdTarget = spawned.transform; return true; }
        }

        // 4) Lokalny spawn z prefabu
        if (spawnIfNoBirdFound && birdPrefab != null)
        {
            Vector2 pos = player.transform.position;
            if (birdSpawner != null && birdSpawner.spawnArea != null)
                pos = GetRandomPointInArea(birdSpawner.spawnArea);

            var go = Instantiate(birdPrefab, pos, Quaternion.identity);
            if (!string.IsNullOrWhiteSpace(birdTag)) go.tag = birdTag; // opcjonalnie nadaj tag
            birdTarget = go.transform;
            return true;
        }

        // Nie udało się
        return false;
    }

    private static GameObject FindNearestByTag(string tag, Vector2 fromPos)
    {
        var objs = GameObject.FindGameObjectsWithTag(tag);
        if (objs == null || objs.Length == 0) return null;

        GameObject best = null;
        float bestSqr = float.PositiveInfinity;

        for (int i = 0; i < objs.Length; i++)
        {
            var go = objs[i];
            if (!go || !go.activeInHierarchy) continue;
            float d = ((Vector2)go.transform.position - fromPos).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = go; }
        }

        return best;
    }

    private static Vector2 GetRandomPointInArea(BoxCollider2D area)
    {
        Vector2 c = area.bounds.center;
        Vector2 s = area.bounds.size;
        float x = Random.Range(c.x - s.x * 0.5f, c.x + s.x * 0.5f);
        float y = Random.Range(c.y - s.y * 0.5f, c.y + s.y * 0.5f);
        return new Vector2(x, y);
    }

    // --------- Wyłączanie/przywracanie komponentu sterowania ---------

    private Behaviour DisableTargetComponentOnPlayer(GameObject player)
    {
        // 1) Dokładnie ten komponent (przeciągnięty w Inspektorze)
        if (disableThisExactComponent != null)
        {
            disableThisExactComponent.enabled = false;
            if (debugLogs) Debug.Log($"[Zone] Wyłączono dokładny komponent: {disableThisExactComponent.GetType().Name}");
            return disableThisExactComponent;
        }

        // 2) Po typie z referencji (np. z prefabu)
        if (componentTypeReference != null)
        {
            var targetType = componentTypeReference.GetType();
            Behaviour found = null;

            if (searchInChildren)
                found = player.GetComponentInChildren(targetType, includeInactive: true) as Behaviour;
            else
                found = player.GetComponent(targetType) as Behaviour;

            if (found != null)
            {
                found.enabled = false;
                if (debugLogs) Debug.Log($"[Zone] Wyłączono komponent typu: {targetType.Name} na graczu.");
                return found;
            }
            else
            {
                if (debugLogs) Debug.LogWarning($"[Zone] Nie znaleziono na graczu komponentu typu {targetType.Name}.");
            }
        }

        if (debugLogs) Debug.LogWarning("[Zone] Nie wskazano komponentu do wyłączenia (ani exact, ani type reference).");
        return null;
    }

    private void RestoreDisabledComponent(Behaviour b)
    {
        if (b == null) return;
        b.enabled = true;
        if (debugLogs) Debug.Log($"[Zone] Przywrócono komponent: {b.GetType().Name}");
    }

    // --------- Ruch + Mash ---------

    private IEnumerator MovePlayerToBirdWithMash(GameObject player)
    {
        float elapsed = 0f;
        float takeoverT = 0f;
        var rb = player != null ? player.GetComponent<Rigidbody2D>() : null;

        if (rb != null) rb.linearVelocity = Vector2.zero;

        while (player != null && isEffectActive)
        {
            // --- Mash ---
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

            // --- Ramp-up prędkości + miękki obrót ---
            float speedMul = 1f;
            if (smoothTakeover && takeoverEaseIn > 0f)
            {
                takeoverT = Mathf.Clamp01(takeoverT + Time.deltaTime / takeoverEaseIn);
                speedMul = Mathf.Clamp01(takeoverCurve.Evaluate(takeoverT));
            }

            // --- Ruch/obrót do celu ---
            if (birdTarget != null)
            {
                Vector2 from = player.transform.position;
                Vector2 to = birdTarget.position;
                Vector2 dir = to - from;

                if (!birdTarget.gameObject.activeInHierarchy)
                    break; // cel zniknął

                if (playerTransform != null && rotateLerpSpeed > 0f && dir.sqrMagnitude > 0.0001f)
                {
                    float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
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
        // Przywróć sterowanie
        RestoreDisabledComponent(disabledScript);
        disabledScript = null;

        isEffectActive = false;
        mashValue = 0f;
        PushMashToUI();

        HideEffectBanner();

        // Reset losowania po efekcie (pełny interval)
        if (playerInside && cachedPlayer != null)
            StartRollLoopFresh();
    }

    // ---------- UI: Mash Helpers ----------
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

    // ---------- UI: Banner Anim ----------
    private void ShowEffectBanner()
    {
        if (effectBanner == null) return;

        if (bannerSlideRoutine != null) StopCoroutine(bannerSlideRoutine);
        if (bannerPulseRoutine != null) StopCoroutine(bannerPulseRoutine);

        effectBanner.anchoredPosition = bannerHiddenPos;
        effectBanner.localScale = Vector3.one;

        bannerSlideRoutine = StartCoroutine(SlideBanner(effectBanner, bannerHiddenPos, bannerShownPos, true));
    }

    private void HideEffectBanner()
    {
        if (effectBanner == null) return;

        if (bannerSlideRoutine != null) StopCoroutine(bannerSlideRoutine);
        if (bannerPulseRoutine != null) StopCoroutine(bannerPulseRoutine);

        bannerSlideRoutine = StartCoroutine(SlideBanner(effectBanner, effectBanner.anchoredPosition, bannerHiddenPos, false));
    }

    private IEnumerator SlideBanner(RectTransform rt, Vector2 from, Vector2 to, bool startPulseAfter)
    {
        float t = 0f;

        float startAlpha = bannerCanvasGroup ? bannerCanvasGroup.alpha : 1f;
        float targetAlpha = startPulseAfter ? 1f : 0f;

        while (t < 1f)
        {
            float dt = bannerUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt / bannerSlideDuration;

            float k = bannerSlideCurve.Evaluate(Mathf.Clamp01(t));
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);

            if (bannerCanvasGroup != null)
                bannerCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, k);

            yield return null;
        }

        rt.anchoredPosition = to;
        if (bannerCanvasGroup != null) bannerCanvasGroup.alpha = targetAlpha;

        bannerSlideRoutine = null;

        if (startPulseAfter)
            bannerPulseRoutine = StartCoroutine(PulseBanner(rt));
    }

    private IEnumerator PulseBanner(Transform tr)
    {
        float timeAcc = 0f;
        while (isEffectActive)
        {
            float dt = bannerUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            timeAcc += dt;
            float s = 1f + Mathf.Sin(timeAcc * Mathf.PI * 2f * bannerPulseFrequency) * bannerPulseAmplitude;
            tr.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        tr.localScale = Vector3.one;
        bannerPulseRoutine = null;
    }
}
