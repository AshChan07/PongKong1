using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    [Header("Win Condition")]
    [SerializeField] private int winScore = 20;
    [SerializeField] private bool suddenDeathEnabled = true;

    [Header("Points")]
    [SerializeField] private int baseMissPoints = 2;

    [Header("Events")]
    [SerializeField] private IntGameEvent onScoreChanged;
    [SerializeField] private PlayerSideGameEvent onMatchEnd;
    [SerializeField] private PointScoredDataGameEvent onPointScoredDetailed;

    [Header("References")]
    [SerializeField] private LedgeController p1Ledge;
    [SerializeField] private LedgeController p2Ledge;

    private int p1Score;
    private int p2Score;
    private int p1Spendable;
    private int p2Spendable;
    private bool isGameOver;
    private bool isSuddenDeath;

    public int P1Score => p1Score;
    public int P2Score => p2Score;
    public int P1Spendable => p1Spendable;
    public int P2Spendable => p2Spendable;
    public int TotalMatchScore => p1Score + p2Score;
    public bool IsGameOver => isGameOver;
    public bool IsSuddenDeath => isSuddenDeath;

    public void RecordScore(PlayerSide scorer)
    {
        RecordScore(scorer, false, false, false);
    }

    public void RecordScore(PlayerSide scorer, bool destroyerActive, bool vanishBonus, bool vanishPenalty)
    {
        if (isGameOver)
            return;

        LedgeController missedLedge = scorer == PlayerSide.P1 ? p2Ledge : p1Ledge;
        bool brokenLedge = missedLedge != null && missedLedge.IsBroken;

        int points = CalculatePoints(destroyerActive, vanishBonus, brokenLedge);
        int penalty = vanishPenalty ? 1 : 0;

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

        onScoreChanged?.Raise(TotalMatchScore);

        if (onPointScoredDetailed != null)
        {
            var data = new PointScoredData
            {
                scorer = scorer,
                points = points,
                isDestroyerActive = destroyerActive,
                isVanishActive = vanishPenalty,
                isBrokenLedge = brokenLedge
            };
            onPointScoredDetailed.Raise(data);
        }

        CheckWinCondition();
    }

    private int CalculatePoints(bool destroyer, bool vanishBonus, bool brokenLedge)
    {
        if (vanishBonus && brokenLedge)
            return 16;
        if (vanishBonus)
            return 8;
        if (destroyer && brokenLedge)
            return 8;
        if (destroyer)
            return 4;
        if (brokenLedge)
            return 4;
        return baseMissPoints;
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

        if (player == PlayerSide.P1 && p1Spendable >= cost)
        {
            p1Spendable -= cost;
            return true;
        }

        if (player == PlayerSide.P2 && p2Spendable >= cost)
        {
            p2Spendable -= cost;
            return true;
        }

        return false;
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
        isGameOver = false;
        isSuddenDeath = false;
        onScoreChanged?.Raise(0);
    }
}
