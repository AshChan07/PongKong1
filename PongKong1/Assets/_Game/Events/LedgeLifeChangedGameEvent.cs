using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/LedgeLifeChanged Event")]
public class LedgeLifeChangedGameEvent : ScriptableObject
{
    public event Action<LedgeLifeChangedData> OnRaised;

    public void Raise(LedgeLifeChangedData data)
    {
        OnRaised?.Invoke(data);
    }
}
