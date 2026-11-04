using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Runs the run's WeatherSchedule and draws it, all fading with the schedule's Visual level:
    //   Rain:   dark blue grade, slanted streaks, splash rings on the road.
    //   Snow:   pale cold grade, drifting flakes, flakes settling on the road.
    //   Spring: vivid rosy grade, fluttering petals.
    //   Summer: vivid golden grade, heat motes rising off the road, lens flare from the sun (SummerLensFlare).
    //   Fall:   vivid amber grade, spinning leaves, leaves settling on the road.
    // Each weather's screen grade (tint, saturation, contrast, brightness) is set in the inspector.
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
        [Tooltip("Hidden/TinyRoadTrip/WeatherGrade. Referenced here so the build includes it.")]
        [SerializeField] private Shader gradeShader;

        [Header("Rain")]
        [SerializeField] private ScreenGrade rainGrade = new ScreenGrade
            { tint = new Color(0.55f, 0.68f, 1f), tintStrength = 0.45f, saturation = 0.75f, contrast = 1.05f, brightness = 0.8f };
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
        [SerializeField] private ScreenGrade snowGrade = new ScreenGrade
            { tint = new Color(0.82f, 0.9f, 1f), tintStrength = 0.4f, saturation = 0.7f, contrast = 0.95f, brightness = 1.08f };
        [SerializeField] private Color snowflakeColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private float snowEmissionRate = 55f;
        [SerializeField] private float snowFallSpeed = 1.6f;
        [Tooltip("How much the flakes wander sideways.")]
        [SerializeField] private float snowWobble = 0.6f;
        [Tooltip("Flakes that land and ride the road before melting away, per second.")]
        [SerializeField] private float settledEmissionRate = 30f;

        [Header("Spring")]
        [SerializeField] private ScreenGrade springGrade = new ScreenGrade
            { tint = new Color(1f, 0.86f, 0.93f), tintStrength = 0.3f, saturation = 1.35f, contrast = 1.08f, brightness = 1.05f };
        [Tooltip("Each petal picks a random colour along this gradient.")]
        [SerializeField] private Gradient petalColors = DefaultPetalColors();
        [Tooltip("Average petals per second. The actual rate drifts with slow gusts around this.")]
        [SerializeField] private float petalEmissionRate = 6f;
        [Tooltip("0 = steady stream, 1 = petals come in clear gusts with near-empty lulls between.")]
        [Range(0f, 1f)]
        [SerializeField] private float petalGustiness = 0.8f;
        [SerializeField] private float petalFallSpeed = 1.1f;
        [SerializeField] private float petalWobble = 0.9f;

        [Header("Summer")]
        [SerializeField] private ScreenGrade summerGrade = new ScreenGrade
            { tint = new Color(1f, 0.86f, 0.55f), tintStrength = 0.35f, saturation = 1.4f, contrast = 1.12f, brightness = 1.08f };
        [Tooltip("Each heat mote picks a random colour along this gradient.")]
        [SerializeField] private Gradient moteColors = DefaultMoteColors();
        [SerializeField] private float moteEmissionRate = 24f;

        [Header("Fall")]
        [SerializeField] private ScreenGrade fallGrade = new ScreenGrade
            { tint = new Color(1f, 0.72f, 0.42f), tintStrength = 0.4f, saturation = 1.3f, contrast = 1.1f, brightness = 1f };
        [SerializeField] private Gradient leafColors = DefaultLeafColors();
        [SerializeField] private float leafEmissionRate = 14f;
        [SerializeField] private float leafFallSpeed = 1.5f;
        [SerializeField] private float leafWobble = 1.1f;
        [Tooltip("Leaves that land and ride the road, per second.")]
        [SerializeField] private float settledLeafEmissionRate = 10f;

        private readonly System.Random random = new System.Random();
        private WeatherScreenFilter screenFilter;
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

            if (targetCamera != null)
            {
                if (gradeShader == null) gradeShader = Shader.Find("Hidden/TinyRoadTrip/WeatherGrade");
                screenFilter = targetCamera.gameObject.AddComponent<WeatherScreenFilter>();
                screenFilter.Init(gradeShader);
            }
            rain = CreateRain();
            splashes = CreateSplashes();
            snow = CreateFalling("WeatherSnow", dot, new ParticleSystem.MinMaxGradient(snowflakeColor), 0.06f, 0.16f, snowFallSpeed, snowWobble, 0f, 400);
            settledSnow = CreateSettled("WeatherSettledSnow", dot, new ParticleSystem.MinMaxGradient(snowflakeColor), 0.05f, 0.12f, 1.2f, 2f, 120);
            petals = CreateFalling("WeatherPetals", petal, RandomFrom(petalColors), 0.1f, 0.18f, petalFallSpeed, petalWobble, 160f, 150);
            motes = CreateMotes(dot);
            var leafGradient = RandomFrom(leafColors);
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

            // Only one weather at a time, so the grade is whichever one is active, faded in by Visual.
            if (screenFilter != null)
                screenFilter.Set(ScreenGrade.Lerp(ScreenGrade.Neutral, GradeFor(schedule.Kind), schedule.Visual));

            SetRate(rain, rainEmissionRate * rainLevel);
            SetRate(splashes, splashEmissionRate * rainLevel);
            SetRate(snow, snowEmissionRate * snowLevel);
            SetRate(settledSnow, settledEmissionRate * snowLevel);
            SetRate(petals, petalEmissionRate * springLevel * Gust(petalGustiness));
            SetRate(motes, moteEmissionRate * summerLevel);
            SetRate(leaves, leafEmissionRate * fallLevel);
            SetRate(settledLeaves, settledLeafEmissionRate * fallLevel);

            float drift = road != null ? road.Velocity : 0f;
            if (!Mathf.Approximately(drift, roadDrift))
            {
                roadDrift = drift;
                SetDrift(splashes, drift);
                SetDrift(settledSnow, drift);
                SetDrift(settledLeaves, drift);
            }

            ApplyWorldSpeed();
        }

        private ScreenGrade GradeFor(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Rain: return rainGrade;
                case WeatherKind.Snow: return snowGrade;
                case WeatherKind.Spring: return springGrade;
                case WeatherKind.Summer: return summerGrade;
                case WeatherKind.Fall: return fallGrade;
                default: return ScreenGrade.Neutral;
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

        // Slowly wandering multiplier averaging about 1: a breeze picks up and dies down every few seconds,
        // so petals arrive in loose drifts with quiet gaps instead of a steady stream.
        private static float Gust(float gustiness)
        {
            float breeze = Mathf.PerlinNoise(Time.time * 0.15f, 0.37f) * 2f - 1f; // smooth, -1..1
            return Mathf.Max(0f, 1f + breeze * 1.4f * gustiness);
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

            // Emit above the top edge. Lifetime lets the slowest streak reach the bottom whatever rainSpeed is set to.
            // The emitter is wider than the view by the sideways travel in that time, so the slant covers the far side.
            float rainLife = (halfHeight * 2f + 1.5f) / Mathf.Max(0.1f, rainSpeed * 0.9f);
            float rainTravel = rainSlant * rainLife;
            var rainMain = rain.main;
            rainMain.startLifetime = rainLife;
            rain.transform.position = new Vector3(cam.x - rainTravel * 0.5f, cam.y + halfHeight + 1f, 0f);
            var shape = rain.shape;
            shape.scale = new Vector3(halfWidth * 2f + Mathf.Abs(rainTravel), 0.1f, 1f);

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
            renderer.sortingOrder = 6; // above road, cars and speed lines

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

            SetRenderer(ps, CreateRingTexture(32), 1); // above road and cars (rain lands on car roofs too)
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

            SetRenderer(ps, texture, 6); // above road, cars and speed lines
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

            SetRenderer(ps, texture, 1); // on the road and car roofs
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
            main.startColor = RandomFrom(moteColors);

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

        // Each particle takes one random colour from the gradient (not a fade along its life).
        private static ParticleSystem.MinMaxGradient RandomFrom(Gradient gradient) =>
            new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };

        private static Gradient Palette(float alpha, params Color[] colors)
        {
            var keys = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                keys[i] = new GradientColorKey(colors[i], colors.Length == 1 ? 0f : i / (float)(colors.Length - 1));
            var gradient = new Gradient();
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) });
            return gradient;
        }

        private static Gradient DefaultPetalColors() => Palette(0.95f,
            new Color(1f, 0.72f, 0.82f),   // soft pink
            new Color(1f, 0.95f, 0.97f));  // blush white

        private static Gradient DefaultMoteColors() => Palette(0.7f,
            new Color(1f, 1f, 0.75f),      // pale gold
            new Color(1f, 0.85f, 0.3f),    // gold
            new Color(1f, 0.6f, 0.15f));   // orange

        private static Gradient DefaultLeafColors() => Palette(0.95f,
            new Color(0.9f, 0.12f, 0.08f), // red
            new Color(1f, 0.4f, 0.05f),    // red-orange
            new Color(1f, 0.6f, 0.1f),     // orange
            new Color(1f, 0.85f, 0.15f));  // yellow

        // Texture with alpha from shape(u, v), where u and v run from -1 to 1 across the texture.
        internal static Texture2D CreateTexture(int size, System.Func<float, float, float> shape)
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
        internal static Texture2D CreateDotTexture(int size) =>
            CreateTexture(size, (u, v) => (1f - Mathf.Sqrt(u * u + v * v)) / 0.4f);

        // White ring with soft edges.
        internal static Texture2D CreateRingTexture(int size) =>
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
    }
}
