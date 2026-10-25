using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Slows road, obstacles and collectibles via the gameplay speed multiplier. Player steering is not slowed.
    public class SlowTimeEffect : PowerUpEffect
    {
        [SerializeField] private GameplaySpeedController speedController;

        public override CollectibleKind Kind => CollectibleKind.SlowTime;

        protected override void OnActivated() => Apply();
        protected override void OnRefreshed() => Apply();

        protected override void OnDeactivated()
        {
            if (speedController != null) speedController.ClearModifier(SpeedSource.SlowTime);
        }

        private void Apply()
        {
            if (speedController != null)
                speedController.SetModifier(SpeedSource.SlowTime, Definition.magnitude);
        }
    }
}
