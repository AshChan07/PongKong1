/// <summary>
/// Lightweight interface so BallController can read ledge side
/// without depending on LedgeController directly.
/// </summary>
public interface ILedgeIdentifier
{
    PlayerSide Side { get; }
}
