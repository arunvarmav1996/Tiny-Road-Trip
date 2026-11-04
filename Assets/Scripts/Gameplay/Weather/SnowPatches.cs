using System.Collections.Generic;
using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Snow patches: drifts of packed snow on the road while it snows. Cars lose grip on them:
    //   Player:  a sudden sideways jolt on hitting one, then a slide that wanders left and right on its own,
    //            the tail fishtailing, and swipes answering slowly. Grip comes back once off the snow.
    //   Traffic: cars wander a little off their lane line and fishtail, then settle back.
    // Every car kicks up a puff of powder on the way through.
    public class SnowPatches : RoadPatches
    {
        [SerializeField] private PatchSettings snowPatches = new PatchSettings
        {
            PerSecond = 0.6f, Width = new Vector2(1f, 1.9f), Aspect = new Vector2(0.4f, 0.7f),
            Color = new Color(1f, 1f, 1f, 0.95f), FillSeconds = 4f,
        };

        [Header("Player slip")]
        [Tooltip("Lane-change speed on snow, as a fraction of normal (on top of the snow weather's own).")]
        [Range(0f, 1f)]
        [SerializeField] private float playerGrip = 0.3f;
        [Tooltip("Fastest the car slides sideways on its own, world units per second.")]
        [SerializeField] private float playerSlideSpeed = 1.6f;
        [Tooltip("Sideways jolt the moment the car hits a patch, world units per second, random side.")]
        [SerializeField] private float playerJolt = 1.4f;
        [Tooltip("Furthest the slide carries the car from the lane it is steering for, world units.")]
        [SerializeField] private float playerMaxSlide = 0.6f;
        [Tooltip("The car never slides past this distance from the road's middle.")]
        [SerializeField] private float playerMaxX = 2.3f;
        [SerializeField] private float playerMaxAngle = 10f;
        [Tooltip("Seconds for grip to come back after leaving the snow.")]
        [SerializeField] private float gripRecoverSeconds = 0.5f;

        [Header("Traffic slip")]
        [Tooltip("Furthest a traffic car wanders off its lane line, world units.")]
        [SerializeField] private float trafficMaxSlide = 0.25f;
        [SerializeField] private float trafficSlideSpeed = 0.9f;
        [SerializeField] private float trafficMaxAngle = 12f;

        [Header("Powder")]
        [SerializeField] private Color powderColor = new Color(1f, 1f, 1f, 0.9f);
        [Tooltip("Puffs from each rear wheel the moment a car hits a patch.")]
        [SerializeField] private int burstPuffs = 8;
        [Tooltip("Puffs per second from each rear wheel while on snow.")]
        [SerializeField] private float trailPuffsPerSecond = 18f;
        [SerializeField] private Vector2 puffSize = new Vector2(0.08f, 0.18f);

        private sealed class Skid
        {
            public float Seed;          // where this car reads the noise, so cars wander independently
            public int OnSnowFrame = -1;
            public float Velocity;      // player: sideways slide speed
            public float Offset;        // traffic: sideways offset from where the spawner put it
            public float Angle;
        }

        private readonly Dictionary<Collider2D, Skid> skids = new Dictionary<Collider2D, Skid>();
        private readonly List<Collider2D> finished = new List<Collider2D>();
        private PlayerController playerController;
        private ParticleSystem powder;

        protected override WeatherKind Kind => WeatherKind.Snow;
        protected override PatchSettings Settings => snowPatches;

        protected override void Awake()
        {
            base.Awake();
            if (player != null) playerController = player.GetComponent<PlayerController>();

            powder = CreateParticles("SnowPowder", WeatherDirector.CreateDotTexture(16), 250, 1); // above cars
            SetDrag(powder, 3f);
            SetSizeCurve(powder, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.4f)));
            SetFade(powder, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
        }

        private void OnDisable()
        {
            foreach (KeyValuePair<Collider2D, Skid> pair in skids) Straighten(pair.Key);
            skids.Clear();
            if (playerController != null) playerController.Grip = 1f;
        }

        protected override void OnEnter(Car car, Vector3 patchPos, float drift)
        {
            Skid skid = SkidFor(car.Collider);
            skid.OnSnowFrame = Time.frameCount;
            if (car.Traffic == null) skid.Velocity += (Random.value < 0.5f ? -1f : 1f) * playerJolt * Level;
            Puff(car.Collider.bounds, drift, Mathf.RoundToInt(burstPuffs * Level), true);
        }

        protected override void OnStay(Car car, Contact contact, Vector3 patchPos, float drift, float dt)
        {
            SkidFor(car.Collider).OnSnowFrame = Time.frameCount;
            Puff(car.Collider.bounds, drift, Take(contact, trailPuffsPerSecond * Level, dt), false);
        }

        private Skid SkidFor(Collider2D c)
        {
            if (!skids.TryGetValue(c, out Skid skid))
            {
                skid = new Skid { Seed = Random.Range(0f, 100f) };
                skids.Add(c, skid);
            }
            return skid;
        }

        protected override void AfterContacts(float dt)
        {
            bool playerOnSnow = false;
            finished.Clear();
            foreach (KeyValuePair<Collider2D, Skid> pair in skids)
            {
                Collider2D c = pair.Key;
                Skid skid = pair.Value;
                int index = c != null ? IndexOf(c) : -1;
                if (index < 0) { finished.Add(c); continue; } // despawned or pooled

                bool onSnow = skid.OnSnowFrame == Time.frameCount;
                // Smooth noise in -1..1, different for every car.
                float wander = Mathf.PerlinNoise(skid.Seed, Time.time * 1.8f) * 2f - 1f;
                bool settled = Cars[index].Traffic == null
                    ? SlidePlayer(skid, onSnow, wander, dt)
                    : SlideTraffic(Cars[index], skid, onSnow, wander, dt);
                if (c == player) playerOnSnow = onSnow;
                if (settled) finished.Add(c);
            }
            foreach (Collider2D c in finished)
            {
                Straighten(c);
                skids.Remove(c);
            }

            if (playerController != null)
            {
                float grip = playerOnSnow ? Mathf.Lerp(1f, playerGrip, Level) : 1f;
                playerController.Grip = Mathf.MoveTowards(playerController.Grip, grip, dt / Mathf.Max(0.01f, gripRecoverSeconds));
            }
        }

        // Slides the player sideways on its own. The controller keeps steering for its lane (slowly, at low grip),
        // so the car drifts off line and is pulled back once the snow is behind it. True when fully recovered.
        private bool SlidePlayer(Skid skid, bool onSnow, float wander, float dt)
        {
            float target = onSnow ? wander * playerSlideSpeed * Level : 0f;
            skid.Velocity = Mathf.Lerp(skid.Velocity, target, 1f - Mathf.Exp(-4f * dt));

            Transform t = player.transform;
            Vector3 pos = t.position;
            // The slide stops at its limits but never yanks the car: a swipe to a new lane moves LaneTarget far away
            // and the controller's own steering closes that gap.
            float laneX = playerController != null ? playerController.LaneTarget.x : pos.x;
            float slid = pos.x + skid.Velocity * dt;
            bool towardLane = Mathf.Abs(slid - laneX) < Mathf.Abs(pos.x - laneX);
            if ((towardLane || Mathf.Abs(slid - laneX) <= playerMaxSlide) && Mathf.Abs(slid) <= playerMaxX)
            {
                pos.x = slid;
                t.position = pos;
            }
            else
            {
                skid.Velocity = 0f; // hit the limit: the slide dies there
            }

            // Nose turns against the slide: the tail is the part stepping out.
            float angleTarget = Mathf.Clamp(-skid.Velocity / Mathf.Max(0.01f, playerSlideSpeed), -1f, 1f) * playerMaxAngle;
            skid.Angle = Mathf.Lerp(skid.Angle, angleTarget, 1f - Mathf.Exp(-8f * dt));
            t.rotation = Quaternion.Euler(0f, 0f, skid.Angle);

            return !onSnow && Mathf.Abs(skid.Velocity) < 0.01f && Mathf.Abs(skid.Angle) < 0.2f;
        }

        // Nudges a traffic car off its lane line and back. A car in the middle of a lane change belongs to the
        // spawner, which sets its X every frame, so it only straightens out. True when back on its line.
        private bool SlideTraffic(Car car, Skid skid, bool onSnow, float wander, float dt)
        {
            Rigidbody2D body = car.Traffic.Body;
            if (body == null) return true;

            float previous = skid.Offset;
            if (car.Traffic.Change == Spawner.LaneChange.Moving)
            {
                skid.Offset = 0f;
                previous = 0f;
            }
            else
            {
                float target = onSnow ? wander * trafficMaxSlide * Level : 0f;
                skid.Offset = Mathf.MoveTowards(skid.Offset, target, trafficSlideSpeed * dt);
                body.position += new Vector2(skid.Offset - previous, 0f);
            }

            float slide = (skid.Offset - previous) / dt;
            float angleTarget = Mathf.Clamp(-slide / Mathf.Max(0.01f, trafficSlideSpeed), -1f, 1f) * trafficMaxAngle;
            skid.Angle = Mathf.Lerp(skid.Angle, angleTarget, 1f - Mathf.Exp(-6f * dt));
            body.rotation = skid.Angle;

            return !onSnow && Mathf.Abs(skid.Offset) < 0.001f && Mathf.Abs(skid.Angle) < 0.2f;
        }

        private void Straighten(Collider2D c)
        {
            if (c == null) return;
            // Transform too: a pooled car is already inactive here, and an inactive body ignores rotation.
            c.transform.rotation = Quaternion.identity;
            Rigidbody2D body = c.attachedRigidbody;
            if (body != null && body.gameObject.activeInHierarchy) body.rotation = 0f;
        }

        // Powder kicked up behind both rear wheels, drifting back with the road.
        private void Puff(Bounds car, float drift, int perWheel, bool hit)
        {
            if (perWheel <= 0) return;
            float y = car.min.y + car.extents.y * 0.2f;
            float inset = car.extents.x * 0.25f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side < 0 ? car.min.x + inset : car.max.x - inset;
                var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
                for (int i = 0; i < perWheel; i++)
                {
                    emit.position = new Vector3(x + Random.Range(-0.08f, 0.08f), y + Random.Range(-0.1f, 0.1f), 0f);
                    float spread = Random.Range(0.3f, hit ? 1.6f : 0.9f);
                    emit.velocity = new Vector3(side * spread, drift * Random.Range(0.6f, 1.1f) - Random.Range(0f, 0.6f), 0f);
                    emit.startSize = Random.Range(puffSize.x, puffSize.y);
                    emit.startLifetime = Random.Range(0.45f, 0.8f);
                    emit.startColor = powderColor;
                    powder.Emit(emit, 1);
                }
            }
        }

        // Packed snow: bright white, faintly blue in the shaded lower edge, with a grainy surface.
        protected override Texture2D CreatePatchTexture(int size, int seed)
        {
            Color white = new Color(0.97f, 0.98f, 1f, 0.95f);
            Color shade = new Color(0.72f, 0.8f, 0.93f, 0.9f);
            float grainSeed = seed * 0.37f;
            return CreateBlobTexture(size, seed, 0.12f, 2f, (u, v, d) =>
            {
                float shadow = Mathf.Clamp01((d - 0.6f) / 0.4f) * Mathf.Clamp01(0.5f - v * 0.7f);
                Color c = Color.Lerp(white, shade, shadow);
                float grain = Mathf.PerlinNoise(u * 9f + grainSeed, v * 9f + grainSeed) - 0.5f;
                c.r += grain * 0.12f;
                c.g += grain * 0.12f;
                c.b += grain * 0.08f;
                return c;
            });
        }
    }
}
