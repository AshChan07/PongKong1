using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Network Connection Event")]
public class NetworkConnectionGameEvent : ScriptableObject
{
    public event Action<NetworkConnectionState> OnRaised;

    public void Raise(NetworkConnectionState state)
    {
        OnRaised?.Invoke(state);
    }
}
