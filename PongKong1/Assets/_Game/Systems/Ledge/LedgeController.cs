using UnityEngine;
using UnityEngine.InputSystem;

public class LedgeController : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] protected PlayerSide side;

    [Header("Movement")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float yBoundary = 4.5f;

    [Header("Life")]
    [SerializeField] private int maxLives = 3;

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

    public PlayerSide Side => side;
    public int CurrentLives => currentLives;
    public bool IsBroken => currentLives <= 0;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
        currentLives = maxLives;
        
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    private void Update()
    {
        movementInput = ReadInput();
    }

    private void FixedUpdate()
    {
        float targetY = movementInput * speed;
        float smoothedY = Mathf.Lerp(rb.linearVelocity.y, targetY, 0.15f);
        rb.linearVelocity = new Vector2(0f, smoothedY);
        
        float clampedY = Mathf.Clamp(transform.position.y, -yBoundary, yBoundary);
        transform.position = new Vector2(transform.position.x, clampedY);

        CalculateStretch();
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
            targetScale = originalScale;
        }

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, stretchSmoothness);
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
    }

    public void ResetLives()
    {
        currentLives = maxLives;
        onLedgeLifeChanged?.Raise(currentLives);
    }
}
