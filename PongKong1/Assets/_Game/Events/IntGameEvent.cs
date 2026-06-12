using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Int Event")]
public class IntGameEvent : ScriptableObject
{
    public event Action<int> OnRaised;

    public void Raise(int value)
    {
        OnRaised?.Invoke(value);
    }
}
