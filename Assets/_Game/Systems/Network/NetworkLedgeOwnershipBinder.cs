using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class NetworkLedgeOwnershipBinder : MonoBehaviour
{
    [SerializeField] private PlayerSide side;
    [SerializeField] private GameEvent onNetworkHostInitiated;
    [SerializeField] private GameEvent onNetworkClientInitiated;

    private PhotonView photonView;

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
    }

    private void OnEnable()
    {
        if (onNetworkHostInitiated != null)
            onNetworkHostInitiated.OnRaised += AssignOwnershipForCurrentRole;
        if (onNetworkClientInitiated != null)
            onNetworkClientInitiated.OnRaised += AssignOwnershipForCurrentRole;
    }

    private void OnDisable()
    {
        if (onNetworkHostInitiated != null)
            onNetworkHostInitiated.OnRaised -= AssignOwnershipForCurrentRole;
        if (onNetworkClientInitiated != null)
            onNetworkClientInitiated.OnRaised -= AssignOwnershipForCurrentRole;
    }

    private void AssignOwnershipForCurrentRole()
    {
        if (NetworkContext.Role == NetworkRole.Offline) return;
        if (NetworkContext.LocalSide != side) return;
        if (photonView != null && !photonView.IsMine)
            photonView.RequestOwnership();
    }
}
