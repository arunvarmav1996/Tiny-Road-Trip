using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Amber turn-signal blink on one side of a traffic car (front and rear corner). The lights are built at
    // runtime from a generated dot sprite, so the vehicle prefab needs no extra setup. Safe on pooled cars:
    // the spawner calls Off() whenever a car is reused.
    public class TurnSignal : MonoBehaviour
    {
        private const float BlinkPeriod = 0.36f;
        private static readonly Color Amber = new Color(1f, 0.85f, 0.2f, 1f); // yellower than the orange coins
        private static Sprite dotSprite;

        private SpriteRenderer front;
        private SpriteRenderer rear;
        private int side; // -1 left, +1 right, 0 off

        public void Show(int direction, Bounds carBounds)
        {
            EnsureLights();
            side = direction < 0 ? -1 : 1;

            // One light at each corner on that side.
            Vector3 scale = transform.lossyScale;
            float x = side * (carBounds.extents.x - 0.06f) / Mathf.Max(0.0001f, Mathf.Abs(scale.x));
            float y = (carBounds.extents.y - 0.12f) / Mathf.Max(0.0001f, Mathf.Abs(scale.y));
            front.transform.localPosition = new Vector3(x, -y, -0.01f);
            rear.transform.localPosition = new Vector3(x, y, -0.01f);
            SetLit(true);
        }

        public void Off()
        {
            side = 0;
            if (front != null) SetLit(false);
        }

        private void Update()
        {
            if (side == 0 || front == null) return;
            SetLit(Mathf.Repeat(Time.time, BlinkPeriod) < BlinkPeriod * 0.5f);
        }

        private void SetLit(bool lit)
        {
            front.enabled = lit;
            rear.enabled = lit;
        }

        private void EnsureLights()
        {
            if (front != null) return;
            SpriteRenderer body = GetComponent<SpriteRenderer>();
            front = CreateLight("TurnSignalFront", body);
            rear = CreateLight("TurnSignalRear", body);
        }

        private SpriteRenderer CreateLight(string lightName, SpriteRenderer body)
        {
            var go = new GameObject(lightName);
            go.transform.SetParent(transform, false);
            float size = 0.24f / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            go.transform.localScale = new Vector3(size, size, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = DotSprite();
            sr.color = Amber;
            if (body != null)
            {
                sr.sortingLayerID = body.sortingLayerID;
                sr.sortingOrder = body.sortingOrder + 1;
            }
            sr.enabled = false;
            return sr;
        }

        // 1 world unit wide soft dot.
        private static Sprite DotSprite()
        {
            if (dotSprite != null) return dotSprite;
            const int n = 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                byte a = (byte)(255f * Mathf.Clamp01((1f - d) * 3f));
                pixels[y * n + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            dotSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return dotSprite;
        }
    }
}
