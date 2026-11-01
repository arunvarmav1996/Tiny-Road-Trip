using System;
using System.Collections.Generic;

namespace TinyRoadTrip.Gameplay.Core
{
    // A car as seen by the gap rule: lane, centre Y and length (its collider's height).
    public struct GapCar
    {
        public int Lane;
        public float Y;
        public float Length;

        public GapCar(int lane, float y, float length)
        {
            Lane = lane;
            Y = y;
            Length = length;
        }
    }

    // Minimum gap rule: any two cars on the road are either side by side in one row (centres within
    // AlignTolerance, different lanes, row still leaves a way through) or separated by a clear window, measured
    // bumper to bumper, of at least the player's length times a factor. The factor eases from EasyGapFactor at the
    // start of a run to HardGapFactor at full difficulty, so the windows tighten gradually. Everything is in
    // player and car lengths, so a longer player car or longer traffic keeps the same feel.
    [Serializable]
    public sealed class TrafficGaps
    {
        public float EasyGapFactor = 1.8f;  // clear window in player lengths at difficulty level 0
        public float HardGapFactor = 1.3f;  // ... at difficulty level 1
        public float AlignTolerance = 0.3f; // centres closer than this count as one row
        public float MaxLift = 12f;         // safety stop for Lift

        // Clear bumper-to-bumper window the player needs at this difficulty level.
        public float MinClearGap(float playerLength, float level)
        {
            if (level < 0f) level = 0f;
            else if (level > 1f) level = 1f;
            return playerLength * (EasyGapFactor + (HardGapFactor - EasyGapFactor) * level);
        }

        // Centre-to-centre distance two cars need for a clear window between them.
        public static float CentreDistance(float lengthA, float lengthB, float clearGap)
        {
            return (lengthA + lengthB) * 0.5f + clearGap;
        }

        // True if two cars break the rule (too close but not a fair side-by-side row pair).
        public bool TooClose(GapCar a, GapCar b, float clearGap)
        {
            float dy = Math.Abs(a.Y - b.Y);
            if (dy <= AlignTolerance) return a.Lane == b.Lane;
            return dy < CentreDistance(a.Length, b.Length, clearGap);
        }

        // How far (>= 0) a group of new cars must be raised so that each one is side by side with, or a clear
        // window away from, every car already on the road, and no row ends up with fewer than
        // TrafficPatterns.MinOpenLanes free lanes. The new cars keep their spacing relative to each other.
        public float Lift(IReadOnlyList<GapCar> existing, IReadOnlyList<GapCar> incoming, float clearGap, int laneCount)
        {
            float lift = 0f;
            for (int pass = 0; pass < 64; pass++)
            {
                float needed = lift;
                for (int n = 0; n < incoming.Count; n++)
                {
                    GapCar car = incoming[n];
                    car.Y += lift;
                    uint rowMask = 1u << car.Lane;
                    for (int e = 0; e < existing.Count; e++)
                    {
                        GapCar other = existing[e];
                        float dy = Math.Abs(car.Y - other.Y);
                        if (dy <= AlignTolerance && other.Lane != car.Lane)
                        {
                            rowMask |= 1u << other.Lane;
                            continue;
                        }
                        if (TooClose(car, other, clearGap))
                            needed = Math.Max(needed, other.Y + CentreDistance(car.Length, other.Length, clearGap) - incoming[n].Y);
                    }
                    for (int m = 0; m < incoming.Count; m++)
                        if (Math.Abs(incoming[m].Y - incoming[n].Y) <= AlignTolerance) rowMask |= 1u << incoming[m].Lane;

                    if (!TrafficVariety.LeavesWayThrough(CountBits(rowMask), laneCount))
                    {
                        // Joining an existing row would close it: move clear of that row instead.
                        for (int e = 0; e < existing.Count; e++)
                            if (Math.Abs(car.Y - existing[e].Y) <= AlignTolerance)
                                needed = Math.Max(needed, existing[e].Y + CentreDistance(car.Length, existing[e].Length, clearGap) - incoming[n].Y);
                    }
                }
                if (needed <= lift) return lift;
                lift = needed;
                if (lift >= MaxLift) return MaxLift;
            }
            return lift;
        }

        private static int CountBits(uint mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1) count++;
            return count;
        }
    }
}
