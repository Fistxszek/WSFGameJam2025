using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class RandomSpawner2D : MonoBehaviour
{
    [Header("Obszar spawnowania (BoxCollider2D jako Trigger)")]
    public BoxCollider2D spawnArea;

    [Header("Kogo obsługiwać")]
    public string requiredTag = ""; // puste = każdy

    [Header("Prefab do zespawnowania")]
    public GameObject prefabToSpawn;

    [Header("Ile sztuk na jedno wejście")]
    [Min(1)] public int spawnCountOnEnter = 1;

    [Header("Organizacja")]
    public bool parentSpawnedToThis = true;

    [Header("Stan (read-only)")]
    public GameObject lastSpawned;

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
        // Sprzątanie wszystkich zespawnowanych
        foreach (var kv in _spawnedByCollider)
        {
            var list = kv.Value;
            if (list == null) continue;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) Destroy(list[i]);
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
        if (!PassesFilter(other)) return;
        if (!_spawnedByCollider.TryGetValue(other, out var list))
        {
            list = new List<GameObject>(spawnCountOnEnter);
            _spawnedByCollider.Add(other, list);
        }

        for (int i = 0; i < spawnCountOnEnter; i++)
        {
            var go = SpawnOne(null, parentSpawnedToThis);
            if (go != null) list.Add(go);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!PassesFilter(other)) return;

        if (_spawnedByCollider.TryGetValue(other, out var list))
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) Destroy(list[i]);

            _spawnedByCollider.Remove(other);
        }
    }

    // ─────────────── NEW: SpawnOne ───────────────
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
    // ─────────────────────────────────────────────

    public Vector2 GetRandomPointInArea()
    {
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
