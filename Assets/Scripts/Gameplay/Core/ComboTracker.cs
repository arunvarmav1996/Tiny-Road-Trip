using System;

namespace TinyRoadTrip.Gameplay.Core
{
    public readonly struct NearMissResult
    {
        public readonly int Combo;
        public readonly int Multiplier;
        public readonly int Points;

        public NearMissResult(int combo, int multiplier, int points)
        {
            Combo = combo;
            Multiplier = multiplier;
            Points = points;
        }
    }

    // Consecutive near misses within the timeout build a combo; points = basePoints * min(combo, maxMultiplier).
    public sealed class ComboTracker
    {
        private readonly int basePoints;
        private readonly int maxMultiplier;
        private readonly float timeout;
        private float lastNearMissTime;

        public int Combo { get; private set; }

        public ComboTracker(int basePoints, int maxMultiplier, float timeout)
        {
            if (basePoints < 0) throw new ArgumentOutOfRangeException(nameof(basePoints));
            if (maxMultiplier < 1) throw new ArgumentOutOfRangeException(nameof(maxMultiplier));
            if (timeout <= 0f) throw new ArgumentOutOfRangeException(nameof(timeout));
            this.basePoints = basePoints;
            this.maxMultiplier = maxMultiplier;
            this.timeout = timeout;
        }

        public NearMissResult Register(float now)
        {
            if (Combo > 0 && now - lastNearMissTime > timeout)
                Combo = 0;

            Combo++;
            lastNearMissTime = now;
            int multiplier = Math.Min(Combo, maxMultiplier);
            return new NearMissResult(Combo, multiplier, basePoints * multiplier);
        }

        // Returns true once when an active combo expires.
        public bool Tick(float now)
        {
            if (Combo > 0 && now - lastNearMissTime > timeout)
            {
                Combo = 0;
                return true;
            }
            return false;
        }

        public void Reset()
        {
            Combo = 0;
        }
    }
}
