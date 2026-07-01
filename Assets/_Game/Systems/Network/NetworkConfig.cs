public static class NetworkConfig
{
    public const int    MaxPlayers              = 2;
    public const int    BallSendRate            = 30;
    public const int    LedgeSendRate           = 30;
    public const float  InterpolationBufferSec  = 0.10f;
    public const float  DesyncSnapThreshold     = 0.5f;
    public const float  ReconciliationSmoothSec = 0.20f;
    public const float  ReconnectWindowSec      = 10f;
    public const string RoomCodeAlphabet        = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int    RoomCodeLength          = 6;
    public const int    PlayerTtlMs             = 10000;
    public const int    EmptyRoomTtlMs          = 10000;
}
