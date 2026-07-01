using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    [Header("Win Condition")]
    [SerializeField] private int winScore = 20;
    [SerializeField] private bool suddenDeathEnabled = true;

    [Header("Events")]
    [SerializeField] private PlayerSideGameEvent onMatchEnd;
    [SerializeField] private PointScoredDataGameEvent onPointScoredDetailed;
    [SerializeField] private ScoreStateGameEvent onScoreStateUpdated;
    [SerializeField] private LedgeLifeChangedGameEvent onLedgeLifeChangedDetailed;
    [SerializeField] private ScoreRequestGameEvent onRequestRecordScore;
    [SerializeField] private SpendPointsGameEvent onSpendPointsRequested;
    [SerializeField] private GameEvent onMatchRestarted;
    [SerializeField] private BoolGameEvent onNetworkClientModeSet;

    private int p1Score;
    private int p2Score;
    private int p1Spendable;
    private int p2Spendable;
    private int p1Lives = Balance.DefaultLives;
    private int p2Lives = Balance.DefaultLives;
    private bool isGameOver;
    private bool isSuddenDeath;

    private bool isClientMode;

    public int P1Score => p1Score;
    public int P2Score => p2Score;
    public int P1Spendable => p1Spendable;
    public int P2Spendable => p2Spendable;
    public int TotalMatchScore => p1Score + p2Score;
    public bool IsGameOver => isGameOver;
    public bool IsSuddenDeath => isSuddenDeath;

    private void OnEnable()
    {
        if (onLedgeLifeChangedDetailed != null)
            onLedgeLifeChangedDetailed.OnRaised += HandleLedgeLifeChanged;
        if (onRequestRecordScore != null)
            onRequestRecordScore.OnRaised += HandleRequestRecordScore;
        if (onSpendPointsRequested != null)
            onSpendPointsRequested.OnRaised += HandleSpendPointsRequested;
        if (onMatchRestarted != null)
            onMatchRestarted.OnRaised += HandleMatchRestarted;
        if (onNetworkClientModeSet != null)
            onNetworkClientModeSet.OnRaised += HandleNetworkClientModeSet;
    }

    private void OnDisable()
    {
        if (onLedgeLifeChangedDetailed != null)
            onLedgeLifeChangedDetailed.OnRaised -= HandleLedgeLifeChanged;
        if (onRequestRecordScore != null)
            onRequestRecordScore.OnRaised -= HandleRequestRecordScore;
        if (onSpendPointsRequested != null)
            onSpendPointsRequested.OnRaised -= HandleSpendPointsRequested;
        if (onMatchRestarted != null)
            onMatchRestarted.OnRaised -= HandleMatchRestarted;
        if (onNetworkClientModeSet != null)
            onNetworkClientModeSet.OnRaised -= HandleNetworkClientModeSet;
    }

    public void SetClientMode(bool clientMode)
    {
        isClientMode = clientMode;
    }

    private void HandleNetworkClientModeSet(bool clientMode)
    {
        SetClientMode(clientMode);
    }

    private void HandleLedgeLifeChanged(LedgeLifeChangedData data)
    {
        if (isClientMode) return;
        if (data.side == PlayerSide.P1)
            p1Lives = data.currentLives;
        else if (data.side == PlayerSide.P2)
            p2Lives = data.currentLives;
    }

    private void HandleRequestRecordScore(ScoreRequestData data)
    {
        if (isClientMode) return;
        RecordScore(data.scorer, data.destroyerActive, data.vanishBonus, data.vanishActive);
    }

    private void HandleSpendPointsRequested(SpendPointsData data)
    {
        if (isClientMode) return;
        SpendPoints(data.side, data.amount);
    }

    private void HandleMatchRestarted()
    {
        if (isClientMode) return;
        ResetMatch();
    }

    public void RecordScore(PlayerSide scorer)
    {
        RecordScore(scorer, false, false, false);
    }

    public void RecordScore(PlayerSide scorer, bool destroyerActive, bool vanishBonus, bool vanishActive)
    {
        if (isGameOver)
            return;

        bool brokenLedge = (scorer == PlayerSide.P1) ? (p2Lives <= 0) : (p1Lives <= 0);

        int points = CalculatePoints(destroyerActive, vanishBonus, brokenLedge);
        int penalty = vanishActive ? Balance.VanishPenalty : 0;

        if (scorer == PlayerSide.P1)
        {
            p1Score += points;
            p1Spendable += points;
            if (penalty > 0)
            {
                p2Score = Mathf.Max(0, p2Score - penalty);
                p2Spendable = Mathf.Max(0, p2Spendable - penalty);
            }
        }
        else
        {
            p2Score += points;
            p2Spendable += points;
            if (penalty > 0)
            {
                p1Score = Mathf.Max(0, p1Score - penalty);
                p1Spendable = Mathf.Max(0, p1Spendable - penalty);
            }
        }

        if (onPointScoredDetailed != null)
        {
            var data = new PointScoredData
            {
                scorer = scorer,
                points = points,
                isDestroyerActive = destroyerActive,
                isVanishActive = vanishActive,
                isBrokenLedge = brokenLedge
            };
            onPointScoredDetailed.Raise(data);
        }

        CheckWinCondition();

        RaiseScoreStateUpdated();
    }

    private int CalculatePoints(bool destroyer, bool vanishBonus, bool brokenLedge)
    {
        if (vanishBonus && brokenLedge)
            return Balance.VanishBrokenPoints;
        if (vanishBonus)
            return Balance.VanishMissPoints;
        if (destroyer && brokenLedge)
            return Balance.DestroyerBrokenPoints;
        if (destroyer)
            return Balance.DestroyerMissPoints;
        if (brokenLedge)
            return Balance.BrokenLedgePoints;
        return Balance.BaseMissPoints;
    }

    private void CheckWinCondition()
    {
        if (isSuddenDeath)
        {
            if (p1Score > p2Score)
                EndMatch(PlayerSide.P1);
            else if (p2Score > p1Score)
                EndMatch(PlayerSide.P2);
            return;
        }

        if (p1Score >= winScore && p2Score >= winScore && suddenDeathEnabled)
        {
            isSuddenDeath = true;
            return;
        }

        if (p1Score >= winScore && p1Score > p2Score)
            EndMatch(PlayerSide.P1);
        else if (p2Score >= winScore && p2Score > p1Score)
            EndMatch(PlayerSide.P2);
    }

    private void EndMatch(PlayerSide winner)
    {
        isGameOver = true;
        onMatchEnd?.Raise(winner);
    }

    public bool SpendPoints(PlayerSide player, int cost)
    {
        if (isGameOver)
            return false;

        bool spent = false;
        if (player == PlayerSide.P1 && p1Spendable >= cost)
        {
            p1Spendable -= cost;
            spent = true;
        }
        else if (player == PlayerSide.P2 && p2Spendable >= cost)
        {
            p2Spendable -= cost;
            spent = true;
        }

        if (spent)
        {
            RaiseScoreStateUpdated();
        }

        return spent;
    }

    public int GetSpendable(PlayerSide player)
    {
        return player == PlayerSide.P1 ? p1Spendable : p2Spendable;
    }

    public void ResetMatch()
    {
        p1Score = 0;
        p2Score = 0;
        p1Spendable = 0;
        p2Spendable = 0;
        p1Lives = Balance.DefaultLives;
        p2Lives = Balance.DefaultLives;
        isGameOver = false;
        isSuddenDeath = false;

        RaiseScoreStateUpdated();
    }

    private void RaiseScoreStateUpdated()
    {
        if (onScoreStateUpdated != null)
        {
            var data = new ScoreStateData
            {
                p1Score = p1Score,
                p2Score = p2Score,
                p1Spendable = p1Spendable,
                p2Spendable = p2Spendable,
                totalMatchScore = TotalMatchScore,
                isGameOver = isGameOver,
                isSuddenDeath = isSuddenDeath
            };
            onScoreStateUpdated.Raise(data);
        }
    }
}
