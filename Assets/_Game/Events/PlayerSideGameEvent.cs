using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/PlayerSide Event")]
public class PlayerSideGameEvent : ScriptableObject
{
    public event Action<PlayerSide> OnRaised;

    public void Raise(PlayerSide side)
    {
        OnRaised?.Invoke(side);
    }
}
