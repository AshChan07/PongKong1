using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/PowerUpData Event")]
public class PowerUpDataGameEvent : ScriptableObject
{
    public event Action<PowerUpEventData> OnRaised;

    public void Raise(PowerUpEventData data)
    {
        OnRaised?.Invoke(data);
    }
}
