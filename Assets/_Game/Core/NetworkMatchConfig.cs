public enum NetworkMatchMode
{
    QuickMatch,
    Private
}

public struct NetworkMatchConfig
{
    public NetworkMatchMode mode;
    public string roomCode;
}
