using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class NetworkScoreRelay : MonoBehaviourPun
{
    [Header("Events — same assets as ScoreManager / LedgeController")]
    [SerializeField] private ScoreStateGameEvent        onScoreStateUpdated;
    [SerializeField] private PointScoredDataGameEvent   onPointScoredDetailed;
    [SerializeField] private LedgeLifeChangedGameEvent  onLedgeLifeChangedDetailed;
    [SerializeField] private PlayerSideGameEvent        onMatchEnd;
    [SerializeField] private LedgeDamageGameEvent       onLedgeDamageRequested;

    [Header("Network Lifecycle Events")]
    [SerializeField] private GameEvent onNetworkHostInitiated;

    private void OnEnable()
    {
        if (onNetworkHostInitiated != null)
            onNetworkHostInitiated.OnRaised += HandleNetworkHostInitiated;
    }

    private void OnDisable()
    {
        if (onNetworkHostInitiated != null)
            onNetworkHostInitiated.OnRaised -= HandleNetworkHostInitiated;
    }

    private bool hostModeReady;

    public void InitHost()
    {
        if (hostModeReady) return;
        hostModeReady = true;

        if (onScoreStateUpdated != null)        onScoreStateUpdated.OnRaised        += Host_OnScoreStateUpdated;
        if (onPointScoredDetailed != null)      onPointScoredDetailed.OnRaised      += Host_OnPointScored;
        if (onLedgeLifeChangedDetailed != null) onLedgeLifeChangedDetailed.OnRaised += Host_OnLedgeLifeChanged;
        if (onMatchEnd != null)                 onMatchEnd.OnRaised                 += Host_OnMatchEnd;
        if (onLedgeDamageRequested != null)     onLedgeDamageRequested.OnRaised     += Host_OnLedgeDamageRequested;
    }

    private void HandleNetworkHostInitiated() => InitHost();

    private void OnDestroy()
    {
        if (!hostModeReady) return;
        if (onScoreStateUpdated != null)        onScoreStateUpdated.OnRaised        -= Host_OnScoreStateUpdated;
        if (onPointScoredDetailed != null)      onPointScoredDetailed.OnRaised      -= Host_OnPointScored;
        if (onLedgeLifeChangedDetailed != null) onLedgeLifeChangedDetailed.OnRaised -= Host_OnLedgeLifeChanged;
        if (onMatchEnd != null)                 onMatchEnd.OnRaised                 -= Host_OnMatchEnd;
        if (onLedgeDamageRequested != null)     onLedgeDamageRequested.OnRaised     -= Host_OnLedgeDamageRequested;
    }

    private void Host_OnScoreStateUpdated(ScoreStateData data)
    {
        photonView.RPC(nameof(RPC_ScoreUpdate), RpcTarget.Others,
            data.p1Score, data.p2Score,
            data.p1Spendable, data.p2Spendable,
            data.totalMatchScore,
            data.isGameOver, data.isSuddenDeath);
    }

    [PunRPC]
    private void RPC_ScoreUpdate(int p1Score, int p2Score,
        int p1Spend, int p2Spend, int total, bool gameOver, bool sudden, PhotonMessageInfo info)
    {
        if (!IsSentByMasterClient(info)) return;
        onScoreStateUpdated?.Raise(new ScoreStateData
        {
            p1Score       = p1Score,
            p2Score       = p2Score,
            p1Spendable   = p1Spend,
            p2Spendable   = p2Spend,
            totalMatchScore = total,
            isGameOver    = gameOver,
            isSuddenDeath = sudden
        });
    }

    private void Host_OnPointScored(PointScoredData data)
    {
        photonView.RPC(nameof(RPC_PointScored), RpcTarget.Others,
            (byte)data.scorer, data.points,
            data.isDestroyerActive, data.isVanishActive, data.isBrokenLedge);
    }

    [PunRPC]
    private void RPC_PointScored(byte scorer, int points, bool destroyer, bool vanish, bool broken, PhotonMessageInfo info)
    {
        if (!IsSentByMasterClient(info)) return;
        if (!IsValidPlayerSide(scorer)) return;

        onPointScoredDetailed?.Raise(new PointScoredData
        {
            scorer           = (PlayerSide)scorer,
            points           = points,
            isDestroyerActive = destroyer,
            isVanishActive   = vanish,
            isBrokenLedge    = broken
        });
    }

    private void Host_OnLedgeLifeChanged(LedgeLifeChangedData data)
    {
        photonView.RPC(nameof(RPC_LedgeLife), RpcTarget.Others,
            (byte)data.side, data.currentLives, data.maxLives, data.isBroken);
    }

    [PunRPC]
    private void RPC_LedgeLife(byte side, int currentLives, int maxLives, bool broken, PhotonMessageInfo info)
    {
        if (!IsSentByMasterClient(info)) return;
        if (!IsValidPlayerSide(side)) return;

        var ps = (PlayerSide)side;

        onLedgeLifeChangedDetailed?.Raise(new LedgeLifeChangedData
        {
            side         = ps,
            currentLives = currentLives,
            maxLives     = maxLives,
            isBroken     = broken
        });
    }

    private void Host_OnLedgeDamageRequested(LedgeDamageData data)
    {
        PlayerSide remoteSide = NetworkContext.LocalSide == PlayerSide.P1 ? PlayerSide.P2 : PlayerSide.P1;
        if (data.side != remoteSide) return;

        photonView.RPC(nameof(RPC_LedgeDamage), RpcTarget.Others, (byte)data.side, data.damage);
    }

    public void SendLedgeDamage(PlayerSide side, int damage)
    {
        photonView.RPC(nameof(RPC_LedgeDamage), RpcTarget.Others, (byte)side, damage);
    }

    [PunRPC]
    private void RPC_LedgeDamage(byte side, int damage, PhotonMessageInfo info)
    {
        if (!IsSentByMasterClient(info)) return;
        if (!IsValidPlayerSide(side)) return;

        onLedgeDamageRequested?.Raise(new LedgeDamageData
        {
            side = (PlayerSide)side,
            damage = damage
        });
    }

    private void Host_OnMatchEnd(PlayerSide winner)
    {
        photonView.RPC(nameof(RPC_MatchEnd), RpcTarget.Others, (byte)winner);
    }

    [PunRPC]
    private void RPC_MatchEnd(byte winner, PhotonMessageInfo info)
    {
        if (!IsSentByMasterClient(info)) return;
        if (!IsValidPlayerSide(winner)) return;
        onMatchEnd?.Raise((PlayerSide)winner);
    }

    private static bool IsValidPlayerSide(byte side) => side < 2;

    private static bool IsSentByMasterClient(PhotonMessageInfo info)
        => PhotonNetwork.MasterClient != null
        && info.Sender.ActorNumber == PhotonNetwork.MasterClient.ActorNumber;
}
