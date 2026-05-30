namespace Marble
{
    public static class MarbleConstants
    {
        public const int MaxMarbleCount = 16;
        public static int MarbleCount = 2;
        public const int TotalLaps = 2;
        public const float CountdownDuration = 3f;

        public const float MarbleRadius = 0.34f;

        public const float SyncRate = 30f;
        public const float SyncInterval = 1f / SyncRate;

        public const float InterpolationDelay = 0.1f;
        public const float MaxExtrapolationTime = 0.2f;

        public const float ProgressToFixed = 65535f;
        public const float FixedToProgress = 1f / 65535f;
        public const float SpeedToFixed = 1000f;
        public const float FixedToSpeed = 1f / 1000f;

        public const byte StatusFinished = 0x01;
        public const byte StatusEliminated = 0x02;

        public const string MarbleTag = "Marble";
    }
}
