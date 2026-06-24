using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Bool Event")]
public class BoolGameEvent : ScriptableObject
{
    public event Action<bool> OnRaised;

    public void Raise(bool value)
    {
        OnRaised?.Invoke(value);
    }
}
