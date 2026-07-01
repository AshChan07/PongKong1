using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class NetworkPowerUpRelay : MonoBehaviourPun
{
    [Header("Events — same assets as PowerUpManager")]
    [SerializeField] private PowerUpDataGameEvent onPowerUpPrimeRequested;
    [SerializeField] private PowerUpDataGameEvent onPowerUpPrimed;
    [SerializeField] private PowerUpDataGameEvent onPowerUpActivated;
    [SerializeField] private PowerUpDataGameEvent onPowerUpDeactivated;
    [SerializeField] private LedgeDashGameEvent   onLedgeDashRequested;
    [SerializeField] private BoolGameEvent        onBallVanishStateChanged;

    [Header("Network Lifecycle Events")]
    [SerializeField] private GameEvent onNetworkHostInitiated;

    private bool hostModeReady;

    private readonly PowerUpState[,] clientStates = new PowerUpState[2, Balance.PowerUpCount];

    private const float RpcCooldownSec = 0.1f;
    private readonly Dictionary<int, float> lastRpcTime = new Dictionary<int, float>();

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

    private void HandleNetworkHostInitiated() => InitHost();

    public void InitHost()
    {
        if (hostModeReady) return;
        hostModeReady  = true;

        if (onLedgeDashRequested != null)
            onLedgeDashRequested.OnRaised += Host_OnLedgeDashRequested;
        if (onPowerUpActivated != null)
            onPowerUpActivated.OnRaised += Host_OnPowerUpActivated;
        if (onPowerUpDeactivated != null)
            onPowerUpDeactivated.OnRaised += Host_OnPowerUpDeactivated;
    }

    private void OnDestroy()
    {
        if (hostModeReady)
        {
            if (onLedgeDashRequested != null)
                onLedgeDashRequested.OnRaised -= Host_OnLedgeDashRequested;
            if (onPowerUpActivated != null)
                onPowerUpActivated.OnRaised -= Host_OnPowerUpActivated;
            if (onPowerUpDeactivated != null)
                onPowerUpDeactivated.OnRaised -= Host_OnPowerUpDeactivated;
        }
    }

    private void Host_OnLedgeDashRequested(LedgeDashData data)
    {
        PlayerSide remoteSide = NetworkContext.LocalSide == PlayerSide.P1 ? PlayerSide.P2 : PlayerSide.P1;
        if (data.side != remoteSide) return;

        photonView.RPC(nameof(RPC_BallDash), RpcTarget.Others,
            (byte)data.side, data.targetY, data.dashSpeedMultiplier);
    }

    private void Host_OnPowerUpActivated(PowerUpEventData data)
    {
        SendPowerUpState(data.side, data.type, PowerUpState.Active);
    }

    private void Host_OnPowerUpDeactivated(PowerUpEventData data)
    {
        SendPowerUpState(data.side, data.type, PowerUpState.Inactive);
    }

    private void Update()
    {
        if (NetworkContext.Role != NetworkRole.Client) return;
        ReadClientPowerUpInput();
    }

    private void ReadClientPowerUpInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        PlayerSide side = NetworkContext.LocalSide;

        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            RequestPrime(side, PowerUpType.BallDash);
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
            RequestPrime(side, PowerUpType.ForceBounce);
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
            RequestPrime(side, PowerUpType.DestroyerBounce);
        if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
            RequestPrime(side, PowerUpType.BallVanish);
    }

    public void RequestPrime(PlayerSide side, PowerUpType type)
    {
        photonView.RPC(nameof(RPC_RequestPrime), RpcTarget.MasterClient, (byte)side, (byte)type);
    }

    [PunRPC]
    private void RPC_RequestPrime(byte side, byte type, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!IsValidPlayerSide(side) || !IsValidPowerUpType(type)) return;

        int actor = info.Sender.ActorNumber;
        float now = Time.time;
        if (lastRpcTime.TryGetValue(actor, out float last) && now - last < RpcCooldownSec)
            return;
        lastRpcTime[actor] = now;

        PlayerSide senderSide = (info.Sender.ActorNumber == PhotonNetwork.MasterClient.ActorNumber)
            ? PlayerSide.P1 : PlayerSide.P2;
        if ((PlayerSide)side != senderSide) return;
        onPowerUpPrimeRequested?.Raise(new PowerUpEventData
        {
            side = (PlayerSide)side,
            type = (PowerUpType)type
        });
    }

    public void SendPowerUpState(PlayerSide side, PowerUpType type, PowerUpState state, float extraArg = 0f)
    {
        photonView.RPC(nameof(RPC_PowerUpState), RpcTarget.Others,
            (byte)side, (byte)type, (byte)state, extraArg);
    }

    [PunRPC]
    private void RPC_PowerUpState(byte side, byte type, byte state, float extraArg, PhotonMessageInfo info)
    {
        if (!IsSentByMasterClient(info)) return;
        if (!IsValidPlayerSide(side) || !IsValidPowerUpType(type) || !IsValidPowerUpState(state)) return;

        var ps   = (PlayerSide)side;
        var pt   = (PowerUpType)type;
        var pst  = (PowerUpState)state;

        clientStates[(int)ps, (int)pt] = pst;

        var data = new PowerUpEventData { type = pt, side = ps };
        switch (pst)
        {
            case PowerUpState.Primed:
                onPowerUpPrimed?.Raise(data);
                break;
            case PowerUpState.Active:
                onPowerUpActivated?.Raise(data);
                ExecuteClientEffect(ps, pt, extraArg);
                break;
            case PowerUpState.Inactive:
                onPowerUpDeactivated?.Raise(data);
                if (pt == PowerUpType.BallVanish)
                    onBallVanishStateChanged?.Raise(false);
                break;
        }
    }

    [PunRPC]
    private void RPC_BallDash(byte side, float targetY, float dashSpeedMultiplier, PhotonMessageInfo info)
    {
        if (!IsSentByMasterClient(info)) return;
        if (onLedgeDashRequested == null) return;
        if (!IsValidPlayerSide(side)) return;

        onLedgeDashRequested.Raise(new LedgeDashData
        {
            side               = (PlayerSide)side,
            targetY            = targetY,
            dashSpeedMultiplier = dashSpeedMultiplier
        });
    }

    private void ExecuteClientEffect(PlayerSide side, PowerUpType type, float extraArg)
    {
        switch (type)
        {
            case PowerUpType.BallVanish:
                onBallVanishStateChanged?.Raise(true);
                break;
        }
    }

    public PowerUpState GetClientState(PlayerSide side, PowerUpType type)
        => clientStates[(int)side, (int)type];

    public void ResetClientStates()
    {
        for (int s = 0; s < 2; s++)
        for (int t = 0; t < Balance.PowerUpCount; t++)
        {
            if (clientStates[s, t] != PowerUpState.Inactive)
            {
                clientStates[s, t] = PowerUpState.Inactive;
                onPowerUpDeactivated?.Raise(new PowerUpEventData
                {
                    type = (PowerUpType)t,
                    side = (PlayerSide)s
                });
            }
        }
        onBallVanishStateChanged?.Raise(false);
    }

    private static bool IsValidPlayerSide(byte side) => side < 2;
    private static bool IsValidPowerUpType(byte type) => type < Balance.PowerUpCount;
    private static bool IsValidPowerUpState(byte state) => state <= (byte)PowerUpState.Active;

    private static bool IsSentByMasterClient(PhotonMessageInfo info)
        => PhotonNetwork.MasterClient != null
        && info.Sender.ActorNumber == PhotonNetwork.MasterClient.ActorNumber;
}
