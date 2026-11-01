using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Runs the run's WeatherSchedule and draws it, all fading with the schedule's Visual level:
    //   Rain:   blue tint, slanted streaks, splash rings on the road.
    //   Snow:   pale tint, drifting flakes, flakes settling on the road.
    //   Spring: rosy tint, fluttering petals.
    //   Summer: warm tint, sun glare in the top corner, heat motes rising off the road.
    //   Fall:   amber tint, spinning leaves, leaves settling on the road.
    // Every texture is generated here at startup, so the weathers add no art files to the build.
    // Gameplay scripts read SteerFactor / DistanceFactor; the world speed is set here. All follow Rule.
    // Visuals start during the warning, before any rule applies, so the player sees the weather coming.
    public class WeatherDirector : MonoBehaviour
    {
        [SerializeField] private WeatherSchedule schedule = new WeatherSchedule();
        [Tooltip("Optional. Weather only rolls in during a calm traffic phase.")]
        [SerializeField] private Spawner spawner;
        [SerializeField] private Camera targetCamera;
        [Tooltip("Optional. Splashes and settled snow / leaves ride the road down the screen at its speed.")]
        [SerializeField] private BackgroundScroller road;
        [Tooltip("Optional. Snow and summer change the world speed through it (stacks with Slow-Time / Nitro).")]
        [SerializeField] private GameplaySpeedController speedController;

        [Header("Rain")]
        [SerializeField] private Color rainTint = new Color(0.12f, 0.18f, 0.32f, 0.32f);
        [SerializeField] private Color rainDropColor = new Color(0.75f, 0.85f, 1f, 0.45f);
        [SerializeField] private float rainEmissionRate = 140f;
        [SerializeField] private float rainSpeed = 16f;
        [Tooltip("Sideways slant of the streaks, world units per second.")]
        [SerializeField] private float rainSlant = -2.5f;

        [Header("Splashes")]
        [SerializeField] private Color splashColor = new Color(0.8f, 0.9f, 1f, 0.55f);
        [SerializeField] private float splashEmissionRate = 45f;
        [Tooltip("Largest ring size, world units.")]
        [SerializeField] private float splashSize = 0.28f;

        [Header("Snow")]
        [SerializeField] private Color snowTint = new Color(0.85f, 0.92f, 1f, 0.22f);
        [SerializeField] private Color snowflakeColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private float snowEmissionRate = 55f;
        [SerializeField] private float snowFallSpeed = 1.6f;
        [Tooltip("How much the flakes wander sideways.")]
        [SerializeField] private float snowWobble = 0.6f;
        [Tooltip("Flakes that land and ride the road before melting away, per second.")]
        [SerializeField] private float settledEmissionRate = 30f;

        [Header("Spring")]
        [SerializeField] private Color springTint = new Color(1f, 0.82f, 0.9f, 0.12f);
        [SerializeField] private Color petalColorA = new Color(1f, 0.72f, 0.82f, 0.95f);
        [SerializeField] private Color petalColorB = new Color(1f, 0.95f, 0.97f, 0.95f);
        [SerializeField] private float petalEmissionRate = 18f;
        [SerializeField] private float petalFallSpeed = 1.1f;
        [SerializeField] private float petalWobble = 0.9f;

        [Header("Summer")]
        [SerializeField] private Color summerTint = new Color(1f, 0.8f, 0.45f, 0.13f);
        [SerializeField] private Color glareColor = new Color(1f, 0.93f, 0.7f, 0.45f);
        [SerializeField] private Color moteColor = new Color(1f, 0.95f, 0.8f, 0.5f);
        [SerializeField] private float moteEmissionRate = 14f;

        [Header("Fall")]
        [SerializeField] private Color fallTint = new Color(1f, 0.6f, 0.25f, 0.13f);
        [SerializeField] private Gradient leafColors = DefaultLeafColors();
        [SerializeField] private float leafEmissionRate = 14f;
        [SerializeField] private float leafFallSpeed = 1.5f;
        [SerializeField] private float leafWobble = 1.1f;
        [Tooltip("Leaves that land and ride the road, per second.")]
        [SerializeField] private float settledLeafEmissionRate = 10f;

        private readonly System.Random random = new System.Random();
        private SpriteRenderer tint;
        private SpriteRenderer glare;
        private ParticleSystem rain;
        private ParticleSystem splashes;
        private ParticleSystem snow;
        private ParticleSystem settledSnow;
        private ParticleSystem petals;
        private ParticleSystem motes;
        private ParticleSystem leaves;
        private ParticleSystem settledLeaves;
        private float roadDrift;
        private float appliedWorldSpeed = 1f;

        public WeatherKind Kind => schedule.Kind;
        public WeatherSchedule.Phase State => schedule.State;
        public float Visual => schedule.Visual;
        public float Rule => schedule.Rule;

        public float SteerFactor => WeatherRules.SteerFactor(schedule.Kind, schedule.Rule);
        public float DistanceFactor => WeatherRules.DistanceFactor(schedule.Kind, schedule.Rule);

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            schedule.Reset();

            Texture2D dot = CreateDotTexture(16);
            Texture2D petal = CreatePetalTexture(16);
            Texture2D leaf = CreateLeafTexture(24);

            tint = CreateTint();
            glare = CreateGlare(CreateGlowTexture(64));
            rain = CreateRain();
            splashes = CreateSplashes();
            snow = CreateFalling("WeatherSnow", dot, new ParticleSystem.MinMaxGradient(snowflakeColor), 0.06f, 0.16f, snowFallSpeed, snowWobble, 0f, 400);
            settledSnow = CreateSettled("WeatherSettledSnow", dot, new ParticleSystem.MinMaxGradient(snowflakeColor), 0.05f, 0.12f, 1.2f, 2f, 120);
            petals = CreateFalling("WeatherPetals", petal, new ParticleSystem.MinMaxGradient(petalColorA, petalColorB), 0.1f, 0.18f, petalFallSpeed, petalWobble, 160f, 150);
            motes = CreateMotes(dot);
            var leafGradient = new ParticleSystem.MinMaxGradient(leafColors) { mode = ParticleSystemGradientMode.RandomColor };
            leaves = CreateFalling("WeatherLeaves", leaf, leafGradient, 0.16f, 0.28f, leafFallSpeed, leafWobble, 200f, 120);
            settledLeaves = CreateSettled("WeatherSettledLeaves", leaf, leafGradient, 0.16f, 0.26f, 2f, 3f, 80);
            Apply();
        }

        private void Update()
        {
            schedule.Tick(Time.deltaTime, spawner == null || spawner.IsCalmPhase, random);
        }

        private void LateUpdate()
        {
            FollowCamera();
            Apply();
        }

        private float Level(WeatherKind kind) => schedule.Kind == kind ? schedule.Visual : 0f;

        private void Apply()
        {
            float rainLevel = Level(WeatherKind.Rain);
            float snowLevel = Level(WeatherKind.Snow);
            float springLevel = Level(WeatherKind.Spring);
            float summerLevel = Level(WeatherKind.Summer);
            float fallLevel = Level(WeatherKind.Fall);

            // Only one weather at a time, so the tint is whichever one is active.
            Color c = TintFor(schedule.Kind);
            c.a *= schedule.Visual;
            tint.color = c;
            tint.enabled = c.a > 0.001f;

            // Summer glare breathes slowly so the sun feels alive.
            Color g = glareColor;
            g.a *= summerLevel * (0.85f + 0.15f * Mathf.Sin(Time.time * 0.8f));
            glare.color = g;
            glare.enabled = g.a > 0.001f;

            SetRate(rain, rainEmissionRate * rainLevel);
            SetRate(splashes, splashEmissionRate * rainLevel);
            SetRate(snow, snowEmissionRate * snowLevel);
            SetRate(settledSnow, settledEmissionRate * snowLevel);
            SetRate(petals, petalEmissionRate * springLevel);
            SetRate(motes, moteEmissionRate * summerLevel);
            SetRate(leaves, leafEmissionRate * fallLevel);
            SetRate(settledLeaves, settledLeafEmissionRate * fallLevel);

            float drift = road != null && road.rb != null ? road.rb.linearVelocity.y : 0f;
            if (!Mathf.Approximately(drift, roadDrift))
            {
                roadDrift = drift;
                SetDrift(splashes, drift);
                SetDrift(settledSnow, drift);
                SetDrift(settledLeaves, drift);
            }

            ApplyWorldSpeed();
        }

        private Color TintFor(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Rain: return rainTint;
                case WeatherKind.Snow: return snowTint;
                case WeatherKind.Spring: return springTint;
                case WeatherKind.Summer: return summerTint;
                case WeatherKind.Fall: return fallTint;
                default: return Color.clear;
            }
        }

        private void ApplyWorldSpeed()
        {
            if (speedController == null) return;
            float factor = WeatherRules.WorldSpeedFactor(schedule.Kind, schedule.Rule);
            bool backToNormal = factor == 1f && appliedWorldSpeed != 1f;
            if (!backToNormal && Mathf.Abs(factor - appliedWorldSpeed) < 0.005f) return;
            appliedWorldSpeed = factor;
            if (factor == 1f) speedController.ClearModifier(SpeedSource.Weather);
            else speedController.SetModifier(SpeedSource.Weather, factor);
        }

        private static void SetRate(ParticleSystem ps, float rate)
        {
            var emission = ps.emission;
            if (!Mathf.Approximately(emission.rateOverTime.constant, rate)) emission.rateOverTime = rate;
        }

        private static void SetDrift(ParticleSystem ps, float drift)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.y = new ParticleSystem.MinMaxCurve(drift, drift);
        }

        // Inspector (component menu, Play mode): skip the wait and start a weather now.
        [ContextMenu("Start Rain Now")] private void StartRainNow() => schedule.Begin(WeatherKind.Rain);
        [ContextMenu("Start Snow Now")] private void StartSnowNow() => schedule.Begin(WeatherKind.Snow);
        [ContextMenu("Start Spring Now")] private void StartSpringNow() => schedule.Begin(WeatherKind.Spring);
        [ContextMenu("Start Summer Now")] private void StartSummerNow() => schedule.Begin(WeatherKind.Summer);
        [ContextMenu("Start Fall Now")] private void StartFallNow() => schedule.Begin(WeatherKind.Fall);

        private void FollowCamera()
        {
            if (targetCamera == null) return;
            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            Vector3 cam = targetCamera.transform.position;

            tint.transform.position = new Vector3(cam.x, cam.y, 0f);
            tint.transform.localScale = new Vector3(halfWidth * 2f + 1f, halfHeight * 2f + 1f, 1f);

            float glareSize = halfWidth * 2.2f;
            glare.transform.position = new Vector3(cam.x + halfWidth * 0.9f, cam.y + halfHeight * 0.95f, 0f);
            glare.transform.localScale = new Vector3(glareSize, glareSize, 1f);

            // Emit above the top edge, a little wider than the view so slanted streaks cover the far side too.
            rain.transform.position = new Vector3(cam.x - rainSlant * 0.3f, cam.y + halfHeight + 1f, 0f);
            var shape = rain.shape;
            shape.scale = new Vector3(halfWidth * 2f + Mathf.Abs(rainSlant), 0.1f, 1f);

            CoverScreen(splashes, cam, halfWidth, halfHeight);
            CoverScreen(settledSnow, cam, halfWidth, halfHeight);
            CoverScreen(settledLeaves, cam, halfWidth, halfHeight);
            CoverScreen(motes, cam, halfWidth, halfHeight);

            AboveScreen(snow, snowFallSpeed, cam, halfWidth, halfHeight);
            AboveScreen(petals, petalFallSpeed, cam, halfWidth, halfHeight);
            AboveScreen(leaves, leafFallSpeed, cam, halfWidth, halfHeight);
        }

        private static void CoverScreen(ParticleSystem ps, Vector3 cam, float halfWidth, float halfHeight)
        {
            ps.transform.position = new Vector3(cam.x, cam.y, 0f);
            var shape = ps.shape;
            shape.scale = new Vector3(halfWidth * 2f, halfHeight * 2f, 0f);
        }

        // Emitter just above the top edge; lifetime long enough for the slowest particle to reach the bottom.
        private static void AboveScreen(ParticleSystem ps, float fallSpeed, Vector3 cam, float halfWidth, float halfHeight)
        {
            ps.transform.position = new Vector3(cam.x, cam.y + halfHeight + 0.5f, 0f);
            var shape = ps.shape;
            shape.scale = new Vector3(halfWidth * 2f + 1f, 0.1f, 1f);
            var main = ps.main;
            main.startLifetime = (halfHeight * 2f + 1f) / Mathf.Max(0.1f, fallSpeed * 0.6f);
        }

        private SpriteRenderer CreateTint()
        {
            var go = new GameObject("WeatherTint");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            Texture2D tex = Texture2D.whiteTexture;
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            sr.sortingOrder = 5; // above road, cars and speed lines
            sr.enabled = false;
            return sr;
        }

        // Soft sun glow in the top corner for summer.
        private SpriteRenderer CreateGlare(Texture2D glow)
        {
            var go = new GameObject("WeatherSunGlare");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(glow, new Rect(0, 0, glow.width, glow.height), new Vector2(0.5f, 0.5f), glow.width);
            sr.sortingOrder = 6; // above the tint
            sr.enabled = false;
            return sr;
        }

        private ParticleSystem CreateRain()
        {
            ParticleSystem ps = NewSystem("WeatherRain", 250);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
            main.startColor = rainDropColor;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6f, 0.1f, 1f);

            // velocityOverLifetime rather than startSpeed so the slant is exact.
            SetVelocity(ps, new ParticleSystem.MinMaxCurve(rainSlant, rainSlant), new ParticleSystem.MinMaxCurve(-rainSpeed * 1.1f, -rainSpeed * 0.9f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 1f;
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.sortingOrder = 6; // above the tint

            ps.Play();
            return ps;
        }

        // Small rings that pop up anywhere on screen, grow and fade, drifting down with the road.
        private ParticleSystem CreateSplashes()
        {
            ParticleSystem ps = NewSystem("WeatherSplashes", 80);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.32f);
            main.startSize = new ParticleSystem.MinMaxCurve(splashSize * 0.6f, splashSize);
            main.startColor = splashColor;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6f, 10f, 0f);
            SetVelocity(ps, new ParticleSystem.MinMaxCurve(0f, 0f), new ParticleSystem.MinMaxCurve(0f, 0f));

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f));
            SetFade(ps, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });

            SetRenderer(ps, CreateRingTexture(32), 1); // above road and cars (rain lands on car roofs too), below the tint
            ps.Play();
            return ps;
        }

        // Flakes, petals or leaves drifting down and sideways across the screen. spin: max degrees per second.
        private ParticleSystem CreateFalling(string name, Texture2D texture, ParticleSystem.MinMaxGradient color,
            float sizeMin, float sizeMax, float fallSpeed, float wobble, float spin, int maxParticles)
        {
            ParticleSystem ps = NewSystem(name, maxParticles);
            var main = ps.main;
            main.startLifetime = 8f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color;
            if (spin > 0f) main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6f, 0.1f, 1f);

            // Wide per-particle spread in fall speed and drift, so they pass through the noise field at different
            // rates and never sway in step.
            SetVelocity(ps, new ParticleSystem.MinMaxCurve(-0.35f, 0.35f), new ParticleSystem.MinMaxCurve(-fallSpeed * 1.4f, -fallSpeed * 0.6f));

            // Small-scale turbulence (about one gap between particles per feature) instead of one big slow wave,
            // with a random sway strength per particle: mostly sideways, a little up and down.
            var noise = ps.noise;
            noise.enabled = true;
            noise.separateAxes = true;
            noise.strengthX = new ParticleSystem.MinMaxCurve(wobble * 0.3f, wobble * 1.2f);
            noise.strengthY = new ParticleSystem.MinMaxCurve(0f, wobble * 0.3f);
            noise.strengthZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            noise.frequency = 1.2f;
            noise.octaveCount = 2;
            noise.scrollSpeed = 0.15f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            if (spin > 0f)
            {
                var rotation = ps.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-spin * Mathf.Deg2Rad, spin * Mathf.Deg2Rad);
            }

            SetRenderer(ps, texture, 6); // above the tint
            ps.Play();
            return ps;
        }

        // Things that have landed: they appear on the road, ride it down and fade.
        private ParticleSystem CreateSettled(string name, Texture2D texture, ParticleSystem.MinMaxGradient color,
            float sizeMin, float sizeMax, float lifeMin, float lifeMax, int maxParticles)
        {
            ParticleSystem ps = NewSystem(name, maxParticles);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6f, 10f, 0f);
            SetVelocity(ps, new ParticleSystem.MinMaxCurve(0f, 0f), new ParticleSystem.MinMaxCurve(0f, 0f));
            SetFade(ps, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.1f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });

            SetRenderer(ps, texture, 1); // on the road and car roofs, below the tint
            ps.Play();
            return ps;
        }

        // Summer heat: faint specks that rise slowly off the road and twinkle out.
        private ParticleSystem CreateMotes(Texture2D dot)
        {
            ParticleSystem ps = NewSystem("WeatherHeatMotes", 80);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = moteColor;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6f, 10f, 0f);
            SetVelocity(ps, new ParticleSystem.MinMaxCurve(-0.1f, 0.1f), new ParticleSystem.MinMaxCurve(0.15f, 0.45f));
            SetFade(ps, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f) });

            SetRenderer(ps, dot, 6);
            ps.Play();
            return ps;
        }

        private ParticleSystem NewSystem(string name, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;

            var em = ps.emission;
            em.rateOverTime = 0f;
            return ps;
        }

        // All three axes must use the same curve mode (two constants).
        private static void SetVelocity(ParticleSystem ps, ParticleSystem.MinMaxCurve x, ParticleSystem.MinMaxCurve y)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = x;
            velocity.y = y;
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        private static void SetFade(ParticleSystem ps, GradientAlphaKey[] alpha)
        {
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
            colorOverLifetime.color = gradient;
        }

        private static void SetRenderer(ParticleSystem ps, Texture2D texture, int sortingOrder)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var material = new Material(Shader.Find("Sprites/Default"));
            material.mainTexture = texture;
            renderer.material = material;
            renderer.sortingOrder = sortingOrder;
        }

        private static Gradient DefaultLeafColors()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.85f, 0.25f, 0.1f), 0f),   // red
                    new GradientColorKey(new Color(0.95f, 0.5f, 0.12f), 0.35f), // orange
                    new GradientColorKey(new Color(0.98f, 0.78f, 0.2f), 0.7f),  // yellow
                    new GradientColorKey(new Color(0.55f, 0.32f, 0.15f), 1f),   // brown
                },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.95f, 1f) });
            return gradient;
        }

        // Texture with alpha from shape(u, v), where u and v run from -1 to 1 across the texture.
        private static Texture2D CreateTexture(int size, System.Func<float, float, float> shape)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f - half) / half;
                    float v = (y + 0.5f - half) / half;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(shape(u, v)) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // White disc with a soft edge.
        private static Texture2D CreateDotTexture(int size) =>
            CreateTexture(size, (u, v) => (1f - Mathf.Sqrt(u * u + v * v)) / 0.4f);

        // White ring with soft edges.
        private static Texture2D CreateRingTexture(int size) =>
            CreateTexture(size, (u, v) => 1f - Mathf.Abs(Mathf.Sqrt(u * u + v * v) - 0.8f) / 0.15f);

        // Rounded petal: an ellipse a little narrower at one end.
        private static Texture2D CreatePetalTexture(int size) =>
            CreateTexture(size, (u, v) =>
            {
                float width = 0.55f + 0.2f * v; // wider towards the top
                float d = Mathf.Sqrt((u / width) * (u / width) + v * v);
                return (1f - d) / 0.25f;
            });

        // Pointed leaf with a faint centre vein.
        private static Texture2D CreateLeafTexture(int size) =>
            CreateTexture(size, (u, v) =>
            {
                float halfWidth = 0.55f * (1f - v * v); // pointed at both ends
                if (halfWidth <= 0f) return 0f;
                float edge = (halfWidth - Mathf.Abs(u)) / 0.12f;
                float vein = Mathf.Abs(u) < 0.05f ? 0.7f : 1f;
                return Mathf.Min(edge, 1f) * vein;
            });

        // Soft radial glow: bright centre fading to nothing at the edge.
        private static Texture2D CreateGlowTexture(int size) =>
            CreateTexture(size, (u, v) =>
            {
                float d = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                return (1f - d) * (1f - d);
            });
    }
}
