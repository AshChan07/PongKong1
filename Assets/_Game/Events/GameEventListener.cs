using UnityEngine;
using UnityEngine.Events;

public class GameEventListener : MonoBehaviour
{
    [SerializeField] private GameEvent gameEvent;
    [SerializeField] private UnityEvent response;

    private void OnEnable()
    {
        if (gameEvent != null)
            gameEvent.OnRaised += OnEventRaised;
    }

    private void OnDisable()
    {
        if (gameEvent != null)
            gameEvent.OnRaised -= OnEventRaised;
    }

    private void OnEventRaised()
    {
        response?.Invoke();
    }
}
