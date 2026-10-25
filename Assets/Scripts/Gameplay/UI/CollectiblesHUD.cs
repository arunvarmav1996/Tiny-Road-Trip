using System;
using TinyRoadTrip.Gameplay.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TinyRoadTrip.Gameplay
{
    // Coin counter, active power-up timers and popups (pickups, near miss / combo, shield break).
    // Texts change only on events; Update runs only while a timer or animation is visible.
    public class CollectiblesHUD : MonoBehaviour
    {
        [Serializable]
        public class PowerUpSlot
        {
            public CollectibleKind kind;
            public GameObject root;
            public Image icon;
            [Tooltip("Scaled horizontally to show remaining time (pivot on the left).")]
            public RectTransform fill;
            public Image fillImage;
            public TMP_Text label;
        }

        [Header("Sources")]
        [SerializeField] private CoinManager coinManager;
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private ComboManager comboManager;
        [SerializeField] private CollectibleSpawner collectibleSpawner;

        [Header("Coins")]
        [SerializeField] private TMP_Text runCoinsText;
        [SerializeField] private TMP_Text totalCoinsText;
        [SerializeField] private RectTransform coinIcon;
        [SerializeField] private float coinPunchScale = 1.3f;
        [SerializeField] private float coinPunchDuration = 0.15f;

        [Header("Power-Ups")]
        [SerializeField] private PowerUpSlot[] slots = Array.Empty<PowerUpSlot>();

        [Header("Layout")]
        [Tooltip("Existing text to keep clear of (the score). The top row is placed just below its glyphs.")]
        [SerializeField] private TMP_Text placeBelow;
        [Tooltip("Container (anchored to the top of the safe area) holding the coin panel and power-up list.")]
        [SerializeField] private RectTransform topContent;
        [SerializeField] private SafeAreaFitter safeArea;
        [SerializeField] private float marginBelow = 12f;

        [Header("Popup")]
        [SerializeField] private TMP_Text popupText;
        [SerializeField] private float popupHold = 0.8f;
        [SerializeField] private float popupFade = 0.35f;
        [SerializeField] private float popupStartScale = 1.35f;
        [SerializeField] private Color nearMissColor = new Color(1f, 0.95f, 0.4f);
        [SerializeField] private Color comboColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField] private Color shieldBreakColor = new Color(0.45f, 1f, 0.6f);

        private float coinPunchTime = -1f;
        private float popupTime = -1f;
        private int activeSlots;

        // Subscriptions live in Awake/OnDestroy because "enabled" is toggled to skip idle Updates.
        private void Awake()
        {
            foreach (PowerUpSlot slot in slots)
                if (slot.root != null) slot.root.SetActive(false);
            if (popupText != null) popupText.gameObject.SetActive(false);

            if (coinManager != null) coinManager.CoinsChanged += OnCoinsChanged;
            if (powerUpManager != null)
            {
                powerUpManager.EffectActivated += OnEffectActivated;
                powerUpManager.EffectExpired += OnEffectExpired;
            }
            if (comboManager != null) comboManager.NearMissAwarded += OnNearMiss;
            if (collectibleSpawner != null) collectibleSpawner.Collected += OnCollected;
            if (safeArea != null) safeArea.Applied += RepositionTopContent;
        }

        private void Start()
        {
            RepositionTopContent();

            // Shield break is only known by the shield itself.
            if (powerUpManager != null && powerUpManager.Shield != null)
                powerUpManager.Shield.Absorbed += OnShieldAbsorbed;
            if (coinManager != null) OnCoinsChanged(coinManager.RunCoins, coinManager.TotalCoins);
        }

        private void OnDestroy()
        {
            if (coinManager != null) coinManager.CoinsChanged -= OnCoinsChanged;
            if (powerUpManager != null)
            {
                powerUpManager.EffectActivated -= OnEffectActivated;
                powerUpManager.EffectExpired -= OnEffectExpired;
                if (powerUpManager.Shield != null) powerUpManager.Shield.Absorbed -= OnShieldAbsorbed;
            }
            if (comboManager != null) comboManager.NearMissAwarded -= OnNearMiss;
            if (collectibleSpawner != null) collectibleSpawner.Collected -= OnCollected;
            if (safeArea != null) safeArea.Applied -= RepositionTopContent;
        }

        // The score uses a Constant Pixel Size canvas, so its share of the screen differs per device.
        // Measure where its glyphs end and start the coin / power-up row just below.
        private void RepositionTopContent()
        {
            if (placeBelow == null || topContent == null) return;
            var parent = topContent.parent as RectTransform;
            if (parent == null) return;

            Canvas.ForceUpdateCanvases();
            placeBelow.ForceMeshUpdate();
            Bounds glyphs = placeBelow.textBounds;
            if (glyphs.size.y <= 0f) return;

            Canvas sourceCanvas = placeBelow.canvas;
            Camera sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? sourceCanvas.worldCamera : null;
            Vector3 bottomWorld = placeBelow.rectTransform.TransformPoint(new Vector3(glyphs.center.x, glyphs.min.y, 0f));
            Vector2 bottomScreen = RectTransformUtility.WorldToScreenPoint(sourceCamera, bottomWorld);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, bottomScreen, null, out Vector2 local)) return;
            float offset = Mathf.Min(local.y - parent.rect.yMax - marginBelow, -marginBelow);
            topContent.anchoredPosition = new Vector2(topContent.anchoredPosition.x, offset);
        }

        private void OnCoinsChanged(int run, int total)
        {
            if (runCoinsText != null) runCoinsText.SetText("{0}", run);
            if (totalCoinsText != null) totalCoinsText.SetText("BANK {0}", total);
            if (run > 0)
            {
                coinPunchTime = 0f;
                enabled = true;
            }
        }

        private void OnCollected(CollectibleDefinition definition, Vector3 position)
        {
            if (definition.IsPowerUp && !string.IsNullOrEmpty(definition.pickupMessage))
                ShowPopup(definition.pickupMessage, definition.uiColor);
        }

        private void OnEffectActivated(PowerUpEffect effect)
        {
            PowerUpSlot slot = FindSlot(effect.Kind);
            if (slot == null || slot.root == null) return;
            if (!slot.root.activeSelf)
            {
                slot.root.SetActive(true);
                activeSlots++;
            }
            if (slot.fillImage != null && effect.Definition != null) slot.fillImage.color = effect.Definition.uiColor;
            if (slot.label != null && effect.Definition != null) slot.label.SetText(effect.Definition.displayName);
            UpdateSlot(slot, effect);
            enabled = true;
        }

        private void OnEffectExpired(PowerUpEffect effect)
        {
            PowerUpSlot slot = FindSlot(effect.Kind);
            if (slot == null || slot.root == null || !slot.root.activeSelf) return;
            slot.root.SetActive(false);
            activeSlots--;
        }

        private void OnNearMiss(NearMissResult result)
        {
            if (result.Combo <= 1)
                ShowPopup($"NEAR MISS! +{result.Points}", nearMissColor);
            else
                ShowPopup($"{result.Multiplier}x COMBO! +{result.Points}", comboColor);
        }

        private void OnShieldAbsorbed()
        {
            ShowPopup("SHIELD SAVED YOU!", shieldBreakColor);
        }

        private void ShowPopup(string message, Color color)
        {
            if (popupText == null) return;
            popupText.SetText(message);
            popupText.color = color;
            popupText.gameObject.SetActive(true);
            popupTime = 0f;
            enabled = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool animating = false;

            if (activeSlots > 0 && powerUpManager != null)
            {
                foreach (PowerUpSlot slot in slots)
                {
                    if (slot.root == null || !slot.root.activeSelf) continue;
                    foreach (PowerUpEffect effect in powerUpManager.Effects)
                        if (effect != null && effect.Kind == slot.kind) UpdateSlot(slot, effect);
                }
                animating = true;
            }

            if (coinPunchTime >= 0f && coinIcon != null)
            {
                coinPunchTime += dt;
                float t = Mathf.Clamp01(coinPunchTime / coinPunchDuration);
                coinIcon.localScale = Vector3.one * Mathf.Lerp(coinPunchScale, 1f, t);
                if (t >= 1f) coinPunchTime = -1f;
                else animating = true;
            }

            if (popupTime >= 0f && popupText != null)
            {
                popupTime += dt;
                float scaleT = Mathf.Clamp01(popupTime / 0.12f);
                popupText.rectTransform.localScale = Vector3.one * Mathf.Lerp(popupStartScale, 1f, scaleT);
                float fadeT = Mathf.Clamp01((popupTime - popupHold) / popupFade);
                popupText.alpha = 1f - fadeT;
                if (fadeT >= 1f)
                {
                    popupText.gameObject.SetActive(false);
                    popupTime = -1f;
                }
                else animating = true;
            }

            if (!animating) enabled = false;
        }

        private static void UpdateSlot(PowerUpSlot slot, PowerUpEffect effect)
        {
            if (slot.fill != null)
                slot.fill.localScale = new Vector3(effect.RemainingNormalized, 1f, 1f);
        }

        private PowerUpSlot FindSlot(CollectibleKind kind)
        {
            foreach (PowerUpSlot slot in slots)
                if (slot.kind == kind) return slot;
            return null;
        }
    }
}
