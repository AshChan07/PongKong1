using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerUpHUD : MonoBehaviour
{
    [Header("P1 Slots")]
    [SerializeField] private Image[] p1SlotIcons = new Image[4];
    [SerializeField] private TextMeshProUGUI p1SpendableText;

    [Header("P2 Slots")]
    [SerializeField] private Image[] p2SlotIcons = new Image[4];
    [SerializeField] private TextMeshProUGUI p2SpendableText;

    [Header("Lives")]
    [SerializeField] private TextMeshProUGUI p1LivesText;
    [SerializeField] private TextMeshProUGUI p2LivesText;

    [Header("Colors")]
    [SerializeField] private Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    [SerializeField] private Color affordableColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color primedColor = new Color(0f, 0.8f, 1f, 1f);
    [SerializeField] private Color activeColor = new Color(1f, 0.9f, 0f, 1f);

    [Header("Events")]
    [SerializeField] private PowerUpDataGameEvent onPowerUpPrimed;
    [SerializeField] private PowerUpDataGameEvent onPowerUpActivated;
    [SerializeField] private PowerUpDataGameEvent onPowerUpDeactivated;
    [SerializeField] private ScoreStateGameEvent onScoreStateUpdated;
    [SerializeField] private LedgeLifeChangedGameEvent onLedgeLifeChangedDetailed;
    [SerializeField] private GameEvent onMatchRestarted;

    private readonly PowerUpState[,] powerUpStates = new PowerUpState[2, Balance.PowerUpCount];
    private int p1Spendable;
    private int p2Spendable;
    private int p1Lives = Balance.DefaultLives;
    private int p2Lives = Balance.DefaultLives;

    private float pulseTimer;

    private void OnEnable()
    {
        if (onPowerUpPrimed != null) onPowerUpPrimed.OnRaised += HandlePowerUpPrimed;
        if (onPowerUpActivated != null) onPowerUpActivated.OnRaised += HandlePowerUpActivated;
        if (onPowerUpDeactivated != null) onPowerUpDeactivated.OnRaised += HandlePowerUpDeactivated;
        if (onScoreStateUpdated != null) onScoreStateUpdated.OnRaised += HandleScoreStateUpdated;
        if (onLedgeLifeChangedDetailed != null) onLedgeLifeChangedDetailed.OnRaised += HandleLedgeLifeChanged;
        if (onMatchRestarted != null) onMatchRestarted.OnRaised += HandleMatchRestarted;
    }

    private void OnDisable()
    {
        if (onPowerUpPrimed != null) onPowerUpPrimed.OnRaised -= HandlePowerUpPrimed;
        if (onPowerUpActivated != null) onPowerUpActivated.OnRaised -= HandlePowerUpActivated;
        if (onPowerUpDeactivated != null) onPowerUpDeactivated.OnRaised -= HandlePowerUpDeactivated;
        if (onScoreStateUpdated != null) onScoreStateUpdated.OnRaised -= HandleScoreStateUpdated;
        if (onLedgeLifeChangedDetailed != null) onLedgeLifeChangedDetailed.OnRaised -= HandleLedgeLifeChanged;
        if (onMatchRestarted != null) onMatchRestarted.OnRaised -= HandleMatchRestarted;
    }

    private void HandlePowerUpPrimed(PowerUpEventData data)
    {
        powerUpStates[(int)data.side, (int)data.type] = PowerUpState.Primed;
        RefreshSlots();
    }

    private void HandlePowerUpActivated(PowerUpEventData data)
    {
        powerUpStates[(int)data.side, (int)data.type] = PowerUpState.Active;
        RefreshSlots();
    }

    private void HandlePowerUpDeactivated(PowerUpEventData data)
    {
        powerUpStates[(int)data.side, (int)data.type] = PowerUpState.Inactive;
        RefreshSlots();
    }

    private void HandleScoreStateUpdated(ScoreStateData data)
    {
        p1Spendable = data.p1Spendable;
        p2Spendable = data.p2Spendable;

        if (p1SpendableText != null)
            p1SpendableText.text = $"SP: {p1Spendable}";
        if (p2SpendableText != null)
            p2SpendableText.text = $"SP: {p2Spendable}";

        RefreshSlots();
    }

    private void HandleLedgeLifeChanged(LedgeLifeChangedData data)
    {
        if (data.side == PlayerSide.P1)
        {
            p1Lives = data.currentLives;
            if (p1LivesText != null)
                p1LivesText.text = FormatLives(p1Lives);
        }
        else if (data.side == PlayerSide.P2)
        {
            p2Lives = data.currentLives;
            if (p2LivesText != null)
                p2LivesText.text = FormatLives(p2Lives);
        }
    }

    private void HandleMatchRestarted()
    {
        for (int s = 0; s < 2; s++)
        {
            for (int t = 0; t < Balance.PowerUpCount; t++)
            {
                powerUpStates[s, t] = PowerUpState.Inactive;
            }
        }
        p1Spendable = 0;
        p2Spendable = 0;
        p1Lives = Balance.DefaultLives;
        p2Lives = Balance.DefaultLives;

        if (p1LivesText != null)
            p1LivesText.text = FormatLives(p1Lives);
        if (p2LivesText != null)
            p2LivesText.text = FormatLives(p2Lives);

        if (p1SpendableText != null)
            p1SpendableText.text = $"SP: {p1Spendable}";
        if (p2SpendableText != null)
            p2SpendableText.text = $"SP: {p2Spendable}";

        RefreshSlots();
    }

    private void Update()
    {
        AnimatePulse();
    }

    private string FormatLives(int lives)
    {
        if (lives >= Balance.DefaultLives) return "|||";
        if (lives == 2) return "|| ";
        if (lives == 1) return "|  ";
        return "X  ";
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < Balance.PowerUpCount; i++)
        {
            UpdateSlot(PlayerSide.P1, (PowerUpType)i, p1SlotIcons, p1Spendable);
            UpdateSlot(PlayerSide.P2, (PowerUpType)i, p2SlotIcons, p2Spendable);
        }
    }

    private void UpdateSlot(PlayerSide side, PowerUpType type, Image[] slots, int spendable)
    {
        int idx = (int)type;
        if (idx >= slots.Length || slots[idx] == null) return;

        PowerUpState state = powerUpStates[(int)side, idx];
        int cost = GetCost(type);

        Color color = state switch
        {
            PowerUpState.Primed => primedColor,
            PowerUpState.Active => activeColor,
            _ => spendable >= cost ? affordableColor : inactiveColor
        };

        slots[idx].color = color;
    }

    private int GetCost(PowerUpType type)
    {
        return Balance.PowerUpCosts[(int)type];
    }

    private void AnimatePulse()
    {
        bool anyPrimed = false;
        for (int s = 0; s < 2 && !anyPrimed; s++)
            for (int t = 0; t < Balance.PowerUpCount && !anyPrimed; t++)
                anyPrimed = powerUpStates[s, t] == PowerUpState.Primed;

        if (!anyPrimed)
            return;

        pulseTimer += Time.deltaTime * 4f;
        float pulse = (Mathf.Sin(pulseTimer) + 1f) * 0.5f;
        Color pulsed = Color.Lerp(primedColor, activeColor, pulse);

        for (int i = 0; i < Balance.PowerUpCount; i++)
        {
            PulseIfPrimed(PlayerSide.P1, (PowerUpType)i, p1SlotIcons, pulsed);
            PulseIfPrimed(PlayerSide.P2, (PowerUpType)i, p2SlotIcons, pulsed);
        }
    }

    private void PulseIfPrimed(PlayerSide side, PowerUpType type, Image[] slots, Color pulsed)
    {
        int idx = (int)type;
        if (idx >= slots.Length || slots[idx] == null) return;

        if (powerUpStates[(int)side, idx] == PowerUpState.Primed)
            slots[idx].color = pulsed;
    }
}
