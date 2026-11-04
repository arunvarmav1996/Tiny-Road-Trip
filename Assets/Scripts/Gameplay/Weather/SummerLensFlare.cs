using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Summer sun as a camera lens flare: a rayed burst near the top corner and a chain of faint coloured ghosts
    // strung along the line from the sun through the middle of the screen. The sun drifts slowly along the top, so
    // the whole chain swings the way a real flare does. Fades with the weather's Visual level.
    // Drawn additively, so it lights up the road and cars behind it without hiding them.
    // Every setting is read each frame, so it can be tuned live in Play mode. To keep values found that way, use
    // the component menu's Copy Component in Play mode, then Paste Component Values after stopping.
    public class SummerLensFlare : MonoBehaviour
    {
        public enum GhostShape { Disc, Hexagon, Ring }

        [System.Serializable]
        public struct Ghost
        {
            [Tooltip("Along the flare line: 0 at the sun, 1 at the middle of the screen, 2 at the opposite corner.")]
            public float Position;
            [Tooltip("Size as a fraction of half the screen width.")]
            public float Size;
            public Color Color;
            public GhostShape Shape;

            public Ghost(float position, float size, Color color, GhostShape shape)
            {
                Position = position;
                Size = size;
                Color = color;
                Shape = shape;
            }
        }

        [SerializeField] private WeatherDirector weather;
        [SerializeField] private Camera targetCamera;
        [Tooltip("Hidden/TinyRoadTrip/FlareAdditive. Referenced here so the build includes it.")]
        [SerializeField] private Shader flareShader;

        [Header("Strength")]
        [Tooltip("Master strength of the whole flare. 0 turns it off.")]
        [Range(0f, 1f)]
        [SerializeField] private float overallStrength = 0.6f;
        [Tooltip("Strength of the sun burst alone.")]
        [Range(0f, 1f)]
        [SerializeField] private float burstStrength = 1f;
        [Tooltip("Strength of the ghosts alone. 0 hides them and leaves only the sun.")]
        [Range(0f, 1f)]
        [SerializeField] private float ghostStrength = 0.5f;

        [Header("Sun")]
        [Tooltip("Where the sun sits: fractions of half the screen, from the middle (1 = the edge).")]
        [SerializeField] private Vector2 sunPosition = new Vector2(0.72f, 0.88f);
        [Tooltip("How far the sun drifts left and right, as a fraction of half the screen width.")]
        [SerializeField] private float sunDrift = 0.06f;
        [Tooltip("Seconds for one drift there and back.")]
        [SerializeField] private float sunDriftSeconds = 60f;
        [Tooltip("Burst size as a fraction of half the screen width.")]
        [SerializeField] private float burstSize = 1.1f;
        [SerializeField] private Color burstColor = new Color(1f, 0.93f, 0.75f, 0.8f);
        [Tooltip("Brightness of the long spikes. 0 leaves a plain round glow.")]
        [Range(0f, 1f)]
        [SerializeField] private float spikeStrength = 0.5f;
        [Tooltip("Degrees per second the burst turns.")]
        [SerializeField] private float burstSpin = 0.5f;
        [Tooltip("How much the brightness pulses. 0 is steady.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float pulse = 0.05f;

        [Header("Ghosts")]
        [Tooltip("Scales every ghost's size.")]
        [Range(0.25f, 2f)]
        [SerializeField] private float ghostScale = 1f;
        [SerializeField] private Ghost[] ghosts =
        {
            new Ghost(0.3f, 0.3f, new Color(1f, 0.72f, 0.3f, 0.14f), GhostShape.Disc),
            new Ghost(0.5f, 0.2f, new Color(0.55f, 1f, 0.6f, 0.14f), GhostShape.Hexagon),
            new Ghost(0.74f, 0.46f, new Color(1f, 0.55f, 0.25f, 0.12f), GhostShape.Ring),
            new Ghost(1.02f, 0.12f, new Color(1f, 0.95f, 0.8f, 0.16f), GhostShape.Disc),
            new Ghost(1.3f, 0.42f, new Color(0.4f, 0.85f, 1f, 0.11f), GhostShape.Hexagon),
            new Ghost(1.62f, 0.68f, new Color(0.75f, 0.5f, 1f, 0.08f), GhostShape.Disc),
            new Ghost(1.95f, 1f, new Color(1f, 0.7f, 0.5f, 0.09f), GhostShape.Ring),
        };

        private SpriteRenderer burst;
        private float builtSpikes; // spikeStrength the burst texture was drawn with
        private SpriteRenderer[] ghostRenderers;
        private bool visible;

        private void Awake()
        {
            if (weather == null) weather = GetComponent<WeatherDirector>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (flareShader == null) flareShader = Shader.Find("Hidden/TinyRoadTrip/FlareAdditive");
            var material = new Material(flareShader);

            builtSpikes = spikeStrength;
            burst = CreateElement("LensFlareBurst", ToSprite(CreateBurstTexture(128, spikeStrength)), material);

            Sprite disc = ToSprite(CreateDiscTexture(64));
            Sprite hexagon = ToSprite(CreateHexagonTexture(64));
            Sprite ring = ToSprite(WeatherDirector.CreateRingTexture(64));
            ghostRenderers = new SpriteRenderer[ghosts.Length];
            for (int i = 0; i < ghosts.Length; i++)
            {
                Sprite sprite = ghosts[i].Shape == GhostShape.Hexagon ? hexagon : ghosts[i].Shape == GhostShape.Ring ? ring : disc;
                ghostRenderers[i] = CreateElement("LensFlareGhost" + i, sprite, material);
            }
        }

        private void LateUpdate()
        {
            float level = weather != null && weather.Kind == WeatherKind.Summer ? weather.Visual : 0f;
            level *= overallStrength;
            bool show = level > 0.001f && targetCamera != null;
            if (show != visible)
            {
                visible = show;
                burst.enabled = show;
                foreach (SpriteRenderer r in ghostRenderers) r.enabled = show;
            }
            if (!show) return;
            if (!Mathf.Approximately(builtSpikes, spikeStrength)) RebuildBurst();

            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            Vector3 cam = targetCamera.transform.position;
            Vector2 centre = new Vector2(cam.x, cam.y);

            float drift = Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(1f, sunDriftSeconds)) * sunDrift;
            Vector2 sun = centre + new Vector2((sunPosition.x + drift) * halfWidth, sunPosition.y * halfHeight);

            // The sun breathes slowly so it feels alive.
            float shimmer = 1f + pulse * (Mathf.Sin(Time.time * 0.8f) - 1f);

            float size = burstSize * halfWidth;
            burst.transform.position = new Vector3(sun.x, sun.y, 0f);
            burst.transform.localScale = new Vector3(size, size, 1f);
            burst.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * burstSpin);
            Color c = burstColor;
            c.a *= level * burstStrength * shimmer;
            burst.color = c;

            int count = Mathf.Min(ghosts.Length, ghostRenderers.Length);
            for (int i = 0; i < count; i++)
            {
                Transform t = ghostRenderers[i].transform;
                Vector2 pos = Vector2.LerpUnclamped(sun, centre, ghosts[i].Position);
                t.position = new Vector3(pos.x, pos.y, 0f);
                float ghostSize = ghosts[i].Size * ghostScale * halfWidth;
                t.localScale = new Vector3(ghostSize, ghostSize, 1f);
                c = ghosts[i].Color;
                c.a *= level * ghostStrength * shimmer;
                ghostRenderers[i].color = c;
            }
        }

        private void RebuildBurst()
        {
            builtSpikes = spikeStrength;
            Sprite old = burst.sprite;
            burst.sprite = ToSprite(CreateBurstTexture(128, spikeStrength));
            Destroy(old.texture);
            Destroy(old);
        }

        private SpriteRenderer CreateElement(string name, Sprite sprite, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.sortingOrder = 6; // above road, cars and speed lines
            sr.enabled = false;
            return sr;
        }

        private static Sprite ToSprite(Texture2D tex) =>
            Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width, 0, SpriteMeshType.FullRect);

        // Hot core, soft glow, six long spikes and fine streaks between them.
        private static Texture2D CreateBurstTexture(int size, float spikeBrightness) =>
            WeatherDirector.CreateTexture(size, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                if (r >= 1f) return 0f;
                float fade = 1f - r;
                float angle = Mathf.Atan2(v, u);
                float core = Mathf.Clamp01((0.1f - r) / 0.05f);
                float glow = fade * fade * fade * fade * 0.6f;
                float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 3f)), 60f) * fade * fade * spikeBrightness;
                float streaks = (0.5f + 0.5f * Mathf.Sin(angle * 23f) * Mathf.Sin(angle * 37f)) * fade * fade * fade * 0.25f;
                return core + glow + spikes + streaks;
            });

        // Flat disc, a little brighter at the rim like a real lens ghost.
        private static Texture2D CreateDiscTexture(int size) =>
            WeatherDirector.CreateTexture(size, (u, v) => Ghosted(Mathf.Sqrt(u * u + v * v)));

        private static Texture2D CreateHexagonTexture(int size) =>
            WeatherDirector.CreateTexture(size, (u, v) =>
                Ghosted(Mathf.Max(Mathf.Abs(u) * 0.866f + Mathf.Abs(v) * 0.5f, Mathf.Abs(v))));

        // d: 0 at the centre, 0.85 at the edge.
        private static float Ghosted(float d)
        {
            float edge = Mathf.Clamp01((0.85f - d) / 0.05f);
            float t = d / 0.85f;
            return edge * (0.6f + 0.4f * t * t * t);
        }
    }
}
