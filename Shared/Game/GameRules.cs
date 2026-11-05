namespace FPSPrototype.Shared.Game
{
    public static class GameRules
    {
        public const int ServerTickRate = 30;
        public const int MaxPlayers = 4;
        public const float PlayerHeight = 1.8f;
        public const float PlayerWidth = 0.7f;
        public const float EyeHeight = 1.6f;
        public const float StepHeight = 0.3f;
        // Phase 4 movement policy; hit detection will use independent hitboxes.
        public const bool PlayerMovementCollision = false;
    }
}
