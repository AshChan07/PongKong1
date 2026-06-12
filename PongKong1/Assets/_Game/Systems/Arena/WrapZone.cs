using UnityEngine;

public class WrapZone : MonoBehaviour
{
    [Header("Wrap Settings")]
    [SerializeField] private bool isTop;
    [SerializeField] private float arenaHeight = 10f;

    [Header("Events")]
    [SerializeField] private Vector2GameEvent onBallWrapped; // Registered Event Channel per Plan Doc

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Ball"))
            return;

        if (other.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            if (isTop && rb.linearVelocity.y < 0) return;
            if (!isTop && rb.linearVelocity.y > 0) return;

            float targetY = isTop ? (-arenaHeight * 0.5f + 0.4f) : (arenaHeight * 0.5f - 0.4f);
            Vector2 wrappedPosition = new Vector2(rb.position.x, targetY);

            rb.position = wrappedPosition;

            onBallWrapped?.Raise(wrappedPosition);
        }
        else
        {
            // Scriptless/Fallback Transform positioning 
            float targetY = isTop ? (-arenaHeight * 0.5f + 0.4f) : (arenaHeight * 0.5f - 0.4f);
            Vector2 wrappedPosition = new Vector2(other.transform.position.x, targetY);
            other.transform.position = wrappedPosition;
            
            onBallWrapped?.Raise(wrappedPosition);
        }
    }
}
