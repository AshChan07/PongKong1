using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

public class LedgeController : MonoBehaviour, ILedgeIdentifier
{
    [Header("Identity")]
    [SerializeField] protected PlayerSide side;

    [Header("Movement")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float speedScalingFactor = 0.15f;
    [SerializeField] private float maxSpeed = 24f;
    [SerializeField] private float yBoundary = 4.5f;

    [Header("Life")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private float shortenPercent = 0.1f;

    [Header("Juice & Stretch")]
    [SerializeField] private float stretchFactor = 0.03f;
    [SerializeField] private float stretchSmoothness = 0.15f;

    [Header("Events")]
    [SerializeField] private ScoreStateGameEvent onScoreStateUpdated;
    [SerializeField] private LedgeLifeChangedGameEvent onLedgeLifeChangedDetailed;
    [SerializeField] private LedgeDashGameEvent onLedgeDashRequested;
    [SerializeField] private LedgeDamageGameEvent onLedgeDamageRequested;
    [SerializeField] private GameEvent onMatchRestarted;

    private Rigidbody2D rb;
    private PhotonView photonView;
    private float movementInput;
    private int currentLives;
    private Vector3 originalScale;
    private Vector3 targetScale;
    private bool isDashing;
    private float dashTargetY;
    private float dashSpeed;
    private int totalMatchScore;

    public PlayerSide Side => side;
    public int CurrentLives => currentLives;
    public bool IsBroken => currentLives <= 0;

    private float CurrentSpeed
    {
        get
        {
            return Mathf.Min(speed + totalMatchScore * speedScalingFactor, maxSpeed);
        }
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        photonView = GetComponent<PhotonView>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
        currentLives = maxLives;

        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    private void OnEnable()
    {
        if (onScoreStateUpdated != null)
            onScoreStateUpdated.OnRaised += HandleScoreState;
        if (onLedgeDashRequested != null)
            onLedgeDashRequested.OnRaised += HandleLedgeDashRequested;
        if (onLedgeDamageRequested != null)
            onLedgeDamageRequested.OnRaised += HandleLedgeDamageRequested;
        if (onLedgeLifeChangedDetailed != null)
            onLedgeLifeChangedDetailed.OnRaised += HandleLedgeLifeChanged;
        if (onMatchRestarted != null)
            onMatchRestarted.OnRaised += HandleMatchRestarted;
    }

    private void OnDisable()
    {
        if (onScoreStateUpdated != null)
            onScoreStateUpdated.OnRaised -= HandleScoreState;
        if (onLedgeDashRequested != null)
            onLedgeDashRequested.OnRaised -= HandleLedgeDashRequested;
        if (onLedgeDamageRequested != null)
            onLedgeDamageRequested.OnRaised -= HandleLedgeDamageRequested;
        if (onLedgeLifeChangedDetailed != null)
            onLedgeLifeChangedDetailed.OnRaised -= HandleLedgeLifeChanged;
        if (onMatchRestarted != null)
            onMatchRestarted.OnRaised -= HandleMatchRestarted;
    }

    private void HandleScoreState(ScoreStateData data)
    {
        totalMatchScore = data.totalMatchScore;
    }

    private void HandleLedgeDashRequested(LedgeDashData data)
    {
        if (data.side != side) return;
        if (!IsLocallyOwned()) return; // Remote ledge: NetworkLedgeSync interpolates
        DashToY(data.targetY, data.dashSpeedMultiplier);
    }

    private void HandleLedgeDamageRequested(LedgeDamageData data)
    {
        if (data.side != side) return;
        if (!IsLocallyOwned()) return; // Remote ledge: NetworkScoreRelay sends RPC to owner
        TakeDamage(data.damage);
    }

    private bool IsLocallyOwned()
    {
        if (NetworkContext.Role == NetworkRole.Offline) return true;
        return photonView == null || photonView.IsMine;
    }

    private void HandleLedgeLifeChanged(LedgeLifeChangedData data)
    {
        if (data.side != side) return;
        SetLives(data.currentLives);
    }

    private void HandleMatchRestarted()
    {
        ResetLives();
    }

    private void Update()
    {
        if (!isDashing)
            movementInput = ReadInput();
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            ProcessDash();
        }
        else
        {
            float targetY = movementInput * CurrentSpeed;
            float smoothedY = Mathf.Lerp(rb.linearVelocity.y, targetY, 0.15f);
            rb.linearVelocity = new Vector2(0f, smoothedY);
        }

        float clampedY = Mathf.Clamp(rb.position.y, -yBoundary, yBoundary);
        if (!Mathf.Approximately(rb.position.y, clampedY))
            rb.MovePosition(new Vector2(rb.position.x, clampedY));

        CalculateStretch();
    }

    private void ProcessDash()
    {
        float diff = dashTargetY - rb.position.y;
        float step = dashSpeed * Time.fixedDeltaTime;

        if (Mathf.Abs(diff) <= step)
        {
            rb.MovePosition(new Vector2(rb.position.x, dashTargetY));
            rb.linearVelocity = Vector2.zero;
            isDashing = false;
            return;
        }

        rb.linearVelocity = new Vector2(0f, Mathf.Sign(diff) * dashSpeed);
    }

    public void DashToY(float targetY, float dashSpeedMultiplier)
    {
        float maxDashDistance = yBoundary * 2f * 0.35f;
        float currentY = transform.position.y;
        float clampedTarget = Mathf.Clamp(targetY, -yBoundary, yBoundary);
        float distance = Mathf.Abs(clampedTarget - currentY);

        if (distance > maxDashDistance)
            clampedTarget = currentY + Mathf.Sign(clampedTarget - currentY) * maxDashDistance;

        dashTargetY = clampedTarget;
        dashSpeed = CurrentSpeed * dashSpeedMultiplier;
        isDashing = true;
    }

    private void CalculateStretch()
    {
        float currentVelocityY = Mathf.Abs(rb.linearVelocity.y);

        if (currentVelocityY > 0.01f)
        {
            float dynamicStretchY = originalScale.y * (1f + (currentVelocityY * stretchFactor));
            targetScale = new Vector3(originalScale.x, dynamicStretchY, originalScale.z);
        }
        else
        {
            targetScale = GetBaseScale();
        }

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, stretchSmoothness);
    }

    private Vector3 GetBaseScale()
    {
        if (currentLives <= 1)
        {
            return new Vector3(
                originalScale.x,
                originalScale.y * (1f - shortenPercent),
                originalScale.z);
        }
        return originalScale;
    }

    private float ReadInput()
    {
        // Online: only the owning client reads input for its ledge
        if (NetworkContext.Role != NetworkRole.Offline && !IsLocallyOwned())
            return 0f;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (NetworkContext.Role != NetworkRole.Offline)
            {
                // Online: local player always uses W/S + arrows regardless of side
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) return 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) return -1f;
            }
            else
            {
                if (side == PlayerSide.P1)
                {
                    if (keyboard.wKey.isPressed) return 1f;
                    if (keyboard.sKey.isPressed) return -1f;
                }
                else
                {
                    if (keyboard.upArrowKey.isPressed) return 1f;
                    if (keyboard.downArrowKey.isPressed) return -1f;
                }
            }
        }

        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            float stickY = gamepad.leftStick.y.value;
            if (Mathf.Abs(stickY) > 0.1f)
                return stickY;
        }

        return 0f;
    }

    /// <summary>
    /// Set lives directly from network relay without firing the life-changed event
    /// (the relay re-raises it separately for the HUD).
    /// </summary>
    public void SetLives(int lives)
    {
        currentLives = Mathf.Clamp(lives, 0, maxLives);
        UpdateScaleForLife();
    }

    public void TakeDamage(int damage)
    {
        if (IsBroken)
            return;

        currentLives = Mathf.Max(0, currentLives - damage);
        
        if (onLedgeLifeChangedDetailed != null)
        {
            var data = new LedgeLifeChangedData
            {
                side = side,
                currentLives = currentLives,
                maxLives = maxLives,
                isBroken = IsBroken
            };
            onLedgeLifeChangedDetailed.Raise(data);
        }

        UpdateScaleForLife();
    }

    public void ResetLives()
    {
        currentLives = maxLives;
        
        if (onLedgeLifeChangedDetailed != null)
        {
            var data = new LedgeLifeChangedData
            {
                side = side,
                currentLives = currentLives,
                maxLives = maxLives,
                isBroken = IsBroken
            };
            onLedgeLifeChangedDetailed.Raise(data);
        }

        transform.localScale = originalScale;
        targetScale = originalScale;
    }

    private void UpdateScaleForLife()
    {
        // Apply unconditionally so a life-correction back to >=2 un-shortens instead of staying stuck.
        Vector3 scale = GetBaseScale();
        transform.localScale = scale;
        targetScale = scale;
    }
}
