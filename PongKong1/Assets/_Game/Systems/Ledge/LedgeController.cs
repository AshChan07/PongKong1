using UnityEngine;
using UnityEngine.InputSystem;

public class LedgeController : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] protected PlayerSide side;

    [Header("Movement")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float speedScalingFactor = 0.15f;
    [SerializeField] private float maxSpeed = 24f;
    [SerializeField] private float yBoundary = 4.5f;

    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;

    [Header("Life")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private float shortenPercent = 0.1f;

    [Header("Juice & Stretch")]
    [SerializeField] private float stretchFactor = 0.03f;
    [SerializeField] private float stretchSmoothness = 0.15f;

    [Header("Events")]
    [SerializeField] private IntGameEvent onLedgeLifeChanged;

    private Rigidbody2D rb;
    private float movementInput;
    private int currentLives;
    private Vector3 originalScale;
    private Vector3 targetScale;
    private bool isDashing;
    private float dashTargetY;
    private float dashSpeed;

    public PlayerSide Side => side;
    public int CurrentLives => currentLives;
    public bool IsBroken => currentLives <= 0;

    private float CurrentSpeed
    {
        get
        {
            int total = scoreManager != null ? scoreManager.TotalMatchScore : 0;
            return Mathf.Min(speed + total * speedScalingFactor, maxSpeed);
        }
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
        currentLives = maxLives;

        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();

        originalScale = transform.localScale;
        targetScale = originalScale;
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

        float clampedY = Mathf.Clamp(transform.position.y, -yBoundary, yBoundary);
        transform.position = new Vector2(transform.position.x, clampedY);

        CalculateStretch();
    }

    private void ProcessDash()
    {
        float currentY = transform.position.y;
        float diff = dashTargetY - currentY;
        float step = dashSpeed * Time.fixedDeltaTime;

        if (Mathf.Abs(diff) <= step)
        {
            transform.position = new Vector2(transform.position.x, dashTargetY);
            rb.linearVelocity = Vector2.zero;
            isDashing = false;
            return;
        }

        float dir = Mathf.Sign(diff);
        rb.linearVelocity = new Vector2(0f, dir * dashSpeed);
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
        if (currentLives == 1)
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
        var keyboard = Keyboard.current;
        if (keyboard != null)
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

        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            float stickY = gamepad.leftStick.y.value;
            if (Mathf.Abs(stickY) > 0.1f)
                return stickY;
        }

        return 0f;
    }

    public void TakeDamage(int damage)
    {
        if (IsBroken)
            return;

        currentLives = Mathf.Max(0, currentLives - damage);
        onLedgeLifeChanged?.Raise(currentLives);
        UpdateScaleForLife();
    }

    public void ResetLives()
    {
        currentLives = maxLives;
        onLedgeLifeChanged?.Raise(currentLives);
        transform.localScale = originalScale;
        targetScale = originalScale;
    }

    private void UpdateScaleForLife()
    {
        if (currentLives == 1)
        {
            Vector3 shortened = GetBaseScale();
            transform.localScale = shortened;
            targetScale = shortened;
        }
    }
}
