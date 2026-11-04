using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Rain puddles: patches of water on the road while it rains. Any car (traffic or the player) that drives
    // through one throws spray out of both sides and leaves ripples on the water. Raindrops ring them too.
    // Every texture is generated at startup, like the rest of the weather.
    public class RainPuddles : RoadPatches
    {
        [SerializeField] private PatchSettings puddles = new PatchSettings();
        [Tooltip("Raindrop rings per second inside each puddle.")]
        [SerializeField] private float raindropRipplesPerPuddle = 3f;
        [SerializeField] private Color rippleColor = new Color(0.8f, 0.9f, 1f, 0.6f);

        [Header("Splash")]
        [SerializeField] private Color sprayColor = new Color(0.82f, 0.9f, 1f, 0.85f);
        [Tooltip("Droplets thrown from each side of a car the moment it hits a puddle.")]
        [SerializeField] private int burstDroplets = 12;
        [Tooltip("Droplets per second from each side while a car is in a puddle.")]
        [SerializeField] private float trailDropletsPerSecond = 35f;
        [Tooltip("Sideways speed of the spray, world units per second.")]
        [SerializeField] private Vector2 sprayOutSpeed = new Vector2(1.4f, 3.4f);
        [SerializeField] private Vector2 dropletSize = new Vector2(0.05f, 0.12f);

        private ParticleSystem spray;
        private ParticleSystem ripples;

        protected override WeatherKind Kind => WeatherKind.Rain;
        protected override PatchSettings Settings => puddles;

        protected override void Awake()
        {
            base.Awake();
            spray = CreateParticles("RainSpray", WeatherDirector.CreateDotTexture(16), 300, 1); // above cars: water thrown up
            SetDrag(spray, 4f); // spray flies out fast and slows hard
            SetSizeCurve(spray, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.35f, 1.15f), new Keyframe(1f, 0.4f)));
            SetFade(spray, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });

            ripples = CreateParticles("RainPuddleRipples", WeatherDirector.CreateRingTexture(32), 120, 0);
            ripples.transform.position = new Vector3(0f, 0f, OnRoadZ);
            SetSizeCurve(ripples, AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f));
            SetFade(ripples, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
        }

        protected override void OnPatch(Vector3 pos, Vector2 halfSize, float weatherLevel, float drift, float dt)
        {
            if (Random.value >= raindropRipplesPerPuddle * weatherLevel * dt) return;
            Vector2 inside = Random.insideUnitCircle;
            EmitRipple(new Vector2(pos.x + inside.x * halfSize.x, pos.y + inside.y * halfSize.y), drift, 0.12f, 0.24f, 0.4f);
        }

        protected override void OnEnter(Car car, Vector3 patchPos, float drift) =>
            Spray(car.Collider.bounds, patchPos.y, drift, Mathf.RoundToInt(burstDroplets * Level), true);

        protected override void OnStay(Car car, Contact contact, Vector3 patchPos, float drift, float dt) =>
            Spray(car.Collider.bounds, patchPos.y, drift, Take(contact, trailDropletsPerSecond * Level, dt), false);

        // Water thrown out from both sides of the car where its tyres cut through the puddle, falling back with the road.
        private void Spray(Bounds car, float puddleY, float drift, int perSide, bool hit)
        {
            if (perSide <= 0) return;
            float y = Mathf.Clamp(puddleY, car.min.y, car.max.y);
            float inset = car.extents.x * 0.15f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side < 0 ? car.min.x + inset : car.max.x - inset;
                var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
                for (int i = 0; i < perSide; i++)
                {
                    emit.position = new Vector3(x + side * Random.Range(0f, 0.08f), y + Random.Range(-0.2f, 0.2f), 0f);
                    float outSpeed = Random.Range(sprayOutSpeed.x, sprayOutSpeed.y) * (hit ? 1f : 0.7f);
                    emit.velocity = new Vector3(side * outSpeed, drift * Random.Range(0.3f, 0.9f) + Random.Range(-0.6f, 1.2f), 0f);
                    emit.startSize = Random.Range(dropletSize.x, dropletSize.y);
                    emit.startLifetime = Random.Range(0.3f, 0.55f);
                    emit.startColor = sprayColor;
                    spray.Emit(emit, 1);
                }
                if (hit) EmitRipple(new Vector2(x, y), drift, 0.35f, 0.55f, 0.5f);
            }
        }

        private void EmitRipple(Vector2 at, float drift, float sizeMin, float sizeMax, float life)
        {
            var emit = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = new Vector3(at.x, at.y, OnRoadZ),
                velocity = new Vector3(0f, drift, 0f), // stays on the road
                startSize = Random.Range(sizeMin, sizeMax),
                startLifetime = life * Random.Range(0.8f, 1.2f),
                startColor = rippleColor,
            };
            ripples.Emit(emit, 1);
        }

        // Dark water: a brighter rim where it catches the light and a soft sky reflection across it.
        protected override Texture2D CreatePatchTexture(int size, int seed)
        {
            Color deep = new Color(0.2f, 0.26f, 0.36f, 0.6f);
            Color rim = new Color(0.6f, 0.7f, 0.85f, 0.75f);
            Color sky = new Color(0.75f, 0.85f, 1f, 0.7f);
            return CreateBlobTexture(size, seed, 0.08f, 1f, (u, v, d) =>
            {
                Color c = Color.Lerp(deep, rim, Mathf.SmoothStep(0f, 1f, (d - 0.75f) / 0.25f));
                float band = (u * 0.8f + v * 0.6f - 0.2f) / 0.22f;
                return Color.Lerp(c, sky, Mathf.Exp(-band * band) * 0.35f);
            });
        }
    }
}
