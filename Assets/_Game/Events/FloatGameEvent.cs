using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Float Event")]
public class FloatGameEvent : ScriptableObject
{
    public event Action<float> OnRaised;

    public void Raise(float value)
    {
        OnRaised?.Invoke(value);
    }
}
