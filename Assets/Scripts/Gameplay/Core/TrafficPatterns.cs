using System;
using System.Collections.Generic;

namespace TinyRoadTrip.Gameplay.Core
{
    public enum TrafficPatternKind { Single, Pair, Wall, Zigzag }

    // One car of a pattern: its lane and how many rows above the spawn line it starts.
    public struct CarSlot
    {
        public int Lane;
        public int Row;

        public CarSlot(int lane, int row)
        {
            Lane = lane;
            Row = row;
        }
    }

    // Traffic pattern selection and layout. Fairness rule: every row leaves at least MinOpenLanes lanes free,
    // so there is always a way through. Harder patterns unlock as the difficulty level (0..1) rises.
    public static class TrafficPatterns
    {
        public const int MinOpenLanes = 2;

        public static float Weight(TrafficPatternKind kind, float level)
        {
            switch (kind)
            {
                case TrafficPatternKind.Single: return 1f;
                case TrafficPatternKind.Pair: return 0.1f + 0.6f * level;
                case TrafficPatternKind.Wall: return level < 0.3f ? 0f : 0.5f * level;
                case TrafficPatternKind.Zigzag: return level < 0.45f ? 0f : 0.45f * level;
                default: return 0f;
            }
        }

        // r01: uniform random in [0, 1).
        public static TrafficPatternKind Pick(float level, double r01)
        {
            float total = 0f;
            for (int k = 0; k <= (int)TrafficPatternKind.Zigzag; k++) total += Weight((TrafficPatternKind)k, level);
            float target = (float)r01 * total;
            for (int k = 0; k <= (int)TrafficPatternKind.Zigzag; k++)
            {
                target -= Weight((TrafficPatternKind)k, level);
                if (target < 0f) return (TrafficPatternKind)k;
            }
            return TrafficPatternKind.Single;
        }

        // Fills output with the pattern's cars and returns the number of rows it spans.
        public static int Build(TrafficPatternKind kind, int laneCount, Random rng, List<CarSlot> output)
        {
            output.Clear();
            if (laneCount <= 0) return 0;

            int maxPerRow = Math.Max(1, laneCount - MinOpenLanes);
            switch (kind)
            {
                case TrafficPatternKind.Pair:
                    AddDistinctLanes(Math.Min(2, maxPerRow), laneCount, 0, rng, output);
                    return 1;

                case TrafficPatternKind.Wall:
                    AddDistinctLanes(maxPerRow, laneCount, 0, rng, output);
                    return 1;

                case TrafficPatternKind.Zigzag:
                {
                    // Rows alternate between blocking the left and the right edge, forcing a weave.
                    int perRow = Math.Min(2, maxPerRow);
                    bool left = rng.Next(2) == 0;
                    for (int row = 0; row < 3; row++, left = !left)
                        for (int i = 0; i < perRow; i++)
                            output.Add(new CarSlot(left ? i : laneCount - 1 - i, row));
                    return 3;
                }

                default:
                    output.Add(new CarSlot(rng.Next(laneCount), 0));
                    return 1;
            }
        }

        private static void AddDistinctLanes(int count, int laneCount, int row, Random rng, List<CarSlot> output)
        {
            // Partial Fisher-Yates over the lane indices.
            int[] lanes = new int[laneCount];
            for (int i = 0; i < laneCount; i++) lanes[i] = i;
            for (int i = 0; i < count; i++)
            {
                int j = rng.Next(i, laneCount);
                (lanes[i], lanes[j]) = (lanes[j], lanes[i]);
                output.Add(new CarSlot(lanes[i], row));
            }
        }
    }
}
