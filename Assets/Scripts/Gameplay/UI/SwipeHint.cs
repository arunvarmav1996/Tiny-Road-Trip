using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TinyRoadTrip.Gameplay
{
    // First-run tutorial: a finger dot sweeps left and right with "SWIPE TO DODGE" for a few seconds.
    // Shown once per install (PlayerPrefs). Hides early as soon as the player changes lane.
    // Built at runtime on its own overlay canvas; uses scaled time so it freezes with the pause menu.
    public class SwipeHint : MonoBehaviour
    {
        public const string SeenKey = "TinyRoadTrip.SeenSwipeHint";

        [SerializeField] private Transform player;
        [SerializeField] private float showDuration = 3.5f;
        [SerializeField] private float startDelay = 0.4f;
        [SerializeField] private float fadeDuration = 0.35f;
        [SerializeField] private float sweepDistance = 220f; // canvas units
        [SerializeField] private float sweepSpeed = 2.2f;     // cycles per second / (2π)
        [SerializeField] private string message = "SWIPE TO DODGE";

        private CanvasGroup group;
        private RectTransform finger;
        private float elapsed;
        private float hideAt;
        private float playerStartX;

        private void Start()
        {
            if (PlayerPrefs.GetInt(SeenKey, 0) == 1)
            {
                enabled = false;
                return;
            }
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();

            Build();
            if (player != null) playerStartX = player.position.x;
            hideAt = startDelay + showDuration;
        }

        private void Update()
        {
            if (group == null) return;
            elapsed += Time.deltaTime;

            // Lane change made: the player got it, start fading now.
            if (player != null && elapsed > startDelay && hideAt > elapsed && Mathf.Abs(player.position.x - playerStartX) > 0.5f)
                hideAt = elapsed;

            float fadeIn = Mathf.Clamp01((elapsed - startDelay) / fadeDuration);
            float fadeOut = Mathf.Clamp01((hideAt + fadeDuration - elapsed) / fadeDuration);
            group.alpha = Mathf.Min(fadeIn, fadeOut);
            finger.anchoredPosition = new Vector2(Mathf.Sin(elapsed * sweepSpeed) * sweepDistance * 0.5f, finger.anchoredPosition.y);

            if (elapsed >= hideAt + fadeDuration)
            {
                Destroy(group.gameObject);
                group = null;
                enabled = false;
            }
        }

        private void Build()
        {
            var root = new GameObject("SwipeHint", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var panel = new GameObject("Panel", typeof(RectTransform));
            var panelRect = (RectTransform)panel.transform;
            panelRect.SetParent(root.transform, false);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.3f);
            panelRect.sizeDelta = new Vector2(620f, 260f);
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);
            bg.raycastTarget = false;

            var track = new GameObject("Track", typeof(RectTransform));
            var trackRect = (RectTransform)track.transform;
            trackRect.SetParent(panelRect, false);
            trackRect.anchoredPosition = new Vector2(0f, -40f);
            trackRect.sizeDelta = new Vector2(sweepDistance + 60f, 6f);
            var trackImage = track.AddComponent<Image>();
            trackImage.color = new Color(1f, 1f, 1f, 0.35f);
            trackImage.raycastTarget = false;

            var dot = new GameObject("Finger", typeof(RectTransform));
            finger = (RectTransform)dot.transform;
            finger.SetParent(panelRect, false);
            finger.anchoredPosition = new Vector2(0f, -40f);
            finger.sizeDelta = new Vector2(64f, 64f);
            var dotImage = dot.AddComponent<Image>();
            dotImage.sprite = CreateCircleSprite(64);
            dotImage.color = Color.white;
            dotImage.raycastTarget = false;

            var label = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(panelRect, false);
            labelRect.anchoredPosition = new Vector2(0f, 55f);
            labelRect.sizeDelta = new Vector2(600f, 90f);
            var text = label.AddComponent<TextMeshProUGUI>();
            text.text = message;
            text.fontSize = 64f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        // Soft-edged white disc (Unity's built-in UI sprites are editor-only, so generate one).
        private static Sprite CreateCircleSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                byte a = (byte)(Mathf.Clamp01(radius - d) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
