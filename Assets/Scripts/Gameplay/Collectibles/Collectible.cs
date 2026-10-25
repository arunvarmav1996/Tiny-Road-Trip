using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Pooled pickup. Movement is driven by CollectibleSpawner (no per-object Update).
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class Collectible : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer icon;
        [Tooltip("Pickup radius as a fraction of the visual size (1 = edge of the sprite).")]
        [SerializeField, Range(0.3f, 1.5f)] private float pickupRadiusFactor = 0.9f;

        private CircleCollider2D circle;

        private CollectibleSpawner owner;

        public CollectibleDefinition Definition { get; private set; }
        public Rigidbody2D Rigidbody { get; private set; }
        public bool Collected { get; private set; }
        public bool Magnetized { get; set; }
        public Vector3 BaseScale { get; private set; }

        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody2D>();
            circle = GetComponent<CircleCollider2D>();
        }

        public void Spawn(CollectibleSpawner spawner, CollectibleDefinition definition, Vector3 position)
        {
            owner = spawner;
            Definition = definition;
            Collected = false;
            Magnetized = false;

            body.sprite = definition.sprite;
            body.color = definition.tint;
            if (icon != null)
            {
                icon.sprite = definition.icon;
                icon.color = definition.iconTint;
                icon.enabled = definition.icon != null;
            }

            float spriteSize = definition.sprite != null
                ? Mathf.Max(definition.sprite.bounds.size.x, definition.sprite.bounds.size.y)
                : 1f;
            BaseScale = Vector3.one * (definition.worldSize / Mathf.Max(spriteSize, 0.0001f));
            transform.localScale = BaseScale;
            if (circle != null) circle.radius = spriteSize * 0.5f * pickupRadiusFactor;

            transform.position = position;
            Rigidbody.position = position;
            gameObject.SetActive(true);
        }

        // Returns false if already collected, so a pickup can never reward twice.
        public bool TryMarkCollected()
        {
            if (Collected) return false;
            Collected = true;
            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!Collected && owner != null && other.CompareTag("Player"))
                owner.Collect(this);
        }
    }
}
