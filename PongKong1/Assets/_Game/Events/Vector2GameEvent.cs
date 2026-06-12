using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Vector2 Event")]
public class Vector2GameEvent : ScriptableObject
{
    public event Action<Vector2> OnRaised;

    public void Raise(Vector2 value)
    {
        OnRaised?.Invoke(value);
    }
}
