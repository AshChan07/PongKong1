using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/SpendPoints Event")]
public class SpendPointsGameEvent : ScriptableObject
{
    public event Action<SpendPointsData> OnRaised;

    public void Raise(SpendPointsData data)
    {
        OnRaised?.Invoke(data);
    }
}
