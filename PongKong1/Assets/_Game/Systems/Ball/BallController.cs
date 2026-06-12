using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BallController : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float baseSpeed = 6f;
    [SerializeField] private float speedScalingFactor = 0.15f;
    [SerializeField] private float maxSpeed = 22f;

    [Header("Arena")]
    [SerializeField] private float arenaHeight = 10f;
    [SerializeField] private float arenaWidth = 18f;

    [Header("Bounce")]
    [SerializeField] private float deflectionMultiplier = 1.5f;

    [Header("Respawn")]
    [SerializeField] private float respawnDelay = 1.5f;

    [Header("Events")]
    [SerializeField] private Vector2GameEvent onBallLaunched;
    [SerializeField] private ContactDataGameEvent onBallHitLedge;
    [SerializeField] private IntGameEvent onScoreChanged;
    [SerializeField] private PlayerSideGameEvent onPointScored;

    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;

    private Rigidbody2D rb;
    private float currentSpeed;
    private bool hasPendingBounce;
    private Vector2 pendingNormal;
    private float pendingHitOffset;
    private PlayerSide pendingLedgeSide;
    private bool isRespawning;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentSpeed = baseSpeed;

        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();
    }

    private void OnEnable()
    {
        if (onScoreChanged != null)
            onScoreChanged.OnRaised += HandleScoreChanged;
    }

    private void OnDisable()
    {
        if (onScoreChanged != null)
            onScoreChanged.OnRaised -= HandleScoreChanged;
    }

    private void Start()
    {
        Launch();
    }

    private void HandleScoreChanged(int totalScore)
    {
        currentSpeed = Mathf.Min(baseSpeed + totalScore * speedScalingFactor, maxSpeed);
    }

    private void FixedUpdate()
    {
        if (hasPendingBounce)
        {
            ApplyCustomBounce();
            hasPendingBounce = false;
        }

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * currentSpeed;
        }

        float halfHeight = arenaHeight * 0.5f;
        Vector2 pos = rb.position;
        Vector2 vel = rb.linearVelocity;

        if (pos.y > halfHeight)
        {
            pos.y = halfHeight;
            vel.y = -Mathf.Abs(vel.y);
        }
        else if (pos.y < -halfHeight)
        {
            pos.y = -halfHeight;
            vel.y = Mathf.Abs(vel.y);
        }

        rb.position = pos;
        rb.linearVelocity = vel;

        float halfWidth = arenaWidth * 0.5f;
        if (!isRespawning && Mathf.Abs(pos.x) > halfWidth)
        {
            PlayerSide scorer = pos.x > 0f ? PlayerSide.P1 : PlayerSide.P2;
            RecordScore(scorer);
            ResetAndRespawn();
        }
    }

    public void Launch()
    {
        float xDir = Random.value >= 0.5f ? 1f : -1f;
        float yDir = Random.Range(-1f, 1f);
        Vector2 direction = new Vector2(xDir, yDir).normalized;
        rb.linearVelocity = direction * currentSpeed;
        onBallLaunched?.Raise(direction);
    }

    public void ResetAndRespawn()
    {
        StartCoroutine(RespawnSequence());
    }

    private IEnumerator RespawnSequence()
    {
        isRespawning = true;
        rb.linearVelocity = Vector2.zero;
        transform.position = Vector2.zero;
        rb.simulated = false;
        yield return new WaitForSeconds(respawnDelay);
        rb.simulated = true;
        currentSpeed = baseSpeed;
        isRespawning = false;
        Launch();
    }

    private void RecordScore(PlayerSide scorer)
    {
        if (onPointScored != null)
            onPointScored.Raise(scorer);
        else if (scoreManager != null)
            scoreManager.RecordScore(scorer);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Ledge"))
            return;

        LedgeController ledge = collision.gameObject.GetComponent<LedgeController>();
        if (ledge == null)
            return;

        pendingNormal = collision.GetContact(0).normal;

        float ledgeHeight = collision.collider.bounds.size.y;
        float contactY = collision.GetContact(0).point.y;
        float ledgeCenterY = collision.collider.bounds.center.y;
        pendingHitOffset = (contactY - ledgeCenterY) / (ledgeHeight * 0.5f);
        pendingHitOffset = Mathf.Clamp(pendingHitOffset, -1f, 1f);
        pendingLedgeSide = ledge.Side;
        hasPendingBounce = true;
    }

    private void ApplyCustomBounce()
    {
        float xDir = -Mathf.Sign(pendingNormal.x);
        if (Mathf.Approximately(pendingNormal.x, 0f))
            xDir = 1f;

        float outAngle = pendingHitOffset * deflectionMultiplier;
        if (xDir < 0f)
            outAngle = Mathf.PI - outAngle;

        Vector2 outDirection = new Vector2(Mathf.Cos(outAngle), Mathf.Sin(outAngle));
        rb.linearVelocity = outDirection * currentSpeed;

        var data = new ContactData
        {
            incidentDirection = -outDirection,
            contactPoint = Vector2.zero,
            hitOffset = pendingHitOffset,
            ledgeSide = pendingLedgeSide
        };
        onBallHitLedge?.Raise(data);
    }
}
