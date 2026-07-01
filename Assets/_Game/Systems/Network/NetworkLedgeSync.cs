using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class NetworkLedgeSync : MonoBehaviourPun, IPunObservable
{
    private Rigidbody2D rb;

    private struct Snapshot
    {
        public double time;
        public float  posY;
        public float  velY;
    }

    private const int MaxSnapshots = 8;
    private readonly Snapshot[] snapshots = new Snapshot[MaxSnapshots];
    private int snapshotStart;
    private int snapshotCount;
    private float renderPosY;
    private float lastKnownY;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(rb.position.y);
            stream.SendNext(rb.linearVelocity.y);
        }
        else
        {
            float posY = (float)stream.ReceiveNext();
            float velY = (float)stream.ReceiveNext();

            lastKnownY = posY;
            PushSnapshot(posY, velY, info.SentServerTime);
        }
    }

    private void Update()
    {
        if (photonView.IsMine) return;
        if (NetworkContext.Role == NetworkRole.Offline) return;

        InterpolateLedge();
    }

    private void InterpolateLedge()
    {
        double renderTime = PhotonNetwork.Time - NetworkConfig.InterpolationBufferSec;

        if (snapshotCount == 0) return;

        if (snapshotCount < 2)
        {
            Snapshot snapshot = GetSnapshot(0);
            float dt = (float)(PhotonNetwork.Time - snapshot.time);
            renderPosY = snapshot.posY + snapshot.velY * dt;
        }
        else
        {
            bool found = false;
            for (int i = 0; i < snapshotCount - 1; i++)
            {
                Snapshot current = GetSnapshot(i);
                Snapshot next = GetSnapshot(i + 1);
                if (current.time <= renderTime && next.time >= renderTime)
                {
                    double span = next.time - current.time;
                    float t = span > 0.0001 ? (float)((renderTime - current.time) / span) : 1f;
                    renderPosY = Mathf.Lerp(current.posY, next.posY, t);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                var newest = GetSnapshot(snapshotCount - 1);
                float dt = (float)(PhotonNetwork.Time - newest.time);
                renderPosY = newest.posY + newest.velY * Mathf.Min(dt, 0.1f);
            }
        }

        if (Mathf.Abs(renderPosY - lastKnownY) > NetworkConfig.DesyncSnapThreshold)
        {
            renderPosY = Mathf.MoveTowards(renderPosY, lastKnownY,
                NetworkConfig.DesyncSnapThreshold * Time.deltaTime / NetworkConfig.ReconciliationSmoothSec);
        }

        rb.MovePosition(new Vector2(rb.position.x, renderPosY));
    }

    private void PushSnapshot(float posY, float velY, double serverTime)
    {
        int index = (snapshotStart + snapshotCount) % MaxSnapshots;
        snapshots[index] = new Snapshot { time = serverTime, posY = posY, velY = velY };

        if (snapshotCount == MaxSnapshots)
            snapshotStart = (snapshotStart + 1) % MaxSnapshots;
        else
            snapshotCount++;
    }

    private Snapshot GetSnapshot(int offset)
    {
        return snapshots[(snapshotStart + offset) % MaxSnapshots];
    }
}
