using UnityEngine;
using System.Collections;
using FMODUnity;
using UnityEngine.UI;
using TMPro; // dla MatchTextPreferred (opcjonalnie)

[RequireComponent(typeof(Collider2D))]
public class Kwak : MonoBehaviour
{
    [Header("Gracz / wejście do strefy")]
    public string playerTag = "Player";

    [Header("Losowanie Kwak")]
    [Tooltip("Co ile sekund próbować losowania")]
    public float interval = 2f;
    [Range(0, 100)] public float chancePercent = 50f;

    [Header("Ruch gracza do tego obiektu (emitera Kwak)")]
    public float moveSpeed = 3f;
    public float stopDistance = 0.25f;
    public float rotateLerpSpeed = 12f;   // 0 = bez obrotu
    [Tooltip("0 = bez limitu")]
    public float maxHomingDuration = 5f;
    [Tooltip("Opcjonalne zatrzymanie przy celu")]
    public float holdAtTargetSeconds = 0f;

    [Header("Mashowanie (E) – przerwanie")]
    public KeyCode mashKey = KeyCode.E;
    [Min(0f)] public float mashPerPress = 0.25f;
    [Min(0f)] public float mashDecayPerSecond = 0.3f;
    public Slider mashSlider; // opcjonalny

    [Header("UI – Baner efektu (opcjonalne)")]
    public RectTransform effectBanner;
    public Vector2 bannerHiddenPos = new Vector2(-1000f, 0f);
    public Vector2 bannerShownPos = new Vector2(0f, 0f);
    [Min(0.01f)] public float bannerSlideDuration = 0.25f;
    public AnimationCurve bannerSlideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Min(0f)] public float bannerPulseAmplitude = 0.06f;
    [Min(0f)] public float bannerPulseFrequency = 3.0f;
    public bool bannerUseUnscaledTime = true;
    public CanvasGroup bannerCanvasGroup;
    
    [field: SerializeField] public EventReference SFX { get; private set; }

    // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
    // INTUICYJNY ROZMIAR BANERU
    public enum BannerSizeMode { FixedPixels, PercentOfParent, MatchTextPreferred }

    [Header("UI – Banner Size (intuicyjnie)")]
    public BannerSizeMode sizeMode = BannerSizeMode.FixedPixels;

    [Tooltip("Stały rozmiar w pikselach (gdy FixedPixels)")]
    public Vector2 fixedSize = new Vector2(600f, 120f);

    [Tooltip("Udział rozmiaru rodzica (0–1) (gdy PercentOfParent)")]
    [Range(0f, 1f)] public float widthPercent = 0.6f;
    [Range(0f, 1f)] public float heightPercent = 0.15f;

    [Tooltip("Padding dodawany do wyliczonego rozmiaru (piksele)")]
    public Vector2 sizePadding = new Vector2(0f, 0f);

    [Tooltip("Zachowaj proporcje W:H")]
    public bool keepAspect = false;
    [Tooltip("Proporcje (W/H), np. 4:1 → 4.0")]
    public float aspect = 4.0f;
    // <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<

    
    [SerializeField] private Animator _mashUiAnimator;
    [SerializeField] private GameObject _mashUi;
    [SerializeField] private GameObject _lockUi;
    
    
    [Header("Debug")]
    public bool debugLogs = true;

    // --- stan ---
    bool playerInside;
    Transform playerTr;
    Rigidbody2D playerRb;

    Coroutine rollRoutine;
    Coroutine homingRoutine;

    // baner coroutines
    Coroutine bannerPulseRoutine;
    Coroutine bannerSlideRoutine;

    bool homingActive;
    float mashValue;
    float elapsed;

    // alias dla logiki banera (zachowanie 1:1 z Twoim kodem)
    bool isEffectActive => homingActive;

    void Start()
    {
        if (mashSlider != null)
        {
            mashSlider.minValue = 0f;
            mashSlider.maxValue = 1f;
            mashSlider.wholeNumbers = false;
            mashSlider.value = 0f;
        }

        // startowe ukrycie banera + zastosowanie rozmiaru
        if (effectBanner != null)
        {
            ApplyBannerSize(); // <<< nowość
            effectBanner.anchoredPosition = bannerHiddenPos;
            if (bannerCanvasGroup != null) bannerCanvasGroup.alpha = 0f;
            effectBanner.localScale = Vector3.one;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInside = true;
        playerTr = other.transform;
        playerRb = other.GetComponent<Rigidbody2D>();

        StartRollLoop();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInside = false;
        playerTr = null;
        playerRb = null;

        StopRollLoop();
        StopHoming(); // to również schowa baner
    }

    // ================== Losowanie ==================
    void StartRollLoop()
    {
        StopRollLoop();
        if (!homingActive && playerInside) rollRoutine = StartCoroutine(RollLoop());
    }

    void StopRollLoop()
    {
        if (rollRoutine != null)
        {
            StopCoroutine(rollRoutine);
            rollRoutine = null;
        }
    }

    IEnumerator RollLoop()
    {
        yield return new WaitForSeconds(interval);

        while (playerInside && playerTr != null && !homingActive)
        {
            yield return new WaitForSeconds(interval);

            if (!playerInside || playerTr == null || homingActive) continue;

            bool shouldKwak = Random.value < (chancePercent / 100f);
            if (shouldKwak)
            {
                if (debugLogs) Debug.Log("[Kwak] Wylosowano: KWAK → start podążania.");
                StartKwak();
                yield break;
            }
            else if (debugLogs)
            {
                Debug.Log("[Kwak] Wylosowano: cisza.");
            }
        }

        rollRoutine = null;
    }

    // ================== Start efektu Kwak ==================
    void StartKwak()
    {
        if (homingActive || playerTr == null) return;

        homingActive = true;
        mashValue = 0f;
        elapsed = 0f;
        PushMashUI();

        ShowEffectBanner(); // pokaż + rozmiar

        _mashUi.SetActive(true);
        AudioManager.Instance.PlayOneShoot(SFX);
        AudioManager.Instance.PlayOneShoot(FMODEvents.Instance.SzczekSet);
        if (homingRoutine == null)
            homingRoutine = StartCoroutine(HomingToThisObject());
    }

    // ================== Ruch + Mash ==================
    IEnumerator HomingToThisObject()
    {
        if (playerRb != null) playerRb.linearVelocity = Vector2.zero;

        while (homingActive && playerTr != null)
        {
            // --- Mash E ---
            if (Input.GetKeyDown(mashKey))
            {
                _mashUiAnimator.SetTrigger("Pressed");
                AudioManager.Instance.PlayOneShoot(FMODEvents.Instance.Click);
                mashValue = Mathf.Min(1f, mashValue + mashPerPress);
                PushMashUI();
                if (debugLogs) Debug.Log($"[Kwak] Mash {mashKey}: {mashValue:0.00}");
            }
            if (mashDecayPerSecond > 0f && mashValue > 0f)
            {
                float old = mashValue;
                mashValue = Mathf.Max(0f, mashValue - mashDecayPerSecond * Time.deltaTime);
                if (!Mathf.Approximately(old, mashValue)) PushMashUI();
            }
            if (mashValue >= 0.99f)
            {
                if (debugLogs) Debug.Log("[Kwak] Uwolniono się mashowaniem – przerywam ruch.");
                _mashUi.SetActive(false);
                break;
            }

            if (!gameObject.activeInHierarchy) break;
            if (maxHomingDuration > 0f)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= maxHomingDuration)
                {
                    if (debugLogs) Debug.Log("[Kwak] Limit czasu – stop.");
                    _mashUi.SetActive(false);
                    break;
                }
            }

            Vector2 from = playerTr.position;
            Vector2 to = transform.position;
            Vector2 dir = to - from;

            if (dir.sqrMagnitude <= stopDistance * stopDistance)
            {
                if (debugLogs) Debug.Log("[Kwak] Gracz dotarł do emitera.");
                _mashUi.SetActive(false);
                _lockUi.SetActive(true);
                if (holdAtTargetSeconds > 0f)
                    yield return new WaitForSeconds(holdAtTargetSeconds);
                _lockUi.SetActive(false);
                break;
            }

            if (rotateLerpSpeed > 0f && dir.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                float newZ = Mathf.LerpAngle(playerTr.eulerAngles.z, angle, rotateLerpSpeed * Time.deltaTime);
                playerTr.rotation = Quaternion.Euler(0f, 0f, newZ);
            }

            Vector2 step = dir.normalized * moveSpeed * Time.deltaTime;
            if (playerRb != null) playerRb.MovePosition(playerRb.position + step);
            else playerTr.position = (Vector2)playerTr.position + step;

            yield return null;
        }

        StopHoming();

        if (playerInside) StartRollLoop();
    }

    void StopHoming()
    {
        if (!homingActive) return;

        homingActive = false;

        if (homingRoutine != null)
        {
            StopCoroutine(homingRoutine);
            homingRoutine = null;
        }

        mashValue = 0f;
        PushMashUI();

        HideEffectBanner();
    }

    // ================== UI ==================
    void PushMashUI()
    {
        if (mashSlider != null)
            mashSlider.value = mashValue;
    }

    // ---------- Banner ----------
    private void ShowEffectBanner()
    {
        if (effectBanner == null) return;

        // Upewnij się, że rozmiar jest aktualny tuż przed animacją
        ApplyBannerSize(); // <<< nowość

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
            float s = 1f + Mathf.Sin(timeAcc * Mathf.PI * 2f * bannerPulseFrequency) * bannerPulseAmplitude; // bazowo 1f
            tr.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        tr.localScale = Vector3.one;
        bannerPulseRoutine = null;
    }

    // ================== Intuicyjny rozmiar baneru ==================
    void ApplyBannerSize()
    {
        if (effectBanner == null) return;

        float w = fixedSize.x;
        float h = fixedSize.y;

        switch (sizeMode)
        {
            case BannerSizeMode.FixedPixels:
                w = Mathf.Max(0f, fixedSize.x);
                h = Mathf.Max(0f, fixedSize.y);
                break;

            case BannerSizeMode.PercentOfParent:
                {
                    var parent = effectBanner.parent as RectTransform;
                    if (parent != null)
                    {
                        w = Mathf.Max(0f, parent.rect.width * widthPercent);
                        h = Mathf.Max(0f, parent.rect.height * heightPercent);
                    }
                    w += sizePadding.x;
                    h += sizePadding.y;
                }
                break;

            case BannerSizeMode.MatchTextPreferred:
                {
                    // Spróbuj TMP najpierw, potem klasyczny Text
                    Vector2 pref = Vector2.zero;

                    var tmp = effectBanner.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (tmp != null)
                    {
                        // duże limity, żeby uzyskać szerokość/wyokość dla aktualnego tekstu
                        Vector2 p = tmp.GetPreferredValues(tmp.text, 10000f, 10000f);
                        pref = p;
                    }
                    else
                    {
                        var uiText = effectBanner.GetComponentInChildren<Text>(true);
                        if (uiText != null)
                        {
                            // Text nie ma prostego GetPreferredValues; jeśli jest LayoutElement to użyjemy jego preferencji
                            var le = uiText.GetComponent<LayoutElement>();
                            if (le != null && (le.preferredWidth > 0 || le.preferredHeight > 0))
                                pref = new Vector2(le.preferredWidth, le.preferredHeight);
                            else
                                pref = new Vector2(400f, 80f); // sensowny fallback
                        }
                        else
                        {
                            pref = new Vector2(400f, 80f); // fallback, jeśli nie ma żadnego tekstu
                        }
                    }

                    w = Mathf.Max(0f, pref.x + sizePadding.x);
                    h = Mathf.Max(0f, pref.y + sizePadding.y);
                }
                break;
        }

        if (keepAspect && aspect > 0.0001f)
        {
            // dopasuj drugi wymiar do pierwszego wg aspect (W/H)
            // wybór bazowego wymiaru: dopasuj wysokość do szerokości
            h = w / aspect;
        }

        // Ustaw rozmiar respektując aktualne kotwice
        effectBanner.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        effectBanner.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
    }
}
