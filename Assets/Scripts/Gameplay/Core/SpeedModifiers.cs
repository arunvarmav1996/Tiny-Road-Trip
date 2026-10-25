namespace TinyRoadTrip.Gameplay.Core
{
    public enum SpeedSource
    {
        SlowTime = 0,
        Nitro = 1,
    }

    // Combined world-speed multiplier = product of all active modifiers (Slow-Time 0.5 x Nitro 1.5 = 0.75).
    public sealed class SpeedModifiers
    {
        private readonly float[] values;
        private readonly bool[] active;

        public SpeedModifiers()
        {
            int count = System.Enum.GetValues(typeof(SpeedSource)).Length;
            values = new float[count];
            active = new bool[count];
        }

        public float Combined { get; private set; } = 1f;

        public void Set(SpeedSource source, float multiplier)
        {
            int i = (int)source;
            values[i] = multiplier < 0f ? 0f : multiplier;
            active[i] = true;
            Recalculate();
        }

        public void Clear(SpeedSource source)
        {
            active[(int)source] = false;
            Recalculate();
        }

        public void ClearAll()
        {
            for (int i = 0; i < active.Length; i++) active[i] = false;
            Recalculate();
        }

        private void Recalculate()
        {
            float product = 1f;
            for (int i = 0; i < values.Length; i++)
                if (active[i]) product *= values[i];
            Combined = product;
        }
    }
}
