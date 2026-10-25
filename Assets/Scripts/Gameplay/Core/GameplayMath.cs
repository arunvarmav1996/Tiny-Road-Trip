namespace TinyRoadTrip.Gameplay.Core
{
    public static class GameplayMath
    {
        // Horizontal gap between two intervals; negative when they overlap.
        public static float HorizontalGap(float aMin, float aMax, float bMin, float bMax)
        {
            if (aMax <= bMin) return bMin - aMax;
            if (bMax <= aMin) return aMin - bMax;
            float overlap = (aMax < bMax ? aMax : bMax) - (aMin > bMin ? aMin : bMin);
            return -overlap;
        }

        public static bool IsNearMiss(float gap, float nearMissDistance)
        {
            return gap >= 0f && gap <= nearMissDistance;
        }

        // Picks an index from non-negative weights using roll in [0,1). Returns -1 when all weights are zero.
        public static int PickWeighted(float[] weights, int count, float roll)
        {
            float total = 0f;
            for (int i = 0; i < count; i++)
                if (weights[i] > 0f) total += weights[i];
            if (total <= 0f) return -1;

            float target = roll * total;
            float cumulative = 0f;
            int last = -1;
            for (int i = 0; i < count; i++)
            {
                if (weights[i] <= 0f) continue;
                cumulative += weights[i];
                last = i;
                if (target < cumulative) return i;
            }
            return last;
        }
    }
}
