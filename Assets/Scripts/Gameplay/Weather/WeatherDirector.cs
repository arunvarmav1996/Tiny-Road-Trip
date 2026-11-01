using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Runs the run's WeatherSchedule and draws it, all fading with the schedule's Visual level:
    //   Rain: blue tint, slanted streaks, splash rings on the road.
    //   Snow: pale tint, drifting flakes, flakes settling on the road.
    // Gameplay scripts read SteerFactor / DistanceFactor; the world speed is set here. All follow Rule.
    // Visuals start during the warning, before any rule applies, so the player sees the weather coming.
    public class WeatherDirector : MonoBehaviour
    {
        [SerializeField] private WeatherSchedule schedule = new WeatherSchedule();
        [Tooltip("Optional. Weather only rolls in during a calm traffic phase.")]
        [SerializeField] private Spawner spawner;
        [SerializeField] private Camera targetCamera;
        [Tooltip("Optional. Splashes ride the road down the screen at its speed.")]
        [SerializeField] private BackgroundScroller road;
        [Tooltip("Optional. Snow slows the world through it (stacks with Slow-Time / Nitro).")]
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

        private readonly System.Random random = new System.Random();
        private SpriteRenderer tint;
        private ParticleSystem rain;
        private ParticleSystem.EmissionModule rainEmission;
        private ParticleSystem splashes;
        private ParticleSystem.EmissionModule splashEmission;
        private ParticleSystem snow;
        private ParticleSystem.EmissionModule snowEmission;
        private ParticleSystem settled;
        private ParticleSystem.EmissionModule settledEmission;
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
            tint = CreateTint();
            rain = CreateRain();
            rainEmission = rain.emission;
            splashes = CreateSplashes();
            splashEmission = splashes.emission;
            Texture2D dot = CreateDotTexture(16);
            snow = CreateSnow(dot);
            snowEmission = snow.emission;
            settled = CreateSettled(dot);
            settledEmission = settled.emission;
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

        private void Apply()
        {
            float rainLevel = schedule.Kind == WeatherKind.Rain ? schedule.Visual : 0f;
            float snowLevel = schedule.Kind == WeatherKind.Snow ? schedule.Visual : 0f;

            // Only one weather at a time, so the tint is whichever one is active.
            Color c = snowLevel > 0f ? snowTint : rainTint;
            c.a *= Mathf.Max(rainLevel, snowLevel);
            tint.color = c;
            tint.enabled = c.a > 0.001f;

            SetRate(ref rainEmission, rainEmissionRate * rainLevel);
            SetRate(ref splashEmission, splashEmissionRate * rainLevel);
            SetRate(ref snowEmission, snowEmissionRate * snowLevel);
            SetRate(ref settledEmission, settledEmissionRate * snowLevel);

            float drift = road != null && road.rb != null ? road.rb.linearVelocity.y : 0f;
            if (!Mathf.Approximately(drift, roadDrift))
            {
                roadDrift = drift;
                var splashVelocity = splashes.velocityOverLifetime;
                splashVelocity.y = new ParticleSystem.MinMaxCurve(drift, drift);
                var settledVelocity = settled.velocityOverLifetime;
                settledVelocity.y = new ParticleSystem.MinMaxCurve(drift, drift);
            }

            ApplyWorldSpeed();
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

        private static void SetRate(ref ParticleSystem.EmissionModule emission, float rate)
        {
            if (!Mathf.Approximately(emission.rateOverTime.constant, rate)) emission.rateOverTime = rate;
        }

        // Inspector (component menu, Play mode): skip the wait and start rain now.
        [ContextMenu("Start Rain Now")]
        private void StartRainNow()
        {
            schedule.Begin(WeatherKind.Rain);
        }

        [ContextMenu("Start Snow Now")]
        private void StartSnowNow()
        {
            schedule.Begin(WeatherKind.Snow);
        }

        private void FollowCamera()
        {
            if (targetCamera == null) return;
            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            Vector3 cam = targetCamera.transform.position;

            tint.transform.position = new Vector3(cam.x, cam.y, 0f);
            tint.transform.localScale = new Vector3(halfWidth * 2f + 1f, halfHeight * 2f + 1f, 1f);

            // Emit above the top edge, a little wider than the view so slanted streaks cover the far side too.
            rain.transform.position = new Vector3(cam.x - rainSlant * 0.3f, cam.y + halfHeight + 1f, 0f);
            var shape = rain.shape;
            shape.scale = new Vector3(halfWidth * 2f + Mathf.Abs(rainSlant), 0.1f, 1f);

            splashes.transform.position = new Vector3(cam.x, cam.y, 0f);
            var splashShape = splashes.shape;
            splashShape.scale = new Vector3(halfWidth * 2f, halfHeight * 2f, 0f);

            snow.transform.position = new Vector3(cam.x, cam.y + halfHeight + 0.5f, 0f);
            var snowShape = snow.shape;
            snowShape.scale = new Vector3(halfWidth * 2f + 1f, 0.1f, 1f);
            var snowMain = snow.main;
            snowMain.startLifetime = (halfHeight * 2f + 1f) / Mathf.Max(0.1f, snowFallSpeed * 0.8f); // slowest flake still reaches the bottom

            settled.transform.position = new Vector3(cam.x, cam.y, 0f);
            var settledShape = settled.shape;
            settledShape.scale = new Vector3(halfWidth * 2f, halfHeight * 2f, 0f);
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

        private ParticleSystem CreateRain()
        {
            var go = new GameObject("WeatherRain");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1f);
            main.startSpeed = 0f; // velocity comes from velocityOverLifetime so the slant is exact
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
            main.startColor = rainDropColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 250;

            var em = ps.emission;
            em.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6f, 0.1f, 1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // All three axes must use the same curve mode (two constants).
            velocity.x = new ParticleSystem.MinMaxCurve(rainSlant, rainSlant);
            velocity.y = new ParticleSystem.MinMaxCurve(-rainSpeed * 1.1f, -rainSpeed * 0.9f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
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
            var go = new GameObject("WeatherSplashes");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.32f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(splashSize * 0.6f, splashSize);
            main.startColor = splashColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;

            var em = ps.emission;
            em.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6f, 10f, 0f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var material = new Material(Shader.Find("Sprites/Default"));
            material.mainTexture = CreateRingTexture(32);
            renderer.material = material;
            renderer.sortingOrder = 1; // above road and cars (rain lands on car roofs too), below the tint

            ps.Play();
            return ps;
        }

        // Soft round flakes drifting down and sideways across the screen.
        private ParticleSystem CreateSnow(Texture2D dot)
        {
            var go = new GameObject("WeatherSnow");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 8f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = snowflakeColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            var em = ps.emission;
            em.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6f, 0.1f, 1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(-snowFallSpeed * 1.2f, -snowFallSpeed * 0.8f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = snowWobble;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.3f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var material = new Material(Shader.Find("Sprites/Default"));
            material.mainTexture = dot;
            renderer.material = material;
            renderer.sortingOrder = 6; // above the tint

            ps.Play();
            return ps;
        }

        // Flakes that have landed: small dots that appear on the road, ride it down and melt.
        private ParticleSystem CreateSettled(Texture2D dot)
        {
            var go = new GameObject("WeatherSettledSnow");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = snowflakeColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;

            var em = ps.emission;
            em.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6f, 10f, 0f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.1f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var material = new Material(Shader.Find("Sprites/Default"));
            material.mainTexture = dot;
            renderer.material = material;
            renderer.sortingOrder = 1; // on the road and car roofs, below the tint

            ps.Play();
            return ps;
        }

        // White disc with a soft edge on a transparent background.
        private static Texture2D CreateDotTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    float a = Mathf.Clamp01((1f - d) / 0.4f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // White ring with soft edges on a transparent background.
        private static Texture2D CreateRingTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.15f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
