using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerUpHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PowerUpManager powerUpManager;
    [SerializeField] private ScoreManager scoreManager;

    [Header("P1 Slots")]
    [SerializeField] private Image[] p1SlotIcons = new Image[4];
    [SerializeField] private TextMeshProUGUI p1SpendableText;

    [Header("P2 Slots")]
    [SerializeField] private Image[] p2SlotIcons = new Image[4];
    [SerializeField] private TextMeshProUGUI p2SpendableText;

    [Header("Lives")]
    [SerializeField] private TextMeshProUGUI p1LivesText;
    [SerializeField] private TextMeshProUGUI p2LivesText;
    [SerializeField] private LedgeController p1Ledge;
    [SerializeField] private LedgeController p2Ledge;

    [Header("Colors")]
    [SerializeField] private Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    [SerializeField] private Color affordableColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color primedColor = new Color(0f, 0.8f, 1f, 1f);
    [SerializeField] private Color activeColor = new Color(1f, 0.9f, 0f, 1f);

    [Header("Events")]
    [SerializeField] private PowerUpDataGameEvent onPowerUpPrimed;
    [SerializeField] private PowerUpDataGameEvent onPowerUpActivated;
    [SerializeField] private PowerUpDataGameEvent onPowerUpDeactivated;

    private float pulseTimer;

    private void OnEnable()
    {
        if (onPowerUpPrimed != null) onPowerUpPrimed.OnRaised += OnStateChanged;
        if (onPowerUpActivated != null) onPowerUpActivated.OnRaised += OnStateChanged;
        if (onPowerUpDeactivated != null) onPowerUpDeactivated.OnRaised += OnStateChanged;
    }

    private void OnDisable()
    {
        if (onPowerUpPrimed != null) onPowerUpPrimed.OnRaised -= OnStateChanged;
        if (onPowerUpActivated != null) onPowerUpActivated.OnRaised -= OnStateChanged;
        if (onPowerUpDeactivated != null) onPowerUpDeactivated.OnRaised -= OnStateChanged;
    }

    private void OnStateChanged(PowerUpEventData data)
    {
        RefreshSlots();
    }

    private void Update()
    {
        RefreshSpendable();
        RefreshLives();
        AnimatePulse();
    }

    private void RefreshSpendable()
    {
        if (scoreManager == null) return;

        if (p1SpendableText != null)
            p1SpendableText.text = $"SP: {scoreManager.P1Spendable}";
        if (p2SpendableText != null)
            p2SpendableText.text = $"SP: {scoreManager.P2Spendable}";
    }

    private void RefreshLives()
    {
        if (p1LivesText != null && p1Ledge != null)
            p1LivesText.text = FormatLives(p1Ledge.CurrentLives);
        if (p2LivesText != null && p2Ledge != null)
            p2LivesText.text = FormatLives(p2Ledge.CurrentLives);
    }

    private string FormatLives(int lives)
    {
        return lives switch
        {
            3 => "|||",
            2 => "|| ",
            1 => "|  ",
            _ => "X  "
        };
    }

    private void RefreshSlots()
    {
        if (powerUpManager == null || scoreManager == null) return;

        for (int i = 0; i < 4; i++)
        {
            UpdateSlot(PlayerSide.P1, (PowerUpType)i, p1SlotIcons, scoreManager.P1Spendable);
            UpdateSlot(PlayerSide.P2, (PowerUpType)i, p2SlotIcons, scoreManager.P2Spendable);
        }
    }

    private void UpdateSlot(PlayerSide side, PowerUpType type, Image[] slots, int spendable)
    {
        int idx = (int)type;
        if (idx >= slots.Length || slots[idx] == null) return;

        PowerUpState state = powerUpManager.GetState(side, type);
        int cost = powerUpManager.GetCost(type);

        Color color = state switch
        {
            PowerUpState.Primed => primedColor,
            PowerUpState.Active => activeColor,
            _ => spendable >= cost ? affordableColor : inactiveColor
        };

        slots[idx].color = color;
    }

    private void AnimatePulse()
    {
        if (powerUpManager == null) return;

        pulseTimer += Time.deltaTime * 4f;
        float pulse = (Mathf.Sin(pulseTimer) + 1f) * 0.5f;
        Color pulsed = Color.Lerp(primedColor, activeColor, pulse);

        for (int i = 0; i < 4; i++)
        {
            PulseIfPrimed(PlayerSide.P1, (PowerUpType)i, p1SlotIcons, pulsed);
            PulseIfPrimed(PlayerSide.P2, (PowerUpType)i, p2SlotIcons, pulsed);
        }
    }

    private void PulseIfPrimed(PlayerSide side, PowerUpType type, Image[] slots, Color pulsed)
    {
        int idx = (int)type;
        if (idx >= slots.Length || slots[idx] == null) return;

        if (powerUpManager.GetState(side, type) == PowerUpState.Primed)
            slots[idx].color = pulsed;
    }
}
