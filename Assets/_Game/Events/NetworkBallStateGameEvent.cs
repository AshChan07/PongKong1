using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Network Ball State Event")]
public class NetworkBallStateGameEvent : ScriptableObject
{
    public event Action<NetworkBallStateData> OnRaised;

    public void Raise(NetworkBallStateData data)
    {
        OnRaised?.Invoke(data);
    }
}
