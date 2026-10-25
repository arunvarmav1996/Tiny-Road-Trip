using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    public enum CollectibleKind
    {
        Coin = 0,
        SlowTime = 1,
        Shield = 2,
        Nitro = 3,
        CoinMagnet = 4,
    }

    // Designer-facing configuration for one collectible type.
    // "Magnitude" meaning per kind: SlowTime/Nitro = world speed multiplier, CoinMagnet = attraction radius
    // (world units), Coin/Shield = unused.
    [CreateAssetMenu(menuName = "Tiny Road Trip/Collectible Definition", fileName = "Collectible_")]
    public class CollectibleDefinition : ScriptableObject
    {
        [Header("Identity")]
        public CollectibleKind kind;
        public string displayName = "Collectible";

        [Header("Spawning")]
        [Tooltip("Relative chance per spawn roll. 0 disables spawning.")]
        [Min(0f)] public float spawnWeight = 1f;

        [Header("Effect")]
        [Tooltip("Coins added per pickup (Coin only).")]
        [Min(0)] public int coinValue = 1;
        [Tooltip("Seconds the effect lasts. Ignored by Coin and Shield (shield lasts until it absorbs a hit).")]
        [Min(0f)] public float duration = 5f;
        [Tooltip("SlowTime/Nitro: speed multiplier. CoinMagnet: attraction radius in world units.")]
        public float magnitude = 1f;

        [Header("Visuals")]
        public Sprite sprite;
        public Color tint = Color.white;
        [Tooltip("Optional icon drawn on top of the sprite.")]
        public Sprite icon;
        public Color iconTint = Color.white;
        [Tooltip("Largest side of the pickup in world units.")]
        [Min(0.05f)] public float worldSize = 0.6f;
        [Tooltip("Colour used for HUD timers and pickup popups.")]
        public Color uiColor = Color.white;

        [Header("Feedback")]
        [Tooltip("AudioManager sound name played on pickup. Empty = silent.")]
        public string pickupSound = "";
        [Tooltip("Text shown in the HUD popup on pickup. Empty = none.")]
        public string pickupMessage = "";

        public bool IsPowerUp => kind != CollectibleKind.Coin;
    }
}
