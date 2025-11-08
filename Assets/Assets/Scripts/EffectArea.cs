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

    [Header("Stop na celu")]
    [Min(0f)] public float holdAtTargetSeconds = 2f;


    [Header("Sterowanie do wyłączenia (opcjonalne)")]
    public DogMovement disableThisExactComponent;   // wyłączy dokładnie ten komponent
    public DogMovement componentTypeReference;      // albo wyłączy komponent po typie znaleziony na graczu
    public bool searchInChildren = true;

    [Header("Cele (ptaki) – tylko istniejące")]
    public Transform birdTarget;                  // opcjonalny istniejący cel
    public string birdTag = "Bird";               // WSZYSTKIE ptaki muszą mieć ten tag
    public bool preferNearestTaggedBird = true;   // gdy jest wiele

    [Header("Ruch do celu")]
    public float moveSpeed = 3f;
    public float stopDistance = 0.2f;
    public float effectDuration = 3f;

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

    [Header("Po mashu: usuń wszystkie ptaki i poproś o respawn (event)")]
    public bool destroyAllBirdsOnMashInterrupt = true;
    [Min(0f)] public float birdRespawnDelay = 5f;

    [Tooltip("Zostanie wywołane po birdRespawnDelay od udanego mashu. " +
             "Podepnij tutaj np. RandomSpawner2D.SpawnObjects() albo własny manager.")]
    public UnityEvent OnRespawnRequested;

    [Header("Debug")]
    public bool debugLogs = true;

    // Stan
    private bool playerInside = false;
    private bool isEffectActive = false;

    private Coroutine rollRoutine;
    private Coroutine moveRoutine;
    private Coroutine bannerPulseRoutine;
    private Coroutine bannerSlideRoutine;

    private GameObject cachedPlayer;
    private Behaviour disabledScript;

    private float mashValue = 0f;
    private bool interruptedByMash = false;

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

        StartRollLoopFresh();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInside = false;
        StopRollLoop();

        if (moveRoutine != null) { StopCoroutine(moveRoutine); moveRoutine = null; }
        if (isEffectActive) RestoreAfterEffect();
    }

    // --------- LOSOWANIE ---------

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
                StopRollLoop();
                ApplyEffect(cachedPlayer);
                yield break;
            }
        }

        rollRoutine = null;
    }

    // --------- EFEKT ---------

    private void ApplyEffect(GameObject player)
    {
        if (isEffectActive) return;

        if (!ResolveBirdTarget(player))
        {
            if (debugLogs) Debug.LogWarning("[Zone] Brak dostępnego celu (bird) w scenie. Efekt pominięty.");
            StartRollLoopFresh();
            return;
        }

        if (debugLogs) Debug.Log($"[Zone] Trafienie! Cel: {birdTarget?.name ?? "null"} (mash {mashKey})");

        mashValue = 0f;
        interruptedByMash = false;
        isEffectActive = true;
        PushMashToUI();

        disabledScript = DisableTargetComponentOnPlayer(player);

        ShowEffectBanner();

        if (moveRoutine == null)
            moveRoutine = StartCoroutine(MovePlayerToBirdWithMash(player));
    }

    private bool ResolveBirdTarget(GameObject player)
    {
        // 1) Przypięty i aktywny
        if (birdTarget != null && birdTarget.gameObject.activeInHierarchy)
            return true;

        // 2) Najbliższy po tagu
        if (preferNearestTaggedBird && !string.IsNullOrWhiteSpace(birdTag))
        {
            var nearest = FindNearestByTag(birdTag, player.transform.position);
            if (nearest != null)
            {
                birdTarget = nearest.transform;
                return true;
            }
        }

        // 3) Brak spawnowania tutaj – jeśli nie znaleziono, anuluj efekt
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

    // --------- Wyłączanie/przywracanie komponentu ---------

    private Behaviour DisableTargetComponentOnPlayer(GameObject player)
    {
        if (disableThisExactComponent != null)
        {
            disableThisExactComponent.enabled = false;
            if (debugLogs) Debug.Log($"[Zone] Wyłączono: {disableThisExactComponent.GetType().Name}");
            return disableThisExactComponent;
        }

        if (componentTypeReference != null)
        {
            var targetType = componentTypeReference.GetType();
            Behaviour found = searchInChildren
                ? player.GetComponentInChildren(targetType, true) as Behaviour
                : player.GetComponent(targetType) as Behaviour;

            if (found != null)
            {
                found.enabled = false;
                if (debugLogs) Debug.Log($"[Zone] Wyłączono typ: {targetType.Name}");
                return found;
            }
            else if (debugLogs) Debug.LogWarning($"[Zone] Nie znaleziono komponentu typu {targetType.Name}");
        }

        return null;
    }

    private void RestoreDisabledComponent(Behaviour b)
    {
        if (b == null) return;
        b.enabled = true;
        if (debugLogs) Debug.Log($"[Zone] Przywrócono: {b.GetType().Name}");
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
            // Mash
            if (Input.GetKeyDown(mashKey))
            {
                mashValue = Mathf.Min(1f, mashValue + mashPerPress);
                PushMashToUI();
                if (debugLogs) Debug.Log($"[Zone] Mash {mashKey}: {mashValue:0.00}");
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
                interruptedByMash = true;
                if (debugLogs) Debug.Log("[Zone] Ucieczka: efekt przerwany mashowaniem.");
                break;
            }

            // Ease-in prędkości
            float speedMul = 1f;
            if (smoothTakeover && takeoverEaseIn > 0f)
            {
                takeoverT = Mathf.Clamp01(takeoverT + Time.deltaTime / takeoverEaseIn);
                speedMul = Mathf.Clamp01(takeoverCurve.Evaluate(takeoverT));
            }

            // Ruch/obrót do celu
            if (birdTarget != null)
            {
                Vector2 from = player.transform.position;
                Vector2 to = birdTarget.position;
                Vector2 dir = to - from;

                if (!birdTarget.gameObject.activeInHierarchy)
                    break;

                if (playerTransform != null && rotateLerpSpeed > 0f && dir.sqrMagnitude > 0.0001f)
                {
                    float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                    float currentZ = playerTransform.eulerAngles.z;
                    float newZ = Mathf.LerpAngle(currentZ, targetAngle, rotateLerpSpeed * Time.deltaTime);
                    playerTransform.rotation = Quaternion.Euler(0f, 0f, newZ);
                }

                if (dir.sqrMagnitude <= (stopDistance * stopDistance))
                {
                    if (debugLogs) Debug.Log("[Zone] Dotarto do celu (ptak zostaje).");
                    if (rb != null) rb.linearVelocity = Vector2.zero;

                    if (holdAtTargetSeconds > 0f)
                        yield return new WaitForSeconds(holdAtTargetSeconds);


                    var birds = GameObject.FindGameObjectsWithTag(birdTag);
                    for (int i = 0; i < birds.Length; i++)
                        if (birds[i] != null) Destroy(birds[i]);
                    break; // nie niszczymy ptaka
                }

                float currentSpeed = moveSpeed * speedMul;
                Vector2 step = dir.normalized * currentSpeed * Time.deltaTime;

                if (rb != null) rb.MovePosition(rb.position + step);
                else player.transform.position = (Vector2)player.transform.position + step;
            }

            // Limit czasu
            elapsed += Time.deltaTime;
            if (effectDuration > 0f && elapsed >= effectDuration)
            {
                if (debugLogs) Debug.Log("[Zone] Minął maksymalny czas efektu.");

                var birds = GameObject.FindGameObjectsWithTag(birdTag);
                for (int i = 0; i < birds.Length; i++)
                    if (birds[i] != null) Destroy(birds[i]);
                break;

            }


            yield return null;
        }

        RestoreAfterEffect();
        moveRoutine = null;
    }

    private void RestoreAfterEffect()
    {
        RestoreDisabledComponent(disabledScript);
        disabledScript = null;

        isEffectActive = false;
        mashValue = 0f;
        PushMashToUI();

        HideEffectBanner();

        if (playerInside && cachedPlayer != null)
            StartRollLoopFresh();

        if (interruptedByMash && destroyAllBirdsOnMashInterrupt)
        {
            StartCoroutine(DestroyAllBirdsThenRequestRespawn());
        }
        interruptedByMash = false;
    }

    private IEnumerator DestroyAllBirdsThenRequestRespawn()
    {
        // 1) Usuń wszystkie ptaki po tagu
        if (string.IsNullOrWhiteSpace(birdTag))
        {
            Debug.LogWarning("[Zone] birdTag jest pusty – nie mogę usunąć wszystkich ptaków. Ustaw tag w Inspectorze.");
        }
        else
        {
            var birds = GameObject.FindGameObjectsWithTag(birdTag);
            for (int i = 0; i < birds.Length; i++)
                if (birds[i] != null) Destroy(birds[i]);

            if (debugLogs) Debug.Log($"[Zone] Usunięto {birds.Length} ptaków (tag: {birdTag}).");
        }

        // 2) Czekaj X sekund
        if (birdRespawnDelay > 0f)
            yield return new WaitForSeconds(birdRespawnDelay);

        // 3) NIE spawnować tutaj. Zamiast tego — zgłoś prośbę o respawn.
        if (OnRespawnRequested != null)
        {
            if (debugLogs) Debug.Log("[Zone] OnRespawnRequested → uruchom swój spawner przez UnityEvent.");
            OnRespawnRequested.Invoke();
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[Zone] OnRespawnRequested niepodpięty – nic nie zostanie zrespawnowane.");
        }
    }

    // ---------- UI helpers ----------
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

    // ---------- Banner ----------
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
