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
    [SerializeField] private PlayerSideGameEvent onPointScored;
    [SerializeField] private PlayerSideGameEvent onBallEnteredCourt;
    [SerializeField] private PlayerSideGameEvent onBallExitedCourt;

    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Rigidbody2D rb;
    private float speedMultiplier = 1f;
    private float flatSpeedBonus;
    private bool hasPendingBounce;
    private Vector2 pendingNormal;
    private float pendingHitOffset;
    private PlayerSide pendingLedgeSide;
    private bool isRespawning;
    private bool matchOver;
    private PlayerSide currentCourt;
    private bool courtAssigned;
    private bool isDestroyerActive;
    private bool isVanishActive;
    private bool isVisible = true;

    public bool IsDestroyerActive => isDestroyerActive;
    public bool IsVanishActive => isVanishActive;
    public bool IsVisible => isVisible;
    public PlayerSide CurrentCourt => currentCourt;
    public bool HasCourt => courtAssigned;
    public bool IsRespawning => isRespawning;

    private float FormulaSpeed
    {
        get
        {
            int total = scoreManager != null ? scoreManager.TotalMatchScore : 0;
            return Mathf.Min(baseSpeed + total * speedScalingFactor, maxSpeed);
        }
    }

    private float EffectiveSpeed => Mathf.Min((FormulaSpeed + flatSpeedBonus) * speedMultiplier, maxSpeed);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();
    }

    private void Start()
    {
        Launch();
    }

    private void FixedUpdate()
    {
        if (isRespawning || matchOver)
            return;

        if (hasPendingBounce)
        {
            ApplyCustomBounce();
            hasPendingBounce = false;
        }

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            rb.linearVelocity = rb.linearVelocity.normalized * EffectiveSpeed;

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

        TrackCourt(pos);

        float halfWidth = arenaWidth * 0.5f;
        if (Mathf.Abs(pos.x) > halfWidth)
        {
            if (!isVisible)
            {
                vel.x = -vel.x;
                pos.x = Mathf.Clamp(pos.x, -halfWidth, halfWidth);
                rb.position = pos;
                rb.linearVelocity = vel;
                return;
            }

            PlayerSide scorer = pos.x > 0f ? PlayerSide.P1 : PlayerSide.P2;
            RecordScore(scorer);

            if (scoreManager != null && scoreManager.IsGameOver)
                StopForMatchEnd();
            else
                ResetAndRespawn();
        }
    }

    private void StopForMatchEnd()
    {
        matchOver = true;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        rb.position = Vector2.zero;
        transform.position = Vector2.zero;
        ClearBallState();
    }

    public void Restart()
    {
        StopAllCoroutines();
        matchOver = false;
        isRespawning = false;
        courtAssigned = false;
        rb.simulated = true;
        rb.position = Vector2.zero;
        transform.position = Vector2.zero;
        ClearBallState();
        Launch();
    }

    private void TrackCourt(Vector2 pos)
    {
        PlayerSide newCourt = pos.x < 0f ? PlayerSide.P1 : PlayerSide.P2;

        if (!courtAssigned)
        {
            currentCourt = newCourt;
            courtAssigned = true;
            onBallEnteredCourt?.Raise(newCourt);
            return;
        }

        if (newCourt != currentCourt)
        {
            onBallExitedCourt?.Raise(currentCourt);
            currentCourt = newCourt;
            onBallEnteredCourt?.Raise(newCourt);
        }
    }

    public void Launch()
    {
        float xDir = Random.value >= 0.5f ? 1f : -1f;
        float yDir = Random.Range(-1f, 1f);
        Vector2 direction = new Vector2(xDir, yDir).normalized;
        rb.linearVelocity = direction * FormulaSpeed;
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

        ClearBallState();

        yield return new WaitForSeconds(respawnDelay);

        rb.simulated = true;
        courtAssigned = false;
        isRespawning = false;
        Launch();
    }

    private void ClearBallState()
    {
        speedMultiplier = 1f;
        flatSpeedBonus = 0f;
        isDestroyerActive = false;
        isVanishActive = false;
        isVisible = true;
        SetVisible(true);
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
        rb.linearVelocity = outDirection * EffectiveSpeed;

        var data = new ContactData
        {
            incidentDirection = -outDirection,
            contactPoint = Vector2.zero,
            hitOffset = pendingHitOffset,
            ledgeSide = pendingLedgeSide
        };
        onBallHitLedge?.Raise(data);
    }

    public void ApplySpeedMultiplier(float multiplier)
    {
        speedMultiplier *= multiplier;
    }

    public void AddFlatSpeed(float amount)
    {
        flatSpeedBonus += amount;
    }

    public void SetDestroyerActive(bool active)
    {
        isDestroyerActive = active;
    }

    public void SetVanishActive(bool active)
    {
        isVanishActive = active;
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (spriteRenderer != null)
            spriteRenderer.enabled = visible;
    }

    public Vector2 GetPosition()
    {
        return rb.position;
    }

    public Vector2 GetVelocity()
    {
        return rb.linearVelocity;
    }
}
