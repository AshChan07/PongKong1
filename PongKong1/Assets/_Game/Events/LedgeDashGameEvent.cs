using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/LedgeDash Event")]
public class LedgeDashGameEvent : ScriptableObject
{
    public event Action<LedgeDashData> OnRaised;

    public void Raise(LedgeDashData data)
    {
        OnRaised?.Invoke(data);
    }
}
