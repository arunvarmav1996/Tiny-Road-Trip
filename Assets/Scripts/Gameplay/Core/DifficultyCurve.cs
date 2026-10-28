using System;

namespace TinyRoadTrip.Gameplay.Core
{
    // Run difficulty from elapsed time. Base level rises fast early and levels off (1 - e^(-t/rampTime)).
    // On top, traffic comes in waves: a busy phase that tightens towards its end, then a short calm phase
    // with fewer cars. Speed follows only the base level so cars already on the road never jump in speed.
    [Serializable]
    public sealed class DifficultyCurve
    {
        public float StartSpeed = 2.5f;
        public float MaxSpeed = 5.5f;
        public float StartInterval = 1.4f;
        public float MinInterval = 0.75f;
        public float RampTime = 45f;

        public float WarmUp = 12f;          // no calm phase before this
        public float BusyDuration = 20f;
        public float CalmDuration = 6f;
        public float BusyPeakIntervalMultiplier = 0.85f; // end of each busy phase: tighter spacing
        public float CalmIntervalMultiplier = 1.9f;      // calm phase: fewer cars

        // 0 at the start, approaching 1 as the run goes on.
        public float Level(float time)
        {
            if (time <= 0f || RampTime <= 0f) return time > 0f ? 1f : 0f;
            return 1f - (float)Math.Exp(-time / RampTime);
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
