using System;
using UnityEngine;
using PongKong.Systems.AI;

[CreateAssetMenu(menuName = "Events/PowerUpPrimed Event")]
public class PowerUpPrimedGameEvent : ScriptableObject
{
    // The dev plan states OnPowerUpPrimed has a payload of: PowerUpType, PlayerSide
    public event Action<PowerUpType, PlayerSide> OnRaised;

    public void Raise(PowerUpType powerUp, PlayerSide side)
    {
        OnRaised?.Invoke(powerUp, side);
    }
}
