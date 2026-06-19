using UnityEngine;
using UnityEngine.InputSystem;

public class PowerUpManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BallController ball;
    [SerializeField] private LedgeController p1Ledge;
    [SerializeField] private LedgeController p2Ledge;
    [SerializeField] private ScoreManager scoreManager;

    [Header("Ball Dash")]
    [SerializeField] private float dashSpeedMultiplier = 3f;

    [Header("Force Bounce")]
    [SerializeField] private float forceBounceMultiplier = 1.8f;

    [Header("Destroyer Bounce")]
    [SerializeField] private float destroyerSpeedPerBounce = 1.5f;

    [Header("Ball Vanish")]
    [SerializeField] private float vanishVisibleDuration = 1.2f;
    [SerializeField] private float vanishInvisibleDuration = 0.8f;

    [Header("Events")]
    [SerializeField] private PlayerSideGameEvent onBallEnteredCourt;
    [SerializeField] private PlayerSideGameEvent onBallExitedCourt;
    [SerializeField] private ContactDataGameEvent onBallHitLedge;
    [SerializeField] private PlayerSideGameEvent onPointScored;
    [SerializeField] private PowerUpDataGameEvent onPowerUpPrimed;
    [SerializeField] private PowerUpDataGameEvent onPowerUpActivated;
    [SerializeField] private PowerUpDataGameEvent onPowerUpDeactivated;

    private static readonly int PowerUpCount = System.Enum.GetValues(typeof(PowerUpType)).Length;
    private static readonly int[] Costs = { 1, 1, 2, 4 };

    private PowerUpState[,] states = new PowerUpState[2, 4];

    private bool destroyerCarryOver;
    private PlayerSide destroyerOwner;

    private bool vanishCycling;
    private PlayerSide vanishOwner;
    private float vanishTimer;
    private bool vanishVisible;

    private void OnEnable()
    {
        if (onBallEnteredCourt != null) onBallEnteredCourt.OnRaised += HandleBallEnteredCourt;
        if (onBallExitedCourt != null) onBallExitedCourt.OnRaised += HandleBallExitedCourt;
        if (onBallHitLedge != null) onBallHitLedge.OnRaised += HandleBallHitLedge;
        if (onPointScored != null) onPointScored.OnRaised += HandlePointScored;
    }

    private void OnDisable()
    {
        if (onBallEnteredCourt != null) onBallEnteredCourt.OnRaised -= HandleBallEnteredCourt;
        if (onBallExitedCourt != null) onBallExitedCourt.OnRaised -= HandleBallExitedCourt;
        if (onBallHitLedge != null) onBallHitLedge.OnRaised -= HandleBallHitLedge;
        if (onPointScored != null) onPointScored.OnRaised -= HandlePointScored;
    }

    private void Update()
    {
        ReadPowerUpInput(PlayerSide.P1);
        ReadPowerUpInput(PlayerSide.P2);
        ReadRestartInput();
        UpdateVanishCycle();
    }

    private void ReadRestartInput()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            RestartMatch();
    }

    private void RestartMatch()
    {
        scoreManager.ResetMatch();
        ResetAllStates();
        ball.Restart();
        p1Ledge.ResetLives();
        p2Ledge.ResetLives();
    }

    private void ReadPowerUpInput(PlayerSide side)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (side == PlayerSide.P1)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) TryPrime(side, PowerUpType.BallDash);
            if (keyboard.digit2Key.wasPressedThisFrame) TryPrime(side, PowerUpType.ForceBounce);
            if (keyboard.digit3Key.wasPressedThisFrame) TryPrime(side, PowerUpType.DestroyerBounce);
            if (keyboard.digit4Key.wasPressedThisFrame) TryPrime(side, PowerUpType.BallVanish);
        }
        else
        {
            if (keyboard.numpad1Key.wasPressedThisFrame) TryPrime(side, PowerUpType.BallDash);
            if (keyboard.numpad2Key.wasPressedThisFrame) TryPrime(side, PowerUpType.ForceBounce);
            if (keyboard.numpad3Key.wasPressedThisFrame) TryPrime(side, PowerUpType.DestroyerBounce);
            if (keyboard.numpad4Key.wasPressedThisFrame) TryPrime(side, PowerUpType.BallVanish);
        }
    }

    private void TryPrime(PlayerSide side, PowerUpType type)
    {
        int sideIdx = (int)side;
        int typeIdx = (int)type;

        if (states[sideIdx, typeIdx] != PowerUpState.Inactive)
            return;

        if (!ValidateStacking(side, type))
            return;

        int cost = Costs[typeIdx];
        if (!scoreManager.SpendPoints(side, cost))
            return;

        states[sideIdx, typeIdx] = PowerUpState.Primed;
        FireEvent(onPowerUpPrimed, type, side);

        if (!ball.IsRespawning && ball.HasCourt && ball.CurrentCourt == side)
            ActivatePowerUp(side, type);
    }

    private void ActivatePowerUp(PlayerSide side, PowerUpType type)
    {
        states[(int)side, (int)type] = PowerUpState.Active;
        FireEvent(onPowerUpActivated, type, side);
        ExecuteOnActivation(side, type);
    }

    private bool ValidateStacking(PlayerSide side, PowerUpType type)
    {
        int sideIdx = (int)side;

        if (type == PowerUpType.DestroyerBounce)
        {
            if (states[sideIdx, (int)PowerUpType.BallVanish] != PowerUpState.Inactive)
                return false;
        }

        if (type == PowerUpType.BallVanish)
        {
            if (states[sideIdx, (int)PowerUpType.DestroyerBounce] != PowerUpState.Inactive)
                return false;
        }

        return true;
    }

    private void HandleBallEnteredCourt(PlayerSide court)
    {
        int sideIdx = (int)court;

        for (int i = 0; i < PowerUpCount; i++)
        {
            if (states[sideIdx, i] != PowerUpState.Primed)
                continue;

            states[sideIdx, i] = PowerUpState.Active;
            PowerUpType type = (PowerUpType)i;
            FireEvent(onPowerUpActivated, type, court);
            ExecuteOnActivation(court, type);
        }
    }

    private void HandleBallExitedCourt(PlayerSide court)
    {
        int sideIdx = (int)court;

        for (int i = 0; i < PowerUpCount; i++)
        {
            if (states[sideIdx, i] == PowerUpState.Inactive)
                continue;

            PowerUpType type = (PowerUpType)i;

            if (states[sideIdx, i] == PowerUpState.Active)
                HandleExitWhileActive(court, type);

            states[sideIdx, i] = PowerUpState.Inactive;
            FireEvent(onPowerUpDeactivated, type, court);
        }
    }

    private void HandleExitWhileActive(PlayerSide court, PowerUpType type)
    {
        if (type == PowerUpType.DestroyerBounce)
        {
            destroyerCarryOver = true;
            destroyerOwner = court;
            ball.SetDestroyerActive(true);
        }
    }

    private void ExecuteOnActivation(PlayerSide side, PowerUpType type)
    {
        if (type == PowerUpType.BallDash)
            ExecuteBallDash(side);

        if (type == PowerUpType.BallVanish)
            StartVanishCycle(side);
    }

    private void StartVanishCycle(PlayerSide owner)
    {
        vanishCycling = true;
        vanishOwner = owner;
        vanishTimer = 0f;
        vanishVisible = true;
        ball.SetVanishActive(true);
        ball.SetVisible(true);
    }

    private void StopVanishCycle()
    {
        vanishCycling = false;
        ball.SetVanishActive(false);
        ball.SetVisible(true);
    }

    private void HandleBallHitLedge(ContactData data)
    {
        int sideIdx = (int)data.ledgeSide;

        if (states[sideIdx, (int)PowerUpType.ForceBounce] == PowerUpState.Active)
        {
            ball.ApplySpeedMultiplier(forceBounceMultiplier);
            states[sideIdx, (int)PowerUpType.ForceBounce] = PowerUpState.Inactive;
            FireEvent(onPowerUpDeactivated, PowerUpType.ForceBounce, data.ledgeSide);
        }

        if (states[sideIdx, (int)PowerUpType.DestroyerBounce] == PowerUpState.Active)
        {
            ball.AddFlatSpeed(destroyerSpeedPerBounce);
            FireEvent(onPowerUpActivated, PowerUpType.DestroyerBounce, data.ledgeSide);
        }

        if (destroyerCarryOver && data.ledgeSide != destroyerOwner)
        {
            LedgeController hitLedge = data.ledgeSide == PlayerSide.P1 ? p1Ledge : p2Ledge;
            hitLedge.TakeDamage(1);
            destroyerCarryOver = false;
            ball.SetDestroyerActive(false);
        }

        if (vanishCycling && data.ledgeSide != vanishOwner)
            StopVanishCycle();
    }

    private void HandlePointScored(PlayerSide scorer)
    {
        bool destroyer = destroyerCarryOver || IsActive(GetOpponent(scorer), PowerUpType.DestroyerBounce);

        bool vanishActive = vanishCycling;
        bool vanishBonus = vanishActive && scorer == vanishOwner;

        scoreManager.RecordScore(scorer, destroyer, vanishBonus, vanishActive);
        ResetAllStates();
    }

    private void ExecuteBallDash(PlayerSide side)
    {
        LedgeController ledge = side == PlayerSide.P1 ? p1Ledge : p2Ledge;
        Vector2 ballPos = ball.GetPosition();
        ledge.DashToY(ballPos.y, dashSpeedMultiplier);

        states[(int)side, (int)PowerUpType.BallDash] = PowerUpState.Inactive;
        FireEvent(onPowerUpDeactivated, PowerUpType.BallDash, side);
    }

    private void UpdateVanishCycle()
    {
        if (!vanishCycling)
            return;

        vanishTimer += Time.deltaTime;

        if (vanishVisible && vanishTimer >= vanishVisibleDuration)
        {
            vanishVisible = false;
            vanishTimer = 0f;
            ball.SetVisible(false);
        }
        else if (!vanishVisible && vanishTimer >= vanishInvisibleDuration)
        {
            vanishVisible = true;
            vanishTimer = 0f;
            ball.SetVisible(true);
        }
    }

    private void ResetAllStates()
    {
        for (int s = 0; s < 2; s++)
        {
            for (int t = 0; t < PowerUpCount; t++)
            {
                if (states[s, t] != PowerUpState.Inactive)
                {
                    states[s, t] = PowerUpState.Inactive;
                    FireEvent(onPowerUpDeactivated, (PowerUpType)t, (PlayerSide)s);
                }
            }
        }

        destroyerCarryOver = false;
        ball.SetDestroyerActive(false);
        StopVanishCycle();
    }

    private bool IsActive(PlayerSide side, PowerUpType type)
    {
        return states[(int)side, (int)type] == PowerUpState.Active;
    }

    public PowerUpState GetState(PlayerSide side, PowerUpType type)
    {
        return states[(int)side, (int)type];
    }

    public int GetCost(PowerUpType type)
    {
        return Costs[(int)type];
    }

    private PlayerSide GetOpponent(PlayerSide side)
    {
        return side == PlayerSide.P1 ? PlayerSide.P2 : PlayerSide.P1;
    }

    private void FireEvent(PowerUpDataGameEvent evt, PowerUpType type, PlayerSide side)
    {
        if (evt == null) return;
        evt.Raise(new PowerUpEventData { type = type, side = side });
    }
}
