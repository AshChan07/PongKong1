using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class NetworkBallSync : MonoBehaviourPun, IPunObservable
{
    [Header("Scene References")]
    [SerializeField] private BallController ball;

    [Header("Events — wire the same assets as BallController")]
    [SerializeField] private Vector2GameEvent onBallLaunched;
    [SerializeField] private GameEvent onBallHitBoundary;
    [SerializeField] private PlayerSideGameEvent onBallEnteredCourt;
    [SerializeField] private PlayerSideGameEvent onBallExitedCourt;
    [SerializeField] private ContactDataGameEvent onBallHitLedge;
    [SerializeField] private Vector2GameEvent onBallPositionUpdated;
    [SerializeField] private BoolGameEvent onBallFreezeRequested;
    [SerializeField] private GameEvent onNetworkMasterMigrated;
    [SerializeField] private NetworkBallStateGameEvent onNetworkHostTakeoverRequested;

    private struct Snapshot
    {
        public double time;
        public Vector2 pos;
        public Vector2 vel;
    }

    private const int MaxSnapshots = 12;
    private readonly Snapshot[] snapshots = new Snapshot[MaxSnapshots];
    private int snapshotStart;
    private int snapshotCount;

    private Vector2 lastKnownPos;
    private Vector2 lastKnownVel;
    private Vector2 renderPos;

    private bool netIsVisible;
    private bool netIsDestroyer;
    private bool netIsVanish;
    private bool netHasCourt;
    private PlayerSide netCourt;

    private Rigidbody2D rb;
    private bool hostSubscribed;

    private void Awake()
    {
        rb = ball.GetComponent<Rigidbody2D>();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(ball.GetPosition());
            stream.SendNext(ball.GetVelocity());
            stream.SendNext(PackFlags());
        }
        else
        {
            var pos   = (Vector2)stream.ReceiveNext();
            var vel   = (Vector2)stream.ReceiveNext();
            var flags = (byte)stream.ReceiveNext();

            UnpackFlags(flags);
            lastKnownPos = pos;
            lastKnownVel = vel;

            PushSnapshot(pos, vel, info.SentServerTime);
        }
    }

    private void OnEnable()
    {
        if (NetworkContext.Role == NetworkRole.Host)
            SubscribeAsHost();
        if (onBallFreezeRequested != null)
            onBallFreezeRequested.OnRaised += HandleBallFreezeRequested;
        if (onNetworkMasterMigrated != null)
            onNetworkMasterMigrated.OnRaised += HandleNetworkMasterMigrated;
    }

    public void SubscribeAsHost()
    {
        if (hostSubscribed) return;
        hostSubscribed = true;
        if (onBallLaunched != null)     onBallLaunched.OnRaised     += Host_OnBallLaunched;
        if (onBallHitBoundary != null)  onBallHitBoundary.OnRaised  += Host_OnBallHitBoundary;
        if (onBallEnteredCourt != null) onBallEnteredCourt.OnRaised += Host_OnBallEnteredCourt;
        if (onBallExitedCourt != null)  onBallExitedCourt.OnRaised  += Host_OnBallExitedCourt;
        if (onBallHitLedge != null)     onBallHitLedge.OnRaised     += Host_OnBallHitLedge;
    }

    private void OnDisable()
    {
        hostSubscribed = false;
        if (onBallLaunched != null)       onBallLaunched.OnRaised       -= Host_OnBallLaunched;
        if (onBallHitBoundary != null)    onBallHitBoundary.OnRaised    -= Host_OnBallHitBoundary;
        if (onBallEnteredCourt != null)   onBallEnteredCourt.OnRaised   -= Host_OnBallEnteredCourt;
        if (onBallExitedCourt != null)    onBallExitedCourt.OnRaised    -= Host_OnBallExitedCourt;
        if (onBallHitLedge != null)       onBallHitLedge.OnRaised       -= Host_OnBallHitLedge;
        if (onBallFreezeRequested != null)
            onBallFreezeRequested.OnRaised -= HandleBallFreezeRequested;
        if (onNetworkMasterMigrated != null)
            onNetworkMasterMigrated.OnRaised -= HandleNetworkMasterMigrated;
    }

    private void Host_OnBallLaunched(Vector2 dir)
        => photonView.RPC(nameof(RPC_BallLaunched), RpcTarget.Others, dir.x, dir.y);

    private void Host_OnBallHitBoundary()
        => photonView.RPC(nameof(RPC_BallHitBoundary), RpcTarget.Others);

    private void Host_OnBallEnteredCourt(PlayerSide side)
        => photonView.RPC(nameof(RPC_BallEnteredCourt), RpcTarget.Others, (byte)side);

    private void Host_OnBallExitedCourt(PlayerSide side)
        => photonView.RPC(nameof(RPC_BallExitedCourt), RpcTarget.Others, (byte)side);

    private void Host_OnBallHitLedge(ContactData data)
        => photonView.RPC(nameof(RPC_BallHitLedge), RpcTarget.Others,
            data.incidentDirection.x, data.incidentDirection.y,
            data.contactPoint.x, data.contactPoint.y,
            data.hitOffset,
            (byte)data.ledgeSide);

    [PunRPC]
    private void RPC_BallLaunched(float dx, float dy, PhotonMessageInfo info)
    {
        if (!IsSentByMasterClient(info)) return;
        onBallLaunched?.Raise(new Vector2(dx, dy));
    }

    [PunRPC]
    private void RPC_BallHitBoundary(PhotonMessageInfo info)
    {
        if (!IsSentByMasterClient(info)) return;
        onBallHitBoundary?.Raise();
    }

    [PunRPC]
    private void RPC_BallEnteredCourt(byte side, PhotonMessageInfo info)
    {
        if (!IsSentByMasterClient(info)) return;
        if (!IsValidPlayerSide(side)) return;
        onBallEnteredCourt?.Raise((PlayerSide)side);
    }

    [PunRPC]
    private void RPC_BallExitedCourt(byte side, PhotonMessageInfo info)
    {
        if (!IsSentByMasterClient(info)) return;
        if (!IsValidPlayerSide(side)) return;
        onBallExitedCourt?.Raise((PlayerSide)side);
    }

    [PunRPC]
    private void RPC_BallHitLedge(float incX, float incY, float cpX, float cpY, float hitOffset, byte side, PhotonMessageInfo info)
    {
        if (!IsSentByMasterClient(info)) return;
        if (!IsValidPlayerSide(side)) return;

        var data = new ContactData
        {
            incidentDirection = new Vector2(incX, incY),
            contactPoint      = new Vector2(cpX, cpY),
            hitOffset         = hitOffset,
            ledgeSide         = (PlayerSide)side
        };
        onBallHitLedge?.Raise(data);
    }

    private void Update()
    {
        if (NetworkContext.Role != NetworkRole.Client) return;

        ApplyFlags();
        InterpolateBall();
    }

    private void ApplyFlags()
    {
        ball.SetVisible(netIsVisible);
        ball.SetDestroyerActive(netIsDestroyer);
        ball.SetVanishActive(netIsVanish);
    }

    private void InterpolateBall()
    {
        double renderTime = PhotonNetwork.Time - NetworkConfig.InterpolationBufferSec;

        if (snapshotCount < 2)
        {
            if (snapshotCount == 1)
            {
                var s = GetSnapshot(0);
                float dt = (float)(PhotonNetwork.Time - s.time);
                renderPos = s.pos + s.vel * dt;
            }
        }
        else
        {
            Snapshot older = default, newer = default;
            bool found = false;

            for (int i = 0; i < snapshotCount - 1; i++)
            {
                Snapshot current = GetSnapshot(i);
                Snapshot next = GetSnapshot(i + 1);
                if (current.time <= renderTime && next.time >= renderTime)
                {
                    older = current;
                    newer = next;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                double span = newer.time - older.time;
                float t = span > 0.0001 ? (float)((renderTime - older.time) / span) : 1f;
                renderPos = Vector2.Lerp(older.pos, newer.pos, t);
            }
            else if (snapshotCount > 0)
            {
                var newest = GetSnapshot(snapshotCount - 1);
                float dt = (float)(PhotonNetwork.Time - newest.time);
                renderPos = newest.pos + newest.vel * Mathf.Min(dt, 0.1f);
            }
        }

        if (Vector2.Distance(renderPos, lastKnownPos) > NetworkConfig.DesyncSnapThreshold)
        {
            renderPos = Vector2.MoveTowards(renderPos, lastKnownPos,
                NetworkConfig.DesyncSnapThreshold * Time.deltaTime / NetworkConfig.ReconciliationSmoothSec);
        }

        rb.position = renderPos;
        onBallPositionUpdated?.Raise(renderPos);
    }

    public void SetInterpolationActive(bool active)
    {
        enabled = active;
        if (!active) ClearSnapshots();
    }

    private void HandleBallFreezeRequested(bool freeze)
    {
        if (NetworkContext.Role != NetworkRole.Client) return;
        SetInterpolationActive(!freeze);
    }

    public (Vector2 pos, Vector2 vel) GetLastKnownState() => (lastKnownPos, lastKnownVel);

    private void HandleNetworkMasterMigrated()
    {
        if (NetworkContext.Role != NetworkRole.Host) return;

        SubscribeAsHost();
        onNetworkHostTakeoverRequested?.Raise(new NetworkBallStateData
        {
            position = lastKnownPos,
            velocity = lastKnownVel
        });
    }

    private void PushSnapshot(Vector2 pos, Vector2 vel, double serverTime)
    {
        int index = (snapshotStart + snapshotCount) % MaxSnapshots;
        snapshots[index] = new Snapshot { time = serverTime, pos = pos, vel = vel };

        if (snapshotCount == MaxSnapshots)
            snapshotStart = (snapshotStart + 1) % MaxSnapshots;
        else
            snapshotCount++;
    }

    private Snapshot GetSnapshot(int offset)
    {
        return snapshots[(snapshotStart + offset) % MaxSnapshots];
    }

    private void ClearSnapshots()
    {
        snapshotStart = 0;
        snapshotCount = 0;
    }

    private byte PackFlags()
    {
        byte f = 0;
        if (ball.IsVisible)         f |= 1;
        if (ball.IsDestroyerActive) f |= 2;
        if (ball.IsVanishActive)    f |= 4;
        if (ball.HasCourt)          f |= 8;
        if (ball.HasCourt && ball.CurrentCourt == PlayerSide.P2) f |= 16;
        return f;
    }

    private void UnpackFlags(byte f)
    {
        netIsVisible  = (f & 1)  != 0;
        netIsDestroyer = (f & 2) != 0;
        netIsVanish   = (f & 4)  != 0;
        netHasCourt   = (f & 8)  != 0;
        netCourt      = (f & 16) != 0 ? PlayerSide.P2 : PlayerSide.P1;
    }

    private static bool IsValidPlayerSide(byte side) => side < 2;

    private static bool IsSentByMasterClient(PhotonMessageInfo info)
        => PhotonNetwork.MasterClient != null
        && info.Sender.ActorNumber == PhotonNetwork.MasterClient.ActorNumber;
}
