using UnityEngine;

public class MoveInsideCollider2D : MonoBehaviour
{
    [Header("Ustawienia ruchu")]
    public Collider2D movementBounds; // collider, wewn¹trz którego obiekt siê porusza
    public float speed = 2f;          // prêdkoœæ poruszania
    public bool startMovingRight = true;

    private float minX;
    private float maxX;
    private int direction; // 1 = prawo, -1 = lewo

    private void Start()
    {
        if (movementBounds == null)
        {
            Debug.LogError("Brak przypisanego collidera do movementBounds!");
            enabled = false;
            return;
        }

        // Wyznacz granice X na podstawie colliddera
        Bounds bounds = movementBounds.bounds;
        minX = bounds.min.x;
        maxX = bounds.max.x;

        // Ustaw kierunek pocz¹tkowy
        direction = startMovingRight ? 1 : -1;
    }

    private void Update()
    {
        // Ruch poziomy
        transform.Translate(Vector2.right * direction * speed * Time.deltaTime);

        // Ogranicz w zakresie collidera
        if (transform.position.x < minX)
        {
            transform.position = new Vector2(minX, transform.position.y);
            direction = 1; // zmiana kierunku w prawo
        }
        else if (transform.position.x > maxX)
        {
            transform.position = new Vector2(maxX, transform.position.y);
            direction = -1; // zmiana kierunku w lewo
        }
        transform.localScale = new Vector3(direction, 1, 1);

    }

    private void OnDrawGizmosSelected()
    {
        if (movementBounds != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(movementBounds.bounds.center, movementBounds.bounds.size);
        }
    }
}
