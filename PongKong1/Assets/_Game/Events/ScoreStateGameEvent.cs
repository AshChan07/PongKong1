using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/ScoreState Event")]
public class ScoreStateGameEvent : ScriptableObject
{
    public event Action<ScoreStateData> OnRaised;

    public void Raise(ScoreStateData data)
    {
        OnRaised?.Invoke(data);
    }
}
