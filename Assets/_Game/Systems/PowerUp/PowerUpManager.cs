using UnityEngine;
using UnityEngine.InputSystem;

public class PowerUpManager : MonoBehaviour
{
    [Header("Ball Dash")]
    [SerializeField] private float dashSpeedMultiplier = 3f;

    [Header("Force Bounce")]
    [SerializeField] private float forceBounceMultiplier = 1.8f;

    [Header("Destroyer Bounce")]
    [SerializeField] private float destroyerSpeedPerBounce = 1.5f;

    [Header("Ball Vanish")]
    [SerializeField] private float vanishVisibleDuration = 1.2f;
    [SerializeField] private float vanishInvisibleDuration = 0.8f;

    [Header("Score")]
    [SerializeField] private ScoreManager scoreManager;

    [Header("Events")]
    [SerializeField] private PlayerSideGameEvent onBallEnteredCourt;
    [SerializeField] private PlayerSideGameEvent onBallExitedCourt;
    [SerializeField] private ContactDataGameEvent onBallHitLedge;
    [SerializeField] private PlayerSideGameEvent onPointScored;
    [SerializeField] private PowerUpDataGameEvent onPowerUpPrimeRequested;
    [SerializeField] private PowerUpDataGameEvent onPowerUpPrimed;
    [SerializeField] private PowerUpDataGameEvent onPowerUpActivated;
    [SerializeField] private PowerUpDataGameEvent onPowerUpDeactivated;

    [SerializeField] private ScoreRequestGameEvent onRequestRecordScore;
    [SerializeField] private SpendPointsGameEvent onSpendPointsRequested;
    [SerializeField] private LedgeDashGameEvent onLedgeDashRequested;
    [SerializeField] private LedgeDamageGameEvent onLedgeDamageRequested;
    [SerializeField] private FloatGameEvent onBallSpeedMultiplierRequested;
    [SerializeField] private FloatGameEvent onBallFlatSpeedRequested;
    [SerializeField] private BoolGameEvent onBallDestroyerStateChanged;
    [SerializeField] private BoolGameEvent onBallVanishStateChanged;
    [SerializeField] private BoolGameEvent onBallVisibilityChanged;
    [SerializeField] private GameEvent onMatchRestarted;
    [SerializeField] private ScoreStateGameEvent onScoreStateUpdated;
    [SerializeField] private Vector2GameEvent onBallPositionUpdated;
    [SerializeField] private GameEvent onBallHitBoundary;
    [SerializeField] private BoolGameEvent onNetworkPresentationModeSet;

    private readonly PowerUpState[,] states = new PowerUpState[2, Balance.PowerUpCount];

    private bool destroyerCarryOver;
    private PlayerSide destroyerOwner;

    private bool vanishCycling;
    private PlayerSide vanishOwner;
    private float vanishTimer;
    private bool vanishVisible;

    private int p1Spendable;
    private int p2Spendable;
    private Vector2 lastBallPosition;
    private PlayerSide activeCourt;
    private bool isBallInPlay;

    private bool isPresentationOnly;

    private void OnEnable()
    {
        if (onBallEnteredCourt != null) onBallEnteredCourt.OnRaised += HandleBallEnteredCourt;
        if (onBallExitedCourt != null) onBallExitedCourt.OnRaised += HandleBallExitedCourt;
        if (onBallHitLedge != null) onBallHitLedge.OnRaised += HandleBallHitLedge;
        if (onPointScored != null) onPointScored.OnRaised += HandlePointScored;
        if (onPowerUpPrimeRequested != null) onPowerUpPrimeRequested.OnRaised += HandlePowerUpPrimeRequested;

        if (onScoreStateUpdated != null) onScoreStateUpdated.OnRaised += HandleScoreStateUpdated;
        if (onBallPositionUpdated != null) onBallPositionUpdated.OnRaised += HandleBallPositionUpdated;
        if (onBallHitBoundary != null) onBallHitBoundary.OnRaised += HandleBallHitBoundary;
        if (onMatchRestarted != null) onMatchRestarted.OnRaised += HandleMatchRestarted;
        if (onNetworkPresentationModeSet != null) onNetworkPresentationModeSet.OnRaised += HandleNetworkPresentationModeSet;
    }

    private void OnDisable()
    {
        if (onBallEnteredCourt != null) onBallEnteredCourt.OnRaised -= HandleBallEnteredCourt;
        if (onBallExitedCourt != null) onBallExitedCourt.OnRaised -= HandleBallExitedCourt;
        if (onBallHitLedge != null) onBallHitLedge.OnRaised -= HandleBallHitLedge;
        if (onPointScored != null) onPointScored.OnRaised -= HandlePointScored;
        if (onPowerUpPrimeRequested != null) onPowerUpPrimeRequested.OnRaised -= HandlePowerUpPrimeRequested;

        if (onScoreStateUpdated != null) onScoreStateUpdated.OnRaised -= HandleScoreStateUpdated;
        if (onBallPositionUpdated != null) onBallPositionUpdated.OnRaised -= HandleBallPositionUpdated;
        if (onBallHitBoundary != null) onBallHitBoundary.OnRaised -= HandleBallHitBoundary;
        if (onMatchRestarted != null) onMatchRestarted.OnRaised -= HandleMatchRestarted;
        if (onNetworkPresentationModeSet != null) onNetworkPresentationModeSet.OnRaised -= HandleNetworkPresentationModeSet;
    }

    private void HandleScoreStateUpdated(ScoreStateData data)
    {
        p1Spendable = data.p1Spendable;
        p2Spendable = data.p2Spendable;
    }

    private void HandleBallPositionUpdated(Vector2 position)
    {
        lastBallPosition = position;
    }

    private void HandleBallHitBoundary()
    {
        if (isPresentationOnly) return;
        if (IsActive(activeCourt, PowerUpType.DestroyerBounce))
        {
            onBallFlatSpeedRequested?.Raise(destroyerSpeedPerBounce);
        }
    }

    private void HandleMatchRestarted()
    {
        if (isPresentationOnly)
        {
            isBallInPlay = false;
            return;
        }
        ResetAllStates();
    }

    public void SetPresentationMode(bool enabled)
    {
        isPresentationOnly = enabled;
    }

    private void HandleNetworkPresentationModeSet(bool enabled)
    {
        SetPresentationMode(enabled);
    }

    private void Update()
    {
        if (isPresentationOnly) return;

        ReadPowerUpInput(PlayerSide.P1);
        ReadPowerUpInput(PlayerSide.P2);
        ReadRestartInput();
        UpdateVanishCycle();
    }

    private void ReadRestartInput()
    {
        if (NetworkContext.Role != NetworkRole.Offline) return;
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            onMatchRestarted?.Raise();
    }

    private void ReadPowerUpInput(PlayerSide side)
    {
        if (NetworkContext.Role != NetworkRole.Offline && side != NetworkContext.LocalSide)
            return;

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

    public void TryPrime(PlayerSide side, PowerUpType type)
    {
        if (!IsValidPlayerSide(side) || !IsValidPowerUpType(type)) return;

        int sideIdx = (int)side;
        int typeIdx = (int)type;

        if (states[sideIdx, typeIdx] != PowerUpState.Inactive)
            return;

        if (!ValidateStacking(side, type))
            return;

        int cost = Balance.PowerUpCosts[typeIdx];

        bool spent = scoreManager != null
            ? scoreManager.SpendPoints(side, cost)
            : FallbackSpend(side, cost);

        if (!spent)
            return;

        states[sideIdx, typeIdx] = PowerUpState.Primed;
        FireEvent(onPowerUpPrimed, type, side);
        if (isBallInPlay && activeCourt == side)
            ActivatePowerUp(side, type);
    }

    private bool FallbackSpend(PlayerSide side, int cost)
    {
        int spendableBefore = side == PlayerSide.P1 ? p1Spendable : p2Spendable;
        if (spendableBefore < cost) return false;
        onSpendPointsRequested?.Raise(new SpendPointsData { side = side, amount = cost });
        int spendableAfter = side == PlayerSide.P1 ? p1Spendable : p2Spendable;
        return spendableAfter < spendableBefore;
    }

    private void HandlePowerUpPrimeRequested(PowerUpEventData data)
    {
        TryPrime(data.side, data.type);
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
        if (isPresentationOnly) return;
        activeCourt = court;
        isBallInPlay = true;
        int sideIdx = (int)court;

        for (int i = 0; i < Balance.PowerUpCount; i++)
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
        if (isPresentationOnly) return;
        int sideIdx = (int)court;

        for (int i = 0; i < Balance.PowerUpCount; i++)
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
            onBallDestroyerStateChanged?.Raise(true);
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
        onBallVanishStateChanged?.Raise(true);
        onBallVisibilityChanged?.Raise(true);
    }

    private void StopVanishCycle()
    {
        vanishCycling = false;
        onBallVanishStateChanged?.Raise(false);
        onBallVisibilityChanged?.Raise(true);
    }

    private void HandleBallHitLedge(ContactData data)
    {
        if (isPresentationOnly) return;
        int sideIdx = (int)data.ledgeSide;

        if (states[sideIdx, (int)PowerUpType.ForceBounce] == PowerUpState.Active)
        {
            onBallSpeedMultiplierRequested?.Raise(forceBounceMultiplier);
            states[sideIdx, (int)PowerUpType.ForceBounce] = PowerUpState.Inactive;
            FireEvent(onPowerUpDeactivated, PowerUpType.ForceBounce, data.ledgeSide);
        }

        if (states[sideIdx, (int)PowerUpType.DestroyerBounce] == PowerUpState.Active)
        {
            onBallFlatSpeedRequested?.Raise(destroyerSpeedPerBounce);
        }

        if (destroyerCarryOver && data.ledgeSide != destroyerOwner)
        {
            if (onLedgeDamageRequested != null)
            {
                var damageData = new LedgeDamageData
                {
                    side = data.ledgeSide,
                    damage = 1
                };
                onLedgeDamageRequested.Raise(damageData);
            }
            destroyerCarryOver = false;
            onBallDestroyerStateChanged?.Raise(false);
        }

        if (vanishCycling && data.ledgeSide != vanishOwner)
            StopVanishCycle();
    }

    private void HandlePointScored(PlayerSide scorer)
    {
        if (isPresentationOnly)
        {
            isBallInPlay = false;
            return;
        }
        bool destroyerBonus = destroyerCarryOver || IsActive(GetOpponent(scorer), PowerUpType.DestroyerBounce);
        bool vanishActive = vanishCycling;
        bool vanishBonus = vanishActive && scorer == vanishOwner;

        if (onRequestRecordScore != null)
        {
            var data = new ScoreRequestData
            {
                scorer = scorer,
                destroyerActive = destroyerBonus && !vanishActive,
                vanishBonus = vanishBonus,
                vanishActive = vanishActive
            };
            onRequestRecordScore.Raise(data);
        }

        // Ball Vanish is a life-reduction source: a vanish miss costs the scored-against ledge 1 life.
        if (vanishBonus && onLedgeDamageRequested != null)
        {
            onLedgeDamageRequested.Raise(new LedgeDamageData
            {
                side = GetOpponent(scorer),
                damage = 1
            });
        }

        isBallInPlay = false;
        ResetAllStates();
    }

    private void ExecuteBallDash(PlayerSide side)
    {
        if (onLedgeDashRequested != null)
        {
            var data = new LedgeDashData
            {
                side = side,
                targetY = lastBallPosition.y,
                dashSpeedMultiplier = dashSpeedMultiplier
            };
            onLedgeDashRequested.Raise(data);
        }

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
            onBallVisibilityChanged?.Raise(false);
        }
        else if (!vanishVisible && vanishTimer >= vanishInvisibleDuration)
        {
            vanishVisible = true;
            vanishTimer = 0f;
            onBallVisibilityChanged?.Raise(true);
        }
    }

    private void ResetAllStates()
    {
        for (int s = 0; s < 2; s++)
        {
            for (int t = 0; t < Balance.PowerUpCount; t++)
            {
                if (states[s, t] != PowerUpState.Inactive)
                {
                    states[s, t] = PowerUpState.Inactive;
                    FireEvent(onPowerUpDeactivated, (PowerUpType)t, (PlayerSide)s);
                }
            }
        }

        destroyerCarryOver = false;
        onBallDestroyerStateChanged?.Raise(false);
        isBallInPlay = false;
        StopVanishCycle();
    }

    private bool IsActive(PlayerSide side, PowerUpType type)
    {
        return states[(int)side, (int)type] == PowerUpState.Active;
    }

    public PowerUpState GetState(PlayerSide side, PowerUpType type)
    {
        if (!IsValidPlayerSide(side) || !IsValidPowerUpType(type)) return PowerUpState.Inactive;
        return states[(int)side, (int)type];
    }

    public int GetCost(PowerUpType type)
    {
        if (!IsValidPowerUpType(type)) return 0;
        return Balance.PowerUpCosts[(int)type];
    }

    private static bool IsValidPlayerSide(PlayerSide side) => side == PlayerSide.P1 || side == PlayerSide.P2;
    private static bool IsValidPowerUpType(PowerUpType type) => (int)type >= 0 && (int)type < Balance.PowerUpCount;

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
