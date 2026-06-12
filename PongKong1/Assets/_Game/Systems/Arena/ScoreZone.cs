using UnityEngine;

public class ScoreZone : MonoBehaviour
{
    [Header("Zone")]
    [SerializeField] private PlayerSide zoneOwner;

    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;

    [Header("Events")]
    [SerializeField] private PlayerSideGameEvent onPointScored;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Ball"))
            return;

        PlayerSide scorer = zoneOwner == PlayerSide.P1 ? PlayerSide.P2 : PlayerSide.P1;

        if (onPointScored != null)
            onPointScored.Raise(scorer);
        else if (scoreManager != null)
            scoreManager.RecordScore(scorer);

        BallController ball = other.GetComponent<BallController>();
        if (ball != null)
            ball.ResetAndRespawn();
    }
}
