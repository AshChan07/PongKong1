using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/ScoreRequest Event")]
public class ScoreRequestGameEvent : ScriptableObject
{
    public event Action<ScoreRequestData> OnRaised;

    public void Raise(ScoreRequestData data)
    {
        OnRaised?.Invoke(data);
    }
}
