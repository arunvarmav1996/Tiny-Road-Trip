using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Routes collected power-ups to their effect components. New power-ups: add a CollectibleKind,
    // a PowerUpEffect subclass, and list the component in "effects".
    public class PowerUpManager : MonoBehaviour
    {
        [SerializeField] private PowerUpEffect[] effects = Array.Empty<PowerUpEffect>();

        private readonly Dictionary<CollectibleKind, PowerUpEffect> byKind = new Dictionary<CollectibleKind, PowerUpEffect>();

        public IReadOnlyList<PowerUpEffect> Effects => effects;
        public ShieldEffect Shield { get; private set; }
        public CoinMagnetEffect Magnet { get; private set; }

        public event Action<PowerUpEffect> EffectActivated;
        public event Action<PowerUpEffect> EffectExpired;

        private void Awake()
        {
            foreach (PowerUpEffect effect in effects)
            {
                if (effect == null) continue;
                byKind[effect.Kind] = effect;
                effect.Activated += OnEffectActivated;
                effect.Expired += OnEffectExpired;
                if (effect is ShieldEffect shield) Shield = shield;
                if (effect is CoinMagnetEffect magnet) Magnet = magnet;
            }
        }

        private void OnDestroy()
        {
            foreach (PowerUpEffect effect in effects)
            {
                if (effect == null) continue;
                effect.Activated -= OnEffectActivated;
                effect.Expired -= OnEffectExpired;
            }
        }

        public void Activate(CollectibleDefinition definition)
        {
            if (definition != null && byKind.TryGetValue(definition.kind, out PowerUpEffect effect))
                effect.Activate(definition);
        }

        public bool IsActive(CollectibleKind kind)
        {
            return byKind.TryGetValue(kind, out PowerUpEffect effect) && effect.IsActive;
        }

        // Shield hook used by PlayerCollision before triggering game over.
        public bool TryAbsorbObstacleHit(Collider2D obstacle)
        {
            return Shield != null && Shield.TryAbsorb(obstacle);
        }

        public void DeactivateAll()
        {
            foreach (PowerUpEffect effect in effects)
                if (effect != null) effect.Deactivate();
        }

        private void OnEffectActivated(PowerUpEffect effect) => EffectActivated?.Invoke(effect);
        private void OnEffectExpired(PowerUpEffect effect) => EffectExpired?.Invoke(effect);
    }
}
