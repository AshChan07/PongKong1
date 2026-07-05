using System;
using UnityEngine;
using PongKong.Systems.AI;

[CreateAssetMenu(menuName = "Events/AIAction Event")]
public class AIActionGameEvent : ScriptableObject
{
    public event Action<AIAction> OnRaised;

    public void Raise(AIAction action)
    {
        OnRaised?.Invoke(action);
    }
}
