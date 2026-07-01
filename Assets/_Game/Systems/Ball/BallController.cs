using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BallController : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float baseSpeed = 6f;
    [SerializeField] private float speedScalingFactor = 0.15f;
    [SerializeField] private float maxSpeed = 22f;
    [SerializeField] private float speedRampPerSecond = 4f;

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
    [SerializeField] private ScoreStateGameEvent onScoreStateUpdated;
    [SerializeField] private PlayerSideGameEvent onMatchEnd;
    [SerializeField] private FloatGameEvent onBallSpeedMultiplierRequested;
    [SerializeField] private FloatGameEvent onBallFlatSpeedRequested;
    [SerializeField] private BoolGameEvent onBallDestroyerStateChanged;
    [SerializeField] private BoolGameEvent onBallVanishStateChanged;
    [SerializeField] private BoolGameEvent onBallVisibilityChanged;
    [SerializeField] private GameEvent onMatchRestarted;
    [SerializeField] private Vector2GameEvent onBallPositionUpdated;
    [SerializeField] private GameEvent onBallHitBoundary;
    [SerializeField] private BoolGameEvent onBallFreezeRequested;
    [SerializeField] private NetworkBallStateGameEvent onNetworkHostTakeoverRequested;


    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Rigidbody2D rb;
    private float speedMultiplier = 1f;
    private float flatSpeedBonus;
    private bool hasPendingBounce;
    private Vector2 pendingNormal;
    private Vector2 pendingIncident;
    private Vector2 pendingContactPoint;
    private float pendingHitOffset;
    private PlayerSide pendingLedgeSide;
    private bool isRespawning;
    private bool matchOver;
    private PlayerSide currentCourt;
    private bool courtAssigned;
    private bool isDestroyerActive;
    private bool isVanishActive;
    private bool isVisible = true;
    private int totalMatchPoints;
    private float rallySpeed;

    private bool isRemote;

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
            return Mathf.Min(baseSpeed + totalMatchPoints * speedScalingFactor, maxSpeed);
        }
    }

    private float EffectiveSpeed => Mathf.Min((rallySpeed + flatSpeedBonus) * speedMultiplier, maxSpeed);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        isRemote = NetworkContext.Role == NetworkRole.Client;
        if (isRemote)
        {
            rb.simulated = false;
            return;
        }
        Launch();
    }

    private void OnEnable()
    {
        if (onScoreStateUpdated != null)
            onScoreStateUpdated.OnRaised += HandleScoreState;
        if (onMatchEnd != null)
            onMatchEnd.OnRaised += HandleMatchEnd;
        if (onBallSpeedMultiplierRequested != null)
            onBallSpeedMultiplierRequested.OnRaised += ApplySpeedMultiplier;
        if (onBallFlatSpeedRequested != null)
            onBallFlatSpeedRequested.OnRaised += AddFlatSpeed;
        if (onBallDestroyerStateChanged != null)
            onBallDestroyerStateChanged.OnRaised += SetDestroyerActive;
        if (onBallVanishStateChanged != null)
            onBallVanishStateChanged.OnRaised += SetVanishActive;
        if (onBallVisibilityChanged != null)
            onBallVisibilityChanged.OnRaised += SetVisible;
        if (onMatchRestarted != null)
            onMatchRestarted.OnRaised += Restart;
        if (onBallFreezeRequested != null)
            onBallFreezeRequested.OnRaised += HandleBallFreezeRequested;
        if (onNetworkHostTakeoverRequested != null)
            onNetworkHostTakeoverRequested.OnRaised += HandleNetworkHostTakeoverRequested;
    }

    private void OnDisable()
    {
        if (onScoreStateUpdated != null)
            onScoreStateUpdated.OnRaised -= HandleScoreState;
        if (onMatchEnd != null)
            onMatchEnd.OnRaised -= HandleMatchEnd;
        if (onBallSpeedMultiplierRequested != null)
            onBallSpeedMultiplierRequested.OnRaised -= ApplySpeedMultiplier;
        if (onBallFlatSpeedRequested != null)
            onBallFlatSpeedRequested.OnRaised -= AddFlatSpeed;
        if (onBallDestroyerStateChanged != null)
            onBallDestroyerStateChanged.OnRaised -= SetDestroyerActive;
        if (onBallVanishStateChanged != null)
            onBallVanishStateChanged.OnRaised -= SetVanishActive;
        if (onBallVisibilityChanged != null)
            onBallVisibilityChanged.OnRaised -= SetVisible;
        if (onMatchRestarted != null)
            onMatchRestarted.OnRaised -= Restart;
        if (onBallFreezeRequested != null)
            onBallFreezeRequested.OnRaised -= HandleBallFreezeRequested;
        if (onNetworkHostTakeoverRequested != null)
            onNetworkHostTakeoverRequested.OnRaised -= HandleNetworkHostTakeoverRequested;
    }

    private void HandleScoreState(ScoreStateData data)
    {
        totalMatchPoints = data.totalMatchScore;
    }

    private void HandleMatchEnd(PlayerSide winner)
    {
        StopForMatchEnd();
    }

    private void FixedUpdate()
    {
        if (isRemote || isRespawning || matchOver)
            return;

        if (hasPendingBounce)
        {
            ApplyCustomBounce();
            hasPendingBounce = false;
        }

        rallySpeed = Mathf.MoveTowards(rallySpeed, FormulaSpeed, speedRampPerSecond * Time.fixedDeltaTime);

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            rb.linearVelocity = rb.linearVelocity.normalized * EffectiveSpeed;

        float halfHeight = arenaHeight * 0.5f;
        Vector2 pos = rb.position;
        Vector2 vel = rb.linearVelocity;

        if (pos.y > halfHeight)
        {
            pos.y = halfHeight;
            vel.y = -Mathf.Abs(vel.y);
            onBallHitBoundary?.Raise();
        }
        else if (pos.y < -halfHeight)
        {
            pos.y = -halfHeight;
            vel.y = Mathf.Abs(vel.y);
            onBallHitBoundary?.Raise();
        }

        rb.position = pos;
        rb.linearVelocity = vel;

        TrackCourt(pos);

        onBallPositionUpdated?.Raise(pos);

        float halfWidth = arenaWidth * 0.5f;
        if (Mathf.Abs(pos.x) > halfWidth)
        {
            PlayerSide scorer = pos.x > 0f ? PlayerSide.P1 : PlayerSide.P2;
            RecordScore(scorer);

            if (!matchOver)
                ResetAndRespawn();
        }
    }

    private void StopForMatchEnd()
    {
        StopAllCoroutines();
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
        rb.position = Vector2.zero;
        transform.position = Vector2.zero;

        if (isRemote) return;

        ClearBallState();
        rb.simulated = true;
        Launch();
    }

    public void TakeOverSimulation(Vector2 lastPos, Vector2 lastVel)
    {
        isRemote = false;
        rb.simulated = true;
        if (!isRespawning && !matchOver)
        {
            rb.position = lastPos;
            rb.linearVelocity = lastVel;
            onBallLaunched?.Raise(lastVel.sqrMagnitude > 0.001f ? lastVel.normalized : Vector2.right);
        }
    }

    public void FreezeForDisconnect()
    {
        rb.simulated = false;
    }

    public void UnfreezeAfterReconnect()
    {
        if (!matchOver && !isRespawning)
            rb.simulated = true;
    }

    private void HandleBallFreezeRequested(bool freeze)
    {
        if (isRemote) return;
        if (freeze)
            FreezeForDisconnect();
        else
            UnfreezeAfterReconnect();
    }

    private void HandleNetworkHostTakeoverRequested(NetworkBallStateData state)
    {
        TakeOverSimulation(state.position, state.velocity);
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
        rallySpeed = baseSpeed;
        rb.linearVelocity = direction * EffectiveSpeed;
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
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Ledge"))
            return;

        ILedgeIdentifier ledge = collision.gameObject.GetComponent<ILedgeIdentifier>();
        if (ledge == null)
            return;

        ContactPoint2D contact = collision.GetContact(0);
        pendingNormal = contact.normal;
        pendingContactPoint = contact.point;
        pendingIncident = rb.linearVelocity;

        float ledgeHeight = collision.collider.bounds.size.y;
        float ledgeCenterY = collision.collider.bounds.center.y;
        pendingHitOffset = (contact.point.y - ledgeCenterY) / (ledgeHeight * 0.5f);
        pendingHitOffset = Mathf.Clamp(pendingHitOffset, -1f, 1f);
        pendingLedgeSide = ledge.Side;
        hasPendingBounce = true;
    }

    private void ApplyCustomBounce()
    {
        float xSign = Mathf.Approximately(pendingNormal.x, 0f)
            ? -Mathf.Sign(pendingIncident.x)
            : Mathf.Sign(pendingNormal.x);

        Vector2 incidentDir = pendingIncident.sqrMagnitude > 0.0001f
            ? pendingIncident.normalized
            : new Vector2(-xSign, 0f);
        float outX = Mathf.Abs(incidentDir.x) * xSign;
        float outY = incidentDir.y + pendingHitOffset * deflectionMultiplier;
        // Floor horizontal so a near-vertical mirror can't lock the ball bouncing
        // top/bottom in one court without ever crossing to score.
        const float minHorizontal = 0.35f;
        if (Mathf.Abs(outX) < minHorizontal)
            outX = minHorizontal * xSign;
        Vector2 outDirection = new Vector2(outX, outY).normalized;
        rb.linearVelocity = outDirection * EffectiveSpeed;

        var data = new ContactData
        {
            incidentDirection = pendingIncident.sqrMagnitude > 0.0001f ? pendingIncident.normalized : -outDirection,
            contactPoint = pendingContactPoint,
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
