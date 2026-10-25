using System;
using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Base for timed power-ups. The component is only enabled (and only ticks) while active.
    // Timers use scaled Time.deltaTime, so they freeze on game over (timeScale 0) and in any future pause.
    // Collecting the same power-up again refreshes it to the full duration (no stacking).
    public abstract class PowerUpEffect : MonoBehaviour
    {
        private EffectTimer timer;

        public abstract CollectibleKind Kind { get; }
        public CollectibleDefinition Definition { get; private set; }
        public bool IsActive { get; private set; }

        // Shield overrides this: it lasts until it absorbs a hit.
        public virtual bool HasDuration => true;
        public float Remaining => timer.Remaining;
        public float RemainingNormalized => HasDuration ? timer.Normalized : 1f;

        public event Action<PowerUpEffect> Activated;
        public event Action<PowerUpEffect> Expired;

        protected virtual void Awake()
        {
            enabled = false;
        }

        public void Activate(CollectibleDefinition definition)
        {
            Definition = definition;
            if (HasDuration) timer.Start(definition.duration);

            bool refreshed = IsActive;
            IsActive = true;
            enabled = true;
            if (refreshed) OnRefreshed();
            else OnActivated();
            Activated?.Invoke(this);
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;
            timer.Stop();
            OnDeactivated();
            Expired?.Invoke(this);
            if (!KeepTicking) enabled = false;
        }

        // Lets an effect keep running after deactivation (e.g. shield invulnerability blink).
        protected virtual bool KeepTicking => false;

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            if (IsActive && HasDuration && timer.Tick(dt))
            {
                Deactivate();
                return;
            }
            OnTick(dt);
            if (!IsActive && !KeepTicking) enabled = false;
        }

        protected virtual void OnDisable()
        {
            // Scene unload / restart: make sure nothing lingers (speed modifiers, visuals).
            if (IsActive) Deactivate();
        }

        protected abstract void OnActivated();
        protected virtual void OnRefreshed() { }
        protected abstract void OnDeactivated();
        protected virtual void OnTick(float deltaTime) { }
    }
}
