using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class RandomSpawner2D : MonoBehaviour
{
    [Header("Obszar spawnowania (BoxCollider2D jako Trigger)")]
    public BoxCollider2D spawnArea;

    [Header("Kogo obsługiwać (puste = każdy)")]
    public string requiredTag = "";

    [Header("Prefab do zespawnowania")]
    public GameObject prefabToSpawn;

    [Header("Ile sztuk na jedno wejście")]
    [Min(1)] public int spawnCountOnEnter = 1;

    [Header("Organizacja")]
    public bool parentSpawnedToThis = true;

    [Header("Sterowanie trybem")]
    public bool spawnOnTriggerEnter = true;   // <— NOWE: pozwala wyłączyć automatyczne spawnowanie z triggera

    [Header("Stan (read-only)")]
    public GameObject lastSpawned;

    // Kto w strefie → co mu zespawnowaliśmy (żeby po wyjściu usunąć dokładnie te obiekty)
    private readonly Dictionary<Collider2D, List<GameObject>> _spawnedByCollider = new();

    private void Reset()
    {
        spawnArea = GetComponent<BoxCollider2D>();
        if (spawnArea) spawnArea.isTrigger = true;
    }

    private void Awake()
    {
        if (spawnArea == null)
            spawnArea = GetComponent<BoxCollider2D>();

        if (spawnArea != null && !spawnArea.isTrigger)
            Debug.LogWarning("[RandomSpawner2D] BoxCollider2D powinien mieć zaznaczone Is Trigger.");
    }

    private void OnDisable()
    {
        // porządek: gdy skrypt/obiekt jest wyłączany, usuń wszystko co zespawnowano
        foreach (var kv in _spawnedByCollider)
        {
            var list = kv.Value;
            if (list == null) continue;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) Destroy(list[i]);
            }
        }
        _spawnedByCollider.Clear();
    }

    private bool PassesFilter(Collider2D other)
    {
        if (string.IsNullOrEmpty(requiredTag)) return true;
        return other.CompareTag(requiredTag);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!spawnOnTriggerEnter) return;                    // <— NOWE: można wyłączyć
        if (!PassesFilter(other)) return;
        if (prefabToSpawn == null || spawnArea == null)
        {
            Debug.LogWarning("[RandomSpawner2D] Brakuje prefabu lub BoxCollider2D!");
            return;
        }

        if (!_spawnedByCollider.TryGetValue(other, out var list))
        {
            list = new List<GameObject>(spawnCountOnEnter);
            _spawnedByCollider.Add(other, list);
        }

        for (int i = 0; i < spawnCountOnEnter; i++)
        {
            var go = SpawnOne(null, parentSpawnedToThis);
            if (go != null) list.Add(go);
            if (i == 0) lastSpawned = go;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!PassesFilter(other)) return;

        if (_spawnedByCollider.TryGetValue(other, out var list))
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) Destroy(list[i]);
            }
            _spawnedByCollider.Remove(other);
        }
    }

    // ─────────────── API ───────────────

    // RĘCZNY respawn z eventu (np. ZoneEffectSimple.OnRespawnRequested)
    public void SpawnObjects()                          // <— NOWE: prosty hook bez parametrów
    {
        if (prefabToSpawn == null || spawnArea == null)
        {
            Debug.LogWarning("[RandomSpawner2D] SpawnObjects: brak prefabu lub spawnArea.");
            return;
        }

        // Ile sztuk? Użyjemy spawnCountOnEnter jako „domyślnej ilości”.
        for (int i = 0; i < Mathf.Max(1, spawnCountOnEnter); i++)
            SpawnOne(null, parentSpawnedToThis);
    }

    // Wersja z parametrem (gdybyś chciał wywoływać przez code): spawnuje 'count' sztuk
    public void SpawnObjectsCount(int count)            // <— opcjonalne, wygodne z kodu
    {
        if (prefabToSpawn == null || spawnArea == null)
        {
            Debug.LogWarning("[RandomSpawner2D] SpawnObjectsCount: brak prefabu lub spawnArea.");
            return;
        }

        int n = Mathf.Max(1, count);
        for (int i = 0; i < n; i++)
            SpawnOne(null, parentSpawnedToThis);
    }

    public GameObject SpawnOne(GameObject overridePrefab = null, bool parentToThis = true)
    {
        if (spawnArea == null)
        {
            Debug.LogWarning("[RandomSpawner2D] Brak BoxCollider2D (spawnArea).");
            return null;
        }

        var prefab = overridePrefab != null ? overridePrefab : prefabToSpawn;
        if (prefab == null)
        {
            Debug.LogWarning("[RandomSpawner2D] Brak przypisanego prefabu.");
            return null;
        }

        Vector2 pos = GetRandomPointInArea();
        var go = Instantiate(prefab, pos, Quaternion.identity);
        if (parentToThis) go.transform.SetParent(transform, true);

        lastSpawned = go;
        return go;
    }

    public Vector2 GetRandomPointInArea()
    {
        // world-bounds collidera
        Vector2 c = spawnArea.bounds.center;
        Vector2 s = spawnArea.bounds.size;
        float x = Random.Range(c.x - s.x * 0.5f, c.x + s.x * 0.5f);
        float y = Random.Range(c.y - s.y * 0.5f, c.y + s.y * 0.5f);
        return new Vector2(x, y);
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnArea != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawCube(spawnArea.bounds.center, spawnArea.bounds.size);
        }
    }
}
