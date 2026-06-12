using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    [Header("Win Condition")]
    [SerializeField] private int winScore = 20;
    [SerializeField] private bool suddenDeathEnabled = true;

    [Header("Points")]
    [SerializeField] private int baseMissPoints = 2;

    [Header("Events")]
    [SerializeField] private PlayerSideGameEvent onPointScored;
    [SerializeField] private IntGameEvent onScoreChanged;
    [SerializeField] private PlayerSideGameEvent onMatchEnd;

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

    private void OnEnable()
    {
        if (onPointScored != null)
            onPointScored.OnRaised += HandlePointScored;
    }

    private void OnDisable()
    {
        if (onPointScored != null)
            onPointScored.OnRaised -= HandlePointScored;
    }

    private void HandlePointScored(PlayerSide scorer)
    {
        RecordScore(scorer);
    }

    public void RecordScore(PlayerSide scorer)
    {
        if (isGameOver)
            return;

        int points = baseMissPoints;
        if (scorer == PlayerSide.P1)
        {
            p1Score += points;
            p1Spendable += points;
        }
        else
        {
            p2Score += points;
            p2Spendable += points;
        }

        onScoreChanged?.Raise(TotalMatchScore);
        CheckWinCondition();
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
