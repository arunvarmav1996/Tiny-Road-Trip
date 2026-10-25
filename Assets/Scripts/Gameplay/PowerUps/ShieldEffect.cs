using System;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Absorbs one obstacle hit, then gives a short invulnerability window so the same contact
    // (or a car right behind it) cannot end the run on the next physics step.
    public class ShieldEffect : PowerUpEffect
    {
        [SerializeField] private Spawner obstacleSpawner;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private SpriteRenderer playerSprite;

        [Tooltip("Seconds of collision immunity after the shield breaks.")]
        [SerializeField, Min(0f)] private float invulnerabilityDuration = 1f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer bubble;
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField] private float pulseAmount = 0.06f;
        [SerializeField] private float blinkInterval = 0.1f;

        private float invulnerableRemaining;
        private Vector3 bubbleBaseScale;

        public override CollectibleKind Kind => CollectibleKind.Shield;
        public override bool HasDuration => false;
        public bool IsInvulnerable => invulnerableRemaining > 0f;

        public event Action Absorbed;

        protected override bool KeepTicking => invulnerableRemaining > 0f;

        protected override void Awake()
        {
            base.Awake();
            if (bubble != null)
            {
                bubbleBaseScale = bubble.transform.localScale;
                bubble.enabled = false;
            }
        }

        protected override void OnActivated()
        {
            if (bubble != null) bubble.enabled = true;
        }

        protected override void OnDeactivated()
        {
            if (bubble != null)
            {
                bubble.enabled = false;
                bubble.transform.localScale = bubbleBaseScale;
            }
        }

        // Called by PlayerCollision before game over. Returns true when the hit is absorbed.
        public bool TryAbsorb(Collider2D obstacle)
        {
            if (IsInvulnerable)
            {
                IgnoreFurtherContact(obstacle);
                return true;
            }
            if (!IsActive) return false;

            // Remove the car we hit and stop any push the contact gave the player.
            if (obstacleSpawner != null) obstacleSpawner.DespawnObstacle(obstacle);
            if (playerBody != null) playerBody.linearVelocity = Vector2.zero;

            invulnerableRemaining = invulnerabilityDuration;
            Deactivate();
            enabled = true; // keep ticking for the invulnerability window
            Absorbed?.Invoke();
            return true;
        }

        private void IgnoreFurtherContact(Collider2D obstacle)
        {
            if (playerCollider != null && obstacle != null)
                Physics2D.IgnoreCollision(playerCollider, obstacle, true);
            if (obstacleSpawner != null) obstacleSpawner.MarkHit(obstacle);
            if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
        }

        protected override void OnTick(float deltaTime)
        {
            if (IsActive && bubble != null)
            {
                float s = 1f + pulseAmount * Mathf.Sin(Time.time * pulseSpeed);
                bubble.transform.localScale = bubbleBaseScale * s;
            }

            if (invulnerableRemaining > 0f)
            {
                invulnerableRemaining -= deltaTime;
                if (playerSprite != null)
                    playerSprite.enabled = invulnerableRemaining <= 0f || Mathf.Repeat(invulnerableRemaining, blinkInterval * 2f) > blinkInterval;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (playerSprite != null) playerSprite.enabled = true;
        }
    }
}
