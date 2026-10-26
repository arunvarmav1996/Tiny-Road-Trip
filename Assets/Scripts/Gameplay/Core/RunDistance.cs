using System;
using System.Globalization;

namespace TinyRoadTrip.Gameplay.Core
{
    // Distance travelled in one run: steady travel (scaled by world speed) plus bonus metres (near misses).
    public sealed class RunDistance
    {
        public double TravelMeters { get; private set; }
        public double BonusMeters { get; private set; }
        public double TotalMeters => TravelMeters + BonusMeters;

        public void AddTravel(float deltaTime, float metersPerSecond, float speedMultiplier)
        {
            if (deltaTime <= 0f || metersPerSecond <= 0f || speedMultiplier <= 0f) return;
            TravelMeters += (double)deltaTime * metersPerSecond * speedMultiplier;
        }

        public void AddBonus(double meters)
        {
            if (meters > 0) BonusMeters += meters;
        }
    }

    public static class DistanceFormat
    {
        // 1234 m -> "1.23" (kilometres, two decimals, always '.' as separator).
        public static string Kilometers(double meters)
        {
            double km = Math.Floor(Math.Max(0, meters) / 10.0) / 100.0; // floor so the HUD never rounds up past the real distance
            return km.ToString("0.00", CultureInfo.InvariantCulture);
        }

        // Value the HUD text depends on; the text only needs rebuilding when this changes (every 10 m).
        public static long DisplayStep(double meters) => (long)Math.Floor(Math.Max(0, meters) / 10.0);
    }

    public readonly struct BestResult
    {
        public readonly double Best;
        public readonly bool IsNewBest;

        public BestResult(double best, bool isNewBest)
        {
            Best = best;
            IsNewBest = isNewBest;
        }
    }

    public static class BestScore
    {
        // A run only counts as a new best when it beats the old one; the first ever run (best 0) counts if it moved at all.
        public static BestResult Submit(double runMeters, double previousBest)
        {
            if (runMeters > previousBest) return new BestResult(runMeters, true);
            return new BestResult(previousBest, false);
        }
    }
}
