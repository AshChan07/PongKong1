using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Game Event")]
public class GameEvent : ScriptableObject
{
    public event Action OnRaised;

    public void Raise()
    {
        OnRaised?.Invoke();
    }
}
