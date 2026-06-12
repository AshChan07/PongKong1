using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/ContactData Event")]
public class ContactDataGameEvent : ScriptableObject
{
    public event Action<ContactData> OnRaised;

    public void Raise(ContactData data)
    {
        OnRaised?.Invoke(data);
    }
}
