public static class Balance
{
    public const int DefaultLives = 3;

    public static readonly int[] PowerUpCosts = { 1, 1, 2, 4 };
    public static readonly int PowerUpCount = System.Enum.GetValues(typeof(PowerUpType)).Length;

    public const int BaseMissPoints = 2;
    public const int DestroyerMissPoints = 4;
    public const int BrokenLedgePoints = 4;
    public const int DestroyerBrokenPoints = 8;
    public const int VanishMissPoints = 8;
    public const int VanishBrokenPoints = 16;
    public const int VanishPenalty = 1;
}
