using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/PointScoredData Event")]
public class PointScoredDataGameEvent : ScriptableObject
{
    public event Action<PointScoredData> OnRaised;

    public void Raise(PointScoredData data)
    {
        OnRaised?.Invoke(data);
    }
}
