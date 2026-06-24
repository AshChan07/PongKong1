using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/LedgeDamage Event")]
public class LedgeDamageGameEvent : ScriptableObject
{
    public event Action<LedgeDamageData> OnRaised;

    public void Raise(LedgeDamageData data)
    {
        OnRaised?.Invoke(data);
    }
}
