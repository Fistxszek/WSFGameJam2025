using UnityEngine;

public class MoveInsideCollider2DVertical : MonoBehaviour
{
    [Header("Ustawienia ruchu")]
    public Collider2D movementBounds; // collider, w którym porusza siê obiekt
    public float speed = 2f;          // prêdkoœæ poruszania
    public bool startMovingUp = true; // czy zaczyna poruszaæ siê w górê

    private float minY;
    private float maxY;
    private int direction; // 1 = w górê, -1 = w dó³

    private void Start()
    {
        if (movementBounds == null)
        {
            Debug.LogError("Brak przypisanego collidera do movementBounds!");
            enabled = false;
            return;
        }

        // Pobierz granice z BoxCollidera2D
        Bounds bounds = movementBounds.bounds;
        minY = bounds.min.y;
        maxY = bounds.max.y;

        // Ustaw pocz¹tkowy kierunek
        direction = startMovingUp ? 1 : -1;
    }

    private void Update()
    {
        transform.localScale = new Vector3(direction, 1, 1);
        // Ruch pionowy
        transform.Translate(Vector2.up * direction * speed * Time.deltaTime);

        // Ograniczenie i odbicie
        if (transform.position.y < minY)
        {
            transform.position = new Vector2(transform.position.x, minY);
            direction = 1; // zmiana kierunku w górê
        }
        else if (transform.position.y > maxY)
        {
            transform.position = new Vector2(transform.position.x, maxY);
            direction = -1; // zmiana kierunku w dó³
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (movementBounds != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(movementBounds.bounds.center, movementBounds.bounds.size);
        }
    }
}
