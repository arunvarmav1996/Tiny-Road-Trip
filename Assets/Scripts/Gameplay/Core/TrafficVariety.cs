using System;
using System.Collections.Generic;

namespace TinyRoadTrip.Gameplay.Core
{
    // A car on the road as seen by the traffic rules: its lane and world Y.
    public struct LaneCar
    {
        public int Lane;
        public float Y;

        public LaneCar(int lane, float y)
        {
            Lane = lane;
            Y = y;
        }
    }

    // Per-car speed variety and lane-changers for single cars. Both unlock as the difficulty level (0..1) rises.
    // Fairness: cars in one lane never touch (a faster car slows to the speed of the car ahead once it is within
    // FollowGap), a varied car never completes a row that leaves fewer than TrafficPatterns.MinOpenLanes lanes,
    // and a lane change only happens into a clear lane and only if the row keeps its way through.
    // Lane changes are measured in distance travelled, so the warning is the same length at any speed.
    [Serializable]
    public sealed class TrafficVariety
    {
        public float VarietyUnlockLevel = 0.1f;
        public float MaxVariedChance = 0.5f;      // chance a single car gets its own speed, at level 1
        public float SpeedSpread = 0.2f;          // varied car speed: base * [1 - spread, 1 + spread]

        public float LaneChangeUnlockLevel = 0.3f;
        public float MaxLaneChangeChance = 0.3f;  // chance a single car changes lane, at level 1
        public float LaneChangeStartYMax = 3.4f;  // signalling starts somewhere between these world Ys
        public float LaneChangeStartYMin = 2.6f;
        public float SignalDistance = 2.4f;       // world units travelled while blinking before moving
        public float MoveDistance = 1.3f;         // world units travelled while sliding into the next lane
        public float LaneClearance = 2.4f;        // target lane must have no car within this vertical distance

        public float FollowGap = 1.7f;            // centre-to-centre distance at which a faster car matches speed
        public float RowBand = 1.4f;              // cars closer than this vertically count as one row
        public float RowGuardMargin = 0.6f;       // a varied car matches speed this far before it would join a row

        // Speed multiplier for a new single car: 1, or a random factor around 1.
        public float RollSpeedFactor(float level, double rChance, double rFactor)
        {
            if (rChance >= Chance(level, VarietyUnlockLevel, MaxVariedChance)) return 1f;
            return 1f - SpeedSpread + 2f * SpeedSpread * (float)rFactor;
        }

        public bool RollLaneChange(float level, double r01)
        {
            return r01 < Chance(level, LaneChangeUnlockLevel, MaxLaneChangeChance);
        }

        public float RollStartY(double r01)
        {
            return LaneChangeStartYMin + (LaneChangeStartYMax - LaneChangeStartYMin) * (float)r01;
        }

        // Edge lanes turn inwards; others pick a side at random.
        public static int PickTargetLane(int lane, int laneCount, double r01)
        {
            if (laneCount < 2) return lane;
            if (lane <= 0) return 1;
            if (lane >= laneCount - 1) return laneCount - 2;
            return r01 < 0.5 ? lane - 1 : lane + 1;
        }

        // Speed a car should drive at given the car ahead in its lane (gap = centre-to-centre distance).
        public float FollowSpeed(float speed, float aheadSpeed, float gap)
        {
            return gap <= FollowGap && speed > aheadSpeed ? aheadSpeed : speed;
        }

        // Number of distinct lanes occupied by cars within band of y (skip = index to ignore, -1 for none),
        // plus extraLane if it is not already counted (-1 for none). Lanes must be below 32.
        public static int CountBlockedLanes(IReadOnlyList<LaneCar> cars, int skip, float y, float band, int extraLane)
        {
            uint mask = extraLane >= 0 ? 1u << extraLane : 0u;
            for (int i = 0; i < cars.Count; i++)
            {
                if (i == skip) continue;
                if (Math.Abs(cars[i].Y - y) < band) mask |= 1u << cars[i].Lane;
            }
            int count = 0;
            for (; mask != 0; mask &= mask - 1) count++;
            return count;
        }

        public static bool LeavesWayThrough(int blockedLanes, int laneCount)
        {
            return laneCount - blockedLanes >= TrafficPatterns.MinOpenLanes;
        }

        // True if the car at index self may move into targetLane right now.
        public bool CanChangeLane(IReadOnlyList<LaneCar> cars, int self, int targetLane, int laneCount)
        {
            float y = cars[self].Y;
            for (int i = 0; i < cars.Count; i++)
                if (i != self && cars[i].Lane == targetLane && Math.Abs(cars[i].Y - y) < LaneClearance) return false;
            return LeavesWayThrough(CountBlockedLanes(cars, self, y, RowBand, targetLane), laneCount);
        }

        // True if the varied car at index self is about to complete a row with no way through.
        public bool WouldCloseRow(IReadOnlyList<LaneCar> cars, int self, int laneCount)
        {
            int blocked = CountBlockedLanes(cars, self, cars[self].Y, RowBand + RowGuardMargin, cars[self].Lane);
            return !LeavesWayThrough(blocked, laneCount);
        }

        private static float Chance(float level, float unlock, float max)
        {
            if (level <= unlock || unlock >= 1f) return 0f;
            return max * (level - unlock) / (1f - unlock);
        }
    }
}
