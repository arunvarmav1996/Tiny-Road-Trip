using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // While active, CollectibleSpawner pulls coins (only coins) within Radius towards the player.
    public class CoinMagnetEffect : PowerUpEffect
    {
        [Tooltip("Speed coins fly towards the player (world units/second).")]
        [SerializeField, Min(0.1f)] private float pullSpeed = 10f;
        [Tooltip("Coins closer than this are collected automatically.")]
        [SerializeField, Min(0.01f)] private float collectDistance = 0.35f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer ring;
        [SerializeField] private float spinSpeed = 90f;

        public override CollectibleKind Kind => CollectibleKind.CoinMagnet;
        public float Radius => Definition != null ? Definition.magnitude : 0f;
        public float PullSpeed => pullSpeed;
        public float CollectDistance => collectDistance;

        protected override void Awake()
        {
            base.Awake();
            if (ring != null) ring.enabled = false;
        }

        protected override void OnActivated()
        {
            if (ring != null) ring.enabled = true;
        }

        protected override void OnDeactivated()
        {
            if (ring != null) ring.enabled = false;
        }

        protected override void OnTick(float deltaTime)
        {
            if (IsActive && ring != null)
                ring.transform.Rotate(0f, 0f, spinSpeed * deltaTime);
        }
    }
}
