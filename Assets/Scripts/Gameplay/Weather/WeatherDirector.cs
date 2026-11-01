using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Runs the run's WeatherSchedule and draws it: a screen tint and slanted rain streaks that both fade with
    // the schedule's Visual level. Gameplay scripts read SteerFactor / DistanceFactor, which follow Rule.
    // Visuals start during the warning, before any rule applies, so the player sees rain coming.
    public class WeatherDirector : MonoBehaviour
    {
        [SerializeField] private WeatherSchedule schedule = new WeatherSchedule();
        [Tooltip("Optional. Weather only rolls in during a calm traffic phase.")]
        [SerializeField] private Spawner spawner;
        [SerializeField] private Camera targetCamera;

        [Header("Rain")]
        [SerializeField] private Color rainTint = new Color(0.12f, 0.18f, 0.32f, 0.32f);
        [SerializeField] private Color rainDropColor = new Color(0.75f, 0.85f, 1f, 0.45f);
        [SerializeField] private float rainEmissionRate = 140f;
        [SerializeField] private float rainSpeed = 16f;
        [Tooltip("Sideways slant of the streaks, world units per second.")]
        [SerializeField] private float rainSlant = -2.5f;

        private readonly System.Random random = new System.Random();
        private SpriteRenderer tint;
        private ParticleSystem rain;
        private ParticleSystem.EmissionModule rainEmission;

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

            Color c = rainTint;
            c.a *= rainLevel;
            tint.color = c;
            tint.enabled = c.a > 0.001f;

            float rate = rainEmissionRate * rainLevel;
            if (!Mathf.Approximately(rainEmission.rateOverTime.constant, rate)) rainEmission.rateOverTime = rate;
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
    }
}
