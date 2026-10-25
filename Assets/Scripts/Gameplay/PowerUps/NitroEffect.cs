using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // The car never moves forward in this game (the world scrolls), so Nitro speeds up the world
    // via the gameplay speed multiplier, adds bonus score per second and shows an exhaust flame.
    public class NitroEffect : PowerUpEffect
    {
        [SerializeField] private GameplaySpeedController speedController;
        [SerializeField] private ScoreBridge scoreBridge;
        [Tooltip("Extra score per second while active (the base score graph adds 10/s).")]
        [SerializeField] private float bonusScorePerSecond = 10f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer flame;
        [SerializeField] private float flickerSpeed = 30f;
        [SerializeField] private float flickerAmount = 0.15f;

        private Vector3 flameBaseScale;

        public override CollectibleKind Kind => CollectibleKind.Nitro;

        protected override void Awake()
        {
            base.Awake();
            if (flame != null)
            {
                flameBaseScale = flame.transform.localScale;
                flame.enabled = false;
            }
        }

        protected override void OnActivated()
        {
            Apply();
            if (flame != null) flame.enabled = true;
        }

        protected override void OnRefreshed() => Apply();

        protected override void OnDeactivated()
        {
            if (speedController != null) speedController.ClearModifier(SpeedSource.Nitro);
            if (flame != null)
            {
                flame.enabled = false;
                flame.transform.localScale = flameBaseScale;
            }
        }

        protected override void OnTick(float deltaTime)
        {
            if (!IsActive) return;
            if (scoreBridge != null) scoreBridge.AddPoints(bonusScorePerSecond * deltaTime);
            if (flame != null)
            {
                float f = 1f + flickerAmount * Mathf.Sin(Time.time * flickerSpeed);
                flame.transform.localScale = new Vector3(flameBaseScale.x, flameBaseScale.y * f, flameBaseScale.z);
            }
        }

        private void Apply()
        {
            if (speedController != null)
                speedController.SetModifier(SpeedSource.Nitro, Definition.magnitude);
        }
    }
}
