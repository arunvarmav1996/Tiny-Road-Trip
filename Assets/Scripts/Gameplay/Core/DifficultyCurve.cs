using System;

namespace TinyRoadTrip.Gameplay.Core
{
    // Run difficulty from elapsed time. Base level stays at 0 for a grace period (single cars, slow), then eases in:
    // with x = (t - Grace) / RampTime, level = x^2 / (1 + x^2). That starts flat, reaches 0.5 after one RampTime
    // and keeps creeping towards 1, so the run gets harder gradually instead of all in the first half-minute.
    // On top, traffic comes in waves: a busy phase that tightens towards its end, then a short calm phase
    // with fewer cars. Speed follows only the base level so cars already on the road never jump in speed.
    [Serializable]
    public sealed class DifficultyCurve
    {
        // Oncoming traffic must stay faster than the road (BackgroundScroller.scrollSpeed), even a varied car at
        // StartSpeed * (1 - TrafficVariety.SpeedSpread), or it looks like it is reversing.
        public float StartSpeed = 4f;
        public float MaxSpeed = 6.5f;
        public float StartInterval = 1.4f;
        public float MinInterval = 0.75f;
        public float Grace = 20f;
        public float RampTime = 90f;

        public float WarmUp = 12f;          // no calm phase before this
        public float BusyDuration = 20f;
        public float CalmDuration = 6f;
        public float BusyPeakIntervalMultiplier = 0.85f; // end of each busy phase: tighter spacing
        public float CalmIntervalMultiplier = 1.9f;      // calm phase: fewer cars

        // 0 through the grace period, then easing towards 1 as the run goes on.
        public float Level(float time)
        {
            float t = time - Grace;
            if (t <= 0f) return 0f;
            if (RampTime <= 0f) return 1f;
            float x = t / RampTime;
            return x * x / (1f + x * x);
        }

        public float Speed(float time)
        {
            return Lerp(StartSpeed, MaxSpeed, Level(time));
        }

        public bool IsCalm(float time)
        {
            return WavePhase(time, out _) == Phase.Calm;
        }

        public float SpawnInterval(float time)
        {
            float interval = Lerp(StartInterval, MinInterval, Level(time));
            switch (WavePhase(time, out float progress))
            {
                case Phase.Busy: return interval * Lerp(1f, BusyPeakIntervalMultiplier, progress);
                case Phase.Calm: return interval * CalmIntervalMultiplier;
                default: return interval;
            }
        }

        private enum Phase { WarmUp, Busy, Calm }

        // progress: 0..1 through the current busy phase.
        private Phase WavePhase(float time, out float progress)
        {
            progress = 0f;
            float cycle = BusyDuration + CalmDuration;
            if (time < WarmUp || BusyDuration <= 0f || cycle <= 0f) return Phase.WarmUp;

            float t = (time - WarmUp) % cycle;
            if (t < BusyDuration)
            {
                progress = t / BusyDuration;
                return Phase.Busy;
            }
            return Phase.Calm;
        }

        private static float Lerp(float a, float b, float t)
        {
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            return a + (b - a) * t;
        }
    }
}
