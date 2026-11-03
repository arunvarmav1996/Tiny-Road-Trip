using System.Collections.Generic;
using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Rain puddles: patches of water that land on the road while it rains and ride it down the screen.
    // Any car (traffic or player) that drives through one throws spray out of both sides and leaves ripples
    // on the water. Raindrops ring the puddles too. Puddles fill in as the rain starts and dry up after it stops.
    // Every texture is generated at startup, like the rest of the weather.
    // Depth: road and cars share sorting order 0 and sort by Z (road 0, player -0.1, traffic -0.5), so puddles and
    // their ripples sit at a Z just in front of the road and under every car.
    public class RainPuddles : MonoBehaviour
    {
        [SerializeField] private WeatherDirector weather;
        [Tooltip("Traffic cars that can splash.")]
        [SerializeField] private Spawner spawner;
        [Tooltip("The player's car collider.")]
        [SerializeField] private Collider2D player;
        [Tooltip("Puddles ride the road down the screen at its speed.")]
        [SerializeField] private BackgroundScroller road;
        [SerializeField] private Camera targetCamera;

        [Header("Puddles")]
        [Tooltip("New puddles per second at full rain.")]
        [SerializeField] private float puddlesPerSecond = 0.7f;
        [SerializeField] private int maxPuddles = 10;
        [Tooltip("Puddle centres land within this distance of the road's middle, world units.")]
        [SerializeField] private float roadHalfWidth = 2.2f;
        [SerializeField] private Vector2 puddleWidth = new Vector2(0.9f, 1.7f);
        [Tooltip("Height as a fraction of width.")]
        [SerializeField] private Vector2 puddleAspect = new Vector2(0.45f, 0.75f);
        [SerializeField] private Color puddleColor = new Color(1f, 1f, 1f, 0.85f);
        [Tooltip("Seconds for puddles to fill in when rain starts, and to dry up after it stops.")]
        [SerializeField] private float fillSeconds = 3f;
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

        private const float PuddleZ = -0.04f;
        private const float RippleZ = -0.05f;

        private sealed class Contact
        {
            public Collider2D Car;
            public float Carry; // fractional trail droplets left over from earlier frames
        }

        private sealed class Puddle
        {
            public SpriteRenderer Renderer;
            public Vector2 HalfSize; // of the water itself, a little inside the sprite's lumpy edge
            public readonly List<Contact> Contacts = new List<Contact>();
        }

        private readonly List<Puddle> puddles = new List<Puddle>();
        private readonly List<Collider2D> cars = new List<Collider2D>();
        private readonly List<Contact> spareContacts = new List<Contact>();
        private Sprite[] puddleSprites;
        private ParticleSystem spray;
        private ParticleSystem ripples;
        private float wetness;    // 0 dry .. 1 full puddles
        private float spawnCarry;

        private void Awake()
        {
            if (weather == null) weather = GetComponent<WeatherDirector>();
            if (spawner == null) spawner = FindFirstObjectByType<Spawner>();
            if (road == null) road = FindFirstObjectByType<BackgroundScroller>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (player == null)
            {
                var controller = FindFirstObjectByType<PlayerController>();
                if (controller != null) player = controller.GetComponent<Collider2D>();
            }

            puddleSprites = new Sprite[3];
            for (int i = 0; i < puddleSprites.Length; i++)
            {
                Texture2D tex = CreatePuddleTexture(64, 1234 + i * 77);
                puddleSprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
            for (int i = 0; i < maxPuddles; i++) puddles.Add(CreatePuddle(i));

            spray = CreateParticles("RainSpray", WeatherDirector.CreateDotTexture(16), 300, 1); // above cars: water thrown up
            var limit = spray.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.drag = 4f; // spray flies out fast and slows hard
            SetSizeCurve(spray, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.35f, 1.15f), new Keyframe(1f, 0.4f)));
            SetFade(spray, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });

            ripples = CreateParticles("RainPuddleRipples", WeatherDirector.CreateRingTexture(32), 120, 0);
            ripples.transform.position = new Vector3(0f, 0f, RippleZ);
            SetSizeCurve(ripples, AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f));
            SetFade(ripples, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || targetCamera == null) return;

            float rain = weather != null && weather.Kind == WeatherKind.Rain ? weather.Visual : 0f;
            wetness = Mathf.MoveTowards(wetness, rain, dt / Mathf.Max(0.01f, fillSeconds));

            float halfHeight = targetCamera.orthographicSize;
            float camY = targetCamera.transform.position.y;
            float drift = road != null && road.rb != null ? road.rb.linearVelocity.y : 0f;

            spawnCarry += dt * puddlesPerSecond * rain;
            while (spawnCarry >= 1f)
            {
                spawnCarry -= 1f;
                Spawn(camY + halfHeight);
            }

            GatherCars();
            Color tint = puddleColor;
            tint.a *= wetness;

            foreach (Puddle p in puddles)
            {
                if (!p.Renderer.gameObject.activeSelf) continue;
                Transform t = p.Renderer.transform;
                Vector3 pos = t.position;
                pos.y += drift * dt;
                t.position = pos;

                if (pos.y + p.HalfSize.y * 1.5f < camY - halfHeight || wetness <= 0f)
                {
                    Retire(p);
                    continue;
                }

                p.Renderer.color = tint;
                if (Random.value < raindropRipplesPerPuddle * rain * dt)
                {
                    Vector2 inside = Random.insideUnitCircle;
                    EmitRipple(new Vector2(pos.x + inside.x * p.HalfSize.x, pos.y + inside.y * p.HalfSize.y), drift, 0.12f, 0.24f, 0.4f);
                }
                Splash(p, pos, drift, dt);
            }
        }

        // Traffic and the player, this frame.
        private void GatherCars()
        {
            cars.Clear();
            if (player != null && player.isActiveAndEnabled) cars.Add(player);
            if (spawner == null) return;
            IReadOnlyList<Spawner.ActiveObstacle> traffic = spawner.ActiveObstacles;
            for (int i = 0; i < traffic.Count; i++)
            {
                Collider2D c = traffic[i].Collider;
                if (c != null && c.gameObject.activeInHierarchy) cars.Add(c);
            }
        }

        private void Splash(Puddle p, Vector3 pos, float drift, float dt)
        {
            for (int i = p.Contacts.Count - 1; i >= 0; i--)
            {
                Contact contact = p.Contacts[i];
                if (contact.Car == null || !cars.Contains(contact.Car) || !Overlaps(p, pos, contact.Car.bounds))
                {
                    spareContacts.Add(contact);
                    p.Contacts.RemoveAt(i);
                }
            }

            foreach (Collider2D car in cars)
            {
                Bounds b = car.bounds;
                if (!Overlaps(p, pos, b)) continue;

                Contact contact = Find(p, car);
                if (contact == null)
                {
                    contact = spareContacts.Count > 0 ? spareContacts[spareContacts.Count - 1] : new Contact();
                    if (spareContacts.Count > 0) spareContacts.RemoveAt(spareContacts.Count - 1);
                    contact.Car = car;
                    contact.Carry = 0f;
                    p.Contacts.Add(contact);
                    Spray(b, pos.y, drift, Mathf.RoundToInt(burstDroplets * wetness), true);
                    continue;
                }

                contact.Carry += trailDropletsPerSecond * wetness * dt;
                int count = (int)contact.Carry;
                contact.Carry -= count;
                if (count > 0) Spray(b, pos.y, drift, count, false);
            }
        }

        private static Contact Find(Puddle p, Collider2D car)
        {
            foreach (Contact c in p.Contacts)
                if (c.Car == car) return c;
            return null;
        }

        private static bool Overlaps(Puddle p, Vector3 pos, Bounds car) =>
            Mathf.Abs(car.center.x - pos.x) < car.extents.x + p.HalfSize.x &&
            Mathf.Abs(car.center.y - pos.y) < car.extents.y + p.HalfSize.y;

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
                position = new Vector3(at.x, at.y, RippleZ),
                velocity = new Vector3(0f, drift, 0f), // stays on the road
                startSize = Random.Range(sizeMin, sizeMax),
                startLifetime = life * Random.Range(0.8f, 1.2f),
                startColor = rippleColor,
            };
            ripples.Emit(emit, 1);
        }

        // A free puddle drops in just above the top edge, away from the others.
        private void Spawn(float topY)
        {
            Puddle free = null;
            foreach (Puddle p in puddles)
                if (!p.Renderer.gameObject.activeSelf) { free = p; break; }
            if (free == null) return;

            float width = Random.Range(puddleWidth.x, puddleWidth.y);
            float height = width * Random.Range(puddleAspect.x, puddleAspect.y);
            float y = topY + height + Random.Range(0f, 1.5f);
            float x = 0f;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                x = Random.Range(-roadHalfWidth, roadHalfWidth);
                if (Clear(x, y, width, height)) break;
                if (attempt == 4) return; // crowded; skip this one
            }

            free.Renderer.sprite = puddleSprites[Random.Range(0, puddleSprites.Length)];
            free.Renderer.flipX = Random.value < 0.5f;
            free.Renderer.flipY = Random.value < 0.5f;
            free.Renderer.transform.position = new Vector3(x, y, PuddleZ);
            free.Renderer.transform.localScale = new Vector3(width, height, 1f);
            free.HalfSize = new Vector2(width, height) * 0.4f;
            free.Renderer.color = new Color(puddleColor.r, puddleColor.g, puddleColor.b, 0f);
            free.Renderer.gameObject.SetActive(true);
        }

        private bool Clear(float x, float y, float width, float height)
        {
            foreach (Puddle p in puddles)
            {
                if (!p.Renderer.gameObject.activeSelf) continue;
                Vector3 other = p.Renderer.transform.position;
                if (Mathf.Abs(other.x - x) < width * 0.5f + p.HalfSize.x + 0.2f &&
                    Mathf.Abs(other.y - y) < height * 0.5f + p.HalfSize.y + 0.4f) return false;
            }
            return true;
        }

        private void Retire(Puddle p)
        {
            spareContacts.AddRange(p.Contacts);
            p.Contacts.Clear();
            p.Renderer.gameObject.SetActive(false);
        }

        private Puddle CreatePuddle(int index)
        {
            var go = new GameObject("RainPuddle" + index);
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 0; // same order as road and cars; Z puts it between them
            return new Puddle { Renderer = sr };
        }

        private ParticleSystem CreateParticles(string name, Texture2D texture, int maxParticles, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            var emission = ps.emission;
            emission.enabled = false; // everything is emitted by hand
            var shape = ps.shape;
            shape.enabled = false;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var material = new Material(Shader.Find("Sprites/Default"));
            material.mainTexture = texture;
            renderer.material = material;
            renderer.sortingOrder = sortingOrder;

            ps.Play();
            return ps;
        }

        private static void SetSizeCurve(ParticleSystem ps, AnimationCurve curve)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void SetFade(ParticleSystem ps, GradientAlphaKey[] alpha)
        {
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
            colorOverLifetime.color = gradient;
        }

        // Lumpy blob of dark water: a brighter rim where it catches the light and a soft sky reflection across it.
        private static Texture2D CreatePuddleTexture(int size, int seed)
        {
            var rng = new System.Random(seed);
            float p1 = (float)rng.NextDouble() * 6.283f;
            float p2 = (float)rng.NextDouble() * 6.283f;
            float p3 = (float)rng.NextDouble() * 6.283f;

            Color deep = new Color(0.2f, 0.26f, 0.36f, 0.6f);
            Color rim = new Color(0.6f, 0.7f, 0.85f, 0.75f);
            Color sky = new Color(0.75f, 0.85f, 1f, 0.7f);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f - half) / half;
                    float v = (y + 0.5f - half) / half;
                    float angle = Mathf.Atan2(v, u);
                    float edge = 0.8f + 0.09f * Mathf.Sin(3f * angle + p1) + 0.06f * Mathf.Sin(5f * angle + p2) + 0.03f * Mathf.Sin(8f * angle + p3);
                    float d = Mathf.Sqrt(u * u + v * v) / edge; // 1 at the edge

                    Color c = Color.Lerp(deep, rim, Mathf.SmoothStep(0f, 1f, (d - 0.75f) / 0.25f));
                    float band = (u * 0.8f + v * 0.6f - 0.2f) / 0.22f;
                    c = Color.Lerp(c, sky, Mathf.Exp(-band * band) * 0.35f);
                    c.a *= Mathf.Clamp01((1f - d) / 0.08f);
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
