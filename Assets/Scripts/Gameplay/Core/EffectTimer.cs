namespace TinyRoadTrip.Gameplay.Core
{
    // Countdown for timed power-ups. Re-starting while running refreshes to the full duration (no stacking).
    public struct EffectTimer
    {
        public float Duration { get; private set; }
        public float Remaining { get; private set; }

        public bool IsRunning => Remaining > 0f;
        public float Normalized => Duration > 0f ? Remaining / Duration : 0f;

        public void Start(float duration)
        {
            Duration = duration > 0f ? duration : 0f;
            Remaining = Duration;
        }

        // Returns true on the tick the timer reaches zero.
        public bool Tick(float deltaTime)
        {
            if (Remaining <= 0f) return false;
            Remaining -= deltaTime;
            if (Remaining > 0f) return false;
            Remaining = 0f;
            return true;
        }

        public void Stop()
        {
            Remaining = 0f;
        }
    }
}
