using System;

namespace TinyRoadTrip.Gameplay.Core
{
    public enum WeatherKind
    {
        Clear = 0,
        Rain = 1,
        Snow = 2,
        Spring = 3,
        Summer = 4,
        Fall = 5,
    }

    // Weather over one run: clear spells alternate with weather spells picked at random by weight
    // (never the same weather twice in a row when there is a choice). Every run starts clear.
    // A spell only begins during a calm traffic phase (or after waiting MaxCalmWait), so weather never
    // rolls in on top of a busy peak. Each spell: Warning (visuals start, rules off) -> FadeIn -> Active -> FadeOut.
    //   Visual: 0..1 for tint, particles, overlays. Leads Rule by the warning so the player sees it coming.
    //   Rule:   0..1 for gameplay effects (steering, score bonus).
    [Serializable]
    public sealed class WeatherSchedule
    {
        public float WarmUp = 30f;            // first run seconds are always clear
        public float MinClear = 15f;
        public float MaxClear = 30f;
        public float MinDuration = 25f;
        public float MaxDuration = 40f;
        public float WarningTime = 3f;
        public float FadeTime = 4f;
        public float MaxCalmWait = 15f;       // start anyway if no calm phase comes
        public float WarningVisual = 0.35f;   // visual level reached by the end of the warning

        public float RainWeight = 1f;
        public float SnowWeight = 1f;
        public float SpringWeight = 1f;
        public float SummerWeight = 1f;
        public float FallWeight = 1f;

        public enum Phase { Clear, Warning, FadeIn, Active, FadeOut }

        public Phase State { get; private set; } = Phase.Clear;
        public WeatherKind Kind { get; private set; } = WeatherKind.Clear;
        public float Visual { get; private set; }
        public float Rule { get; private set; }

        private float phaseTime;
        private float phaseLength = -1f;     // -1: warm-up not over yet (WarmUp is read live so the inspector value counts)
        private WeatherKind last = WeatherKind.Clear;
        private readonly float[] weights = new float[Enum.GetValues(typeof(WeatherKind)).Length];

        public void Reset()
        {
            State = Phase.Clear;
            Kind = WeatherKind.Clear;
            Visual = 0f;
            Rule = 0f;
            phaseTime = 0f;
            phaseLength = -1f;
            last = WeatherKind.Clear;
        }

        // Starts a spell of this weather now (skips the clear wait and the calm check). For testing.
        public void Begin(WeatherKind kind)
        {
            if (kind == WeatherKind.Clear) return;
            Kind = kind;
            Enter(Phase.Warning, WarningTime);
            UpdateLevels();
        }

        // calm: the traffic is in a calm phase right now. random: 0..1 rolls.
        public void Tick(float deltaTime, bool calm, Random random)
        {
            if (deltaTime <= 0f) return;
            phaseTime += deltaTime;

            switch (State)
            {
                case Phase.Clear:
                    float due = phaseLength < 0f ? WarmUp : phaseLength;
                    if (phaseTime < due) break;
                    if (!calm && phaseTime < due + MaxCalmWait) break;
                    WeatherKind next = PickNext(random.NextDouble());
                    if (next == WeatherKind.Clear)
                    {
                        Enter(Phase.Clear, Roll(MinClear, MaxClear, random));
                        break;
                    }
                    Kind = next;
                    Enter(Phase.Warning, WarningTime);
                    break;

                case Phase.Warning:
                    if (phaseTime >= phaseLength) Enter(Phase.FadeIn, FadeTime);
                    break;

                case Phase.FadeIn:
                    if (phaseTime >= phaseLength) Enter(Phase.Active, Roll(MinDuration, MaxDuration, random));
                    break;

                case Phase.Active:
                    if (phaseTime >= phaseLength) Enter(Phase.FadeOut, FadeTime);
                    break;

                case Phase.FadeOut:
                    if (phaseTime >= phaseLength)
                    {
                        last = Kind;
                        Kind = WeatherKind.Clear;
                        Enter(Phase.Clear, Roll(MinClear, MaxClear, random));
                    }
                    break;
            }

            UpdateLevels();
        }

        private void Enter(Phase phase, float length)
        {
            State = phase;
            phaseTime = 0f;
            phaseLength = length;
        }

        private void UpdateLevels()
        {
            float p = phaseLength > 0f ? Clamp01(phaseTime / phaseLength) : 1f;
            switch (State)
            {
                case Phase.Warning: Visual = WarningVisual * p; Rule = 0f; break;
                case Phase.FadeIn: Visual = WarningVisual + (1f - WarningVisual) * p; Rule = p; break;
                case Phase.Active: Visual = 1f; Rule = 1f; break;
                case Phase.FadeOut: Visual = 1f - p; Rule = 1f - p; break;
                default: Visual = 0f; Rule = 0f; break;
            }
        }

        // Weighted pick among non-clear weathers, skipping the last one if another has weight.
        private WeatherKind PickNext(double roll)
        {
            weights[(int)WeatherKind.Clear] = 0f;
            weights[(int)WeatherKind.Rain] = RainWeight;
            weights[(int)WeatherKind.Snow] = SnowWeight;
            weights[(int)WeatherKind.Spring] = SpringWeight;
            weights[(int)WeatherKind.Summer] = SummerWeight;
            weights[(int)WeatherKind.Fall] = FallWeight;

            float others = 0f;
            for (int i = 1; i < weights.Length; i++)
                if (i != (int)last) others += weights[i];
            if (others > 0f && last != WeatherKind.Clear) weights[(int)last] = 0f;

            int picked = GameplayMath.PickWeighted(weights, weights.Length, (float)roll);
            return picked < 0 ? WeatherKind.Clear : (WeatherKind)picked;
        }

        private static float Roll(float min, float max, Random random)
        {
            if (max <= min) return min;
            return min + (max - min) * (float)random.NextDouble();
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    // Gameplay effect of each weather, scaled by WeatherSchedule.Rule.
    //   Rain: slippery steering, small score bonus.
    //   Snow: the whole world slows (careful driving) but steering is icier still; bigger score bonus,
    //         so the net distance per second is still a little higher than in clear weather.
    //   Spring: a breather. No handicap, small score bonus.
    //   Summer: hot asphalt, the world runs a little faster; score bonus to match.
    //   Fall: leaves on the road make steering a little slippery; score bonus.
    public static class WeatherRules
    {
        public const float RainSteerFactor = 0.55f;    // lane changes at 55% speed: the road is slippery
        public const float RainDistanceBonus = 1.25f;  // distance (score) counts 25% more while it rains
        public const float SnowSteerFactor = 0.4f;
        public const float SnowWorldSpeed = 0.8f;      // road, cars and coins at 80% speed
        public const float SnowDistanceBonus = 1.4f;
        public const float SpringDistanceBonus = 1.1f;
        public const float SummerWorldSpeed = 1.1f;
        public const float SummerDistanceBonus = 1.15f;
        public const float FallSteerFactor = 0.8f;
        public const float FallDistanceBonus = 1.15f;

        public static float SteerFactor(WeatherKind kind, float rule)
        {
            switch (kind)
            {
                case WeatherKind.Rain: return Lerp(1f, RainSteerFactor, rule);
                case WeatherKind.Snow: return Lerp(1f, SnowSteerFactor, rule);
                case WeatherKind.Fall: return Lerp(1f, FallSteerFactor, rule);
                default: return 1f;
            }
        }

        public static float DistanceFactor(WeatherKind kind, float rule)
        {
            switch (kind)
            {
                case WeatherKind.Rain: return Lerp(1f, RainDistanceBonus, rule);
                case WeatherKind.Snow: return Lerp(1f, SnowDistanceBonus, rule);
                case WeatherKind.Spring: return Lerp(1f, SpringDistanceBonus, rule);
                case WeatherKind.Summer: return Lerp(1f, SummerDistanceBonus, rule);
                case WeatherKind.Fall: return Lerp(1f, FallDistanceBonus, rule);
                default: return 1f;
            }
        }

        // Multiplier for the world speed (GameplaySpeedController), on top of Slow-Time / Nitro.
        public static float WorldSpeedFactor(WeatherKind kind, float rule)
        {
            switch (kind)
            {
                case WeatherKind.Snow: return Lerp(1f, SnowWorldSpeed, rule);
                case WeatherKind.Summer: return Lerp(1f, SummerWorldSpeed, rule);
                default: return 1f;
            }
        }

        private static float Lerp(float a, float b, float t)
        {
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            return a + (b - a) * t;
        }
    }
}
