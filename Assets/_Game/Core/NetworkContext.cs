using UnityEngine;

public static class NetworkContext
{
    public static NetworkRole Role { get; private set; } = NetworkRole.Offline;
    public static PlayerSide LocalSide { get; private set; } = PlayerSide.P1;

    public static void SetRole(NetworkRole role, PlayerSide localSide = PlayerSide.P1)
    {
        Role = role;
        LocalSide = localSide;
    }

    // Runs before every scene load on play start, so stale static state can't
    // survive "Enter Play Mode without domain reload". Also called on disconnect.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Role = NetworkRole.Offline;
        LocalSide = PlayerSide.P1;
    }
}
