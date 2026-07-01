using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkDisconnectHandler : MonoBehaviourPunCallbacks
{
    [Header("Events")]
    [SerializeField] private NetworkConnectionGameEvent onConnectionStateChanged;
    [SerializeField] private PlayerSideGameEvent        onMatchEnd;
    [SerializeField] private GameEvent                  onMatchRestarted;
    [SerializeField] private BoolGameEvent onBallFreezeRequested;

    private Coroutine        reconnectCoroutine;
    private string           lastRoomName;
    private bool             matchInProgress;

    public override void OnEnable()
    {
        base.OnEnable();
        if (onMatchRestarted != null)
            onMatchRestarted.OnRaised += HandleMatchStarted;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (onMatchRestarted != null)
            onMatchRestarted.OnRaised -= HandleMatchStarted;
    }

    private void HandleMatchStarted()
    {
        OnMatchStarted();
    }

    public void OnMatchStarted()
    {
        matchInProgress = true;
        lastRoomName = PhotonNetwork.CurrentRoom?.Name;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!matchInProgress) return;
        StartReconnectWindow(otherPlayer);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (!matchInProgress) return;
        StartCoroutine(TryRejoin());
    }

    public override void OnJoinedRoom()
    {
        if (!matchInProgress) return;
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }
        ResumeMatch();
        onConnectionStateChanged?.Raise(NetworkConnectionState.InMatch);
    }

    private void StartReconnectWindow(Player disconnectedPlayer)
    {
        FreezeBall();
        onConnectionStateChanged?.Raise(NetworkConnectionState.Reconnecting);

        if (reconnectCoroutine != null) StopCoroutine(reconnectCoroutine);
        reconnectCoroutine = StartCoroutine(ReconnectCountdown(disconnectedPlayer));
    }

    private IEnumerator ReconnectCountdown(Player disconnectedPlayer)
    {
        yield return new WaitForSeconds(NetworkConfig.ReconnectWindowSec);

        if (!PhotonNetwork.IsMasterClient)
        {
            reconnectCoroutine = null;
            yield break;
        }

        PlayerSide winner = NetworkContext.LocalSide;
        onMatchEnd?.Raise(winner);
        matchInProgress = false;
        reconnectCoroutine = null;
    }

    private IEnumerator TryRejoin()
    {
        if (string.IsNullOrEmpty(lastRoomName)) yield break;

        onConnectionStateChanged?.Raise(NetworkConnectionState.Reconnecting);
        yield return new WaitForSeconds(1f);

        PhotonNetwork.RejoinRoom(lastRoomName);
    }

    private void FreezeBall()
    {
        onBallFreezeRequested?.Raise(true);
    }

    private void ResumeMatch()
    {
        onBallFreezeRequested?.Raise(false);
    }
}
