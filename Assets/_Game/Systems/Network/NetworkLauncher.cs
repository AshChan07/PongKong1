using System.Security.Cryptography;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class NetworkLauncher : MonoBehaviourPunCallbacks
{
    [Header("Config")]
    [SerializeField] private string gameSceneName = "Game Scene";

    [Header("Events")]
    [SerializeField] private NetworkConnectionGameEvent onConnectionStateChanged;

    public static NetworkLauncher Instance { get; private set; }
    public NetworkConnectionState State { get; private set; } = NetworkConnectionState.Disconnected;
    public string CurrentRoomCode { get; private set; }

    private enum PendingAction { None, QuickMatch, CreatePrivate, JoinPrivate }
    private PendingAction pendingAction = PendingAction.None;
    private string pendingRoomCode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.SerializationRate = NetworkConfig.BallSendRate;
        PhotonNetwork.SendRate = 60;
    }

    public void QuickMatch()
    {
        pendingAction = PendingAction.QuickMatch;
        ConnectIfNeeded();
    }

    public string CreatePrivateRoom()
    {
        string code = GenerateRoomCode();
        CurrentRoomCode = code;
        pendingRoomCode = code;
        pendingAction = PendingAction.CreatePrivate;
        ConnectIfNeeded();
        return code;
    }

    public void JoinPrivateRoom(string code)
    {
        pendingRoomCode = code.ToUpperInvariant();
        pendingAction = PendingAction.JoinPrivate;
        ConnectIfNeeded();
    }

    public void DisconnectFromPhoton()
    {
        pendingAction = PendingAction.None;
        PhotonNetwork.Disconnect();
    }

    private void ConnectIfNeeded()
    {
        if (PhotonNetwork.IsConnected)
        {
            ExecutePending();
            return;
        }
        SetState(NetworkConnectionState.Connecting);
        PhotonNetwork.ConnectUsingSettings();
    }

    private void ExecutePending()
    {
        if (pendingAction == PendingAction.None) return;
        SetState(NetworkConnectionState.Joining);

        switch (pendingAction)
        {
            case PendingAction.QuickMatch:
                pendingAction = PendingAction.None;
                PhotonNetwork.JoinRandomRoom();
                break;

            case PendingAction.CreatePrivate:
                pendingAction = PendingAction.None;
                var createOpts = new RoomOptions
                {
                    MaxPlayers  = NetworkConfig.MaxPlayers,
                    IsVisible   = false,
                    IsOpen      = true,
                    PlayerTtl   = NetworkConfig.PlayerTtlMs,
                    EmptyRoomTtl = NetworkConfig.EmptyRoomTtlMs,
                    CustomRoomProperties = new Hashtable { { "code", pendingRoomCode } },
                    CustomRoomPropertiesForLobby = new[] { "code" }
                };
                PhotonNetwork.CreateRoom(pendingRoomCode, createOpts);
                break;

            case PendingAction.JoinPrivate:
                pendingAction = PendingAction.None;
                PhotonNetwork.JoinRoom(pendingRoomCode);
                break;
        }
    }

    public override void OnConnectedToMaster()
    {
        SetState(NetworkConnectionState.ConnectedToMaster);
        ExecutePending();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        var opts = new RoomOptions
        {
            MaxPlayers   = NetworkConfig.MaxPlayers,
            IsVisible    = true,
            PlayerTtl    = NetworkConfig.PlayerTtlMs,
            EmptyRoomTtl = NetworkConfig.EmptyRoomTtlMs
        };
        PhotonNetwork.CreateRoom(null, opts);
    }

    public override void OnJoinedRoom()
    {
        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        CurrentRoomCode = props.ContainsKey("code")
            ? (string)props["code"]
            : PhotonNetwork.CurrentRoom.Name;

        SetState(NetworkConnectionState.InRoom);

        if (PhotonNetwork.CurrentRoom.PlayerCount == NetworkConfig.MaxPlayers)
            TryStartMatch();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount == NetworkConfig.MaxPlayers)
            TryStartMatch();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        SetState(NetworkConnectionState.InRoom);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SetState(NetworkConnectionState.Disconnected);
        NetworkContext.Reset();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[NetworkLauncher] JoinRoom failed ({returnCode}): {message}");
#endif
        SetState(NetworkConnectionState.ConnectedToMaster);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[NetworkLauncher] CreateRoom failed ({returnCode}): {message}");
#endif
        SetState(NetworkConnectionState.ConnectedToMaster);
    }

    private void TryStartMatch()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        SetState(NetworkConnectionState.Starting);
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.LoadLevel(gameSceneName);
    }

    private void SetState(NetworkConnectionState state)
    {
        State = state;
        onConnectionStateChanged?.Raise(state);
    }

    private static string GenerateRoomCode()
    {
        char[] alphabet = NetworkConfig.RoomCodeAlphabet.ToCharArray();
        char[] code = new char[NetworkConfig.RoomCodeLength];
        for (int i = 0; i < NetworkConfig.RoomCodeLength; i++)
            code[i] = alphabet[SecureRandomIndex(alphabet.Length)];
        return new string(code);
    }

    private static int SecureRandomIndex(int upperBound)
    {
        byte[] value = new byte[1];
        int fairLimit = byte.MaxValue + 1 - ((byte.MaxValue + 1) % upperBound);

        do
        {
            RandomNumberGenerator.Fill(value);
        }
        while (value[0] >= fairLimit);

        return value[0] % upperBound;
    }
}
