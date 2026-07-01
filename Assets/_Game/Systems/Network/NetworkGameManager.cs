using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(PhotonView))]
public class NetworkGameManager : MonoBehaviourPunCallbacks
{
    [Header("Lifecycle Events")]
    [SerializeField] private BoolGameEvent onNetworkClientModeSet;
    [SerializeField] private BoolGameEvent onNetworkPresentationModeSet;
    [SerializeField] private GameEvent onNetworkHostInitiated;
    [SerializeField] private GameEvent onNetworkClientInitiated;
    [SerializeField] private GameEvent onNetworkMasterMigrated;
    [SerializeField] private GameEvent onMatchRestarted;

    private bool systemsSetup;
    private Coroutine matchStartCoroutine;

    private void Awake()
    {
        if (PhotonNetwork.IsConnected)
            SetupRole();
    }

    private void Start()
    {
        if (!PhotonNetwork.IsConnected)
            return;

        SetupSystems();
        InitiateMatchStart();
    }

    private void SetupRole()
    {
        if (PhotonNetwork.IsMasterClient)
            NetworkContext.SetRole(NetworkRole.Host, PlayerSide.P1);
        else
            NetworkContext.SetRole(NetworkRole.Client, PlayerSide.P2);
    }

    private void SetupSystems()
    {
        if (systemsSetup) return;
        systemsSetup = true;

        if (NetworkContext.Role == NetworkRole.Client)
        {
            onNetworkClientModeSet?.Raise(true);
            onNetworkPresentationModeSet?.Raise(true);
            onNetworkClientInitiated?.Raise();
        }
        else
        {
            onNetworkHostInitiated?.Raise();
        }
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (matchStartCoroutine != null)
        {
            StopCoroutine(matchStartCoroutine);
            matchStartCoroutine = null;
        }
    }

    private void InitiateMatchStart()
    {
        if (PhotonNetwork.IsMasterClient)
            matchStartCoroutine = StartCoroutine(MatchStartDelay());
    }

    private IEnumerator MatchStartDelay()
    {
        yield return new WaitForSeconds(0.3f);
        DoStartMatch();
    }

    private void DoStartMatch()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC(nameof(RPC_StartMatch), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_StartMatch()
    {
        if (!systemsSetup) SetupSystems();
        onMatchRestarted?.Raise();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        systemsSetup = false;

        NetworkContext.SetRole(NetworkRole.Host, NetworkContext.LocalSide);

        onNetworkClientModeSet?.Raise(false);
        onNetworkPresentationModeSet?.Raise(false);
        onNetworkHostInitiated?.Raise();
        onNetworkMasterMigrated?.Raise();
    }
}
