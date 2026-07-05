using System;
using UnityEngine;
using PongKong.Systems.AI;

[CreateAssetMenu(menuName = "Events/PowerUpPrimed Event")]
public class PowerUpPrimedGameEvent : ScriptableObject
{
    
    public event Action<PowerUpType, PlayerSide> OnRaised;

    public void Raise(PowerUpType powerUp, PlayerSide side)
    {
        OnRaised?.Invoke(powerUp, side);
    }
}
