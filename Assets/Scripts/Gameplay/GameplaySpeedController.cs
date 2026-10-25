using System;
using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // World-speed multiplier for road, obstacles and collectibles (Slow-Time, Nitro).
    // Deliberately separate from Time.timeScale, which the game uses for game over.
    public class GameplaySpeedController : MonoBehaviour
    {
        [Tooltip("Seconds to blend between multipliers. 0 = instant.")]
        [SerializeField] private float rampDuration = 0.25f;

        private readonly SpeedModifiers modifiers = new SpeedModifiers();

        public float Multiplier { get; private set; } = 1f;
        public float TargetMultiplier => modifiers.Combined;

        public event Action<float> MultiplierChanged;

        public void SetModifier(SpeedSource source, float multiplier)
        {
            modifiers.Set(source, multiplier);
            enabled = true;
        }

        public void ClearModifier(SpeedSource source)
        {
            modifiers.Clear(source);
            enabled = true;
        }

        private void Awake()
        {
            enabled = false;
        }

        // Only runs while blending towards a new target.
        private void Update()
        {
            float target = modifiers.Combined;
            float next = rampDuration > 0f
                ? Mathf.MoveTowards(Multiplier, target, Time.deltaTime / rampDuration)
                : target;

            if (!Mathf.Approximately(next, Multiplier))
            {
                Multiplier = next;
                MultiplierChanged?.Invoke(Multiplier);
            }

            if (Mathf.Approximately(Multiplier, target))
            {
                Multiplier = target;
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            MultiplierChanged = null;
        }
    }
}
