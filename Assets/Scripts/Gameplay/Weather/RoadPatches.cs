using System.Collections.Generic;
using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    [System.Serializable]
    public class PatchSettings
    {
        [Tooltip("New patches per second at full weather.")]
        public float PerSecond = 0.7f;
        public int Max = 10;
        [Tooltip("Patch centres land within this distance of the road's middle, world units.")]
        public float RoadHalfWidth = 2.2f;
        public Vector2 Width = new Vector2(0.9f, 1.7f);
        [Tooltip("Height as a fraction of width.")]
        public Vector2 Aspect = new Vector2(0.45f, 0.75f);
        public Color Color = new Color(1f, 1f, 1f, 0.85f);
        [Tooltip("Seconds for patches to fill in when the weather starts, and to clear after it stops.")]
        public float FillSeconds = 3f;
        [Tooltip("Part of the patch size that counts as touching; the edge is soft and lumpy.")]
        public float HitScale = 0.8f;
    }

    // Patches on the road for one weather (rain puddles, snow): they land above the top edge while that weather
    // is on, ride the road down the screen, and fade in and out with it. Tracks which cars (traffic and the
    // player) are driving through each patch and hands enter / stay events to the subclass.
    // Depth: road and cars share sorting order 0 and sort by Z (road 0, player -0.1, traffic -0.5), so patches
    // and their effects on the road sit at a Z just in front of the road and under every car.
    public abstract class RoadPatches : MonoBehaviour
    {
        [SerializeField] protected WeatherDirector weather;
        [Tooltip("Traffic cars that drive through patches.")]
        [SerializeField] private Spawner spawner;
        [Tooltip("The player's car collider.")]
        [SerializeField] protected Collider2D player;
        [Tooltip("Patches ride the road down the screen at its speed.")]
        [SerializeField] private BackgroundScroller road;
        [SerializeField] private Camera targetCamera;

        protected const float PatchZ = -0.04f;
        protected const float OnRoadZ = -0.05f;

        protected struct Car
        {
            public Collider2D Collider;
            public Spawner.ActiveObstacle Traffic; // null for the player
        }

        protected sealed class Contact
        {
            public Collider2D Collider;
            public float Carry; // fractional particles left over from earlier frames
        }

        private sealed class Patch
        {
            public SpriteRenderer Renderer;
            public Vector2 HalfSize; // of the touchable part
            public readonly List<Contact> Contacts = new List<Contact>();
        }

        private readonly List<Patch> patches = new List<Patch>();
        private readonly List<Contact> spareContacts = new List<Contact>();
        protected readonly List<Car> Cars = new List<Car>();
        private Sprite[] sprites;
        private float spawnCarry;

        protected abstract WeatherKind Kind { get; }
        protected abstract PatchSettings Settings { get; }
        // How filled in the patches are: 0 none .. 1 full. Follows the weather's Visual level, a little slower.
        protected float Level { get; private set; }

        protected abstract Texture2D CreatePatchTexture(int size, int seed);
        protected abstract void OnEnter(Car car, Vector3 patchPos, float drift);
        protected abstract void OnStay(Car car, Contact contact, Vector3 patchPos, float drift, float dt);
        // Every frame for each patch on screen, e.g. raindrops ringing a puddle.
        protected virtual void OnPatch(Vector3 pos, Vector2 halfSize, float weatherLevel, float drift, float dt) { }
        // Every frame after all contacts, even with no patches left.
        protected virtual void AfterContacts(float dt) { }

        protected virtual void Awake()
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

            sprites = new Sprite[3];
            for (int i = 0; i < sprites.Length; i++)
            {
                Texture2D tex = CreatePatchTexture(64, 1234 + i * 77);
                sprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
            for (int i = 0; i < Settings.Max; i++) patches.Add(CreatePatch(i));
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || targetCamera == null) return;
            PatchSettings settings = Settings;

            float weatherLevel = weather != null && weather.Kind == Kind ? weather.Visual : 0f;
            Level = Mathf.MoveTowards(Level, weatherLevel, dt / Mathf.Max(0.01f, settings.FillSeconds));

            float halfHeight = targetCamera.orthographicSize;
            float camY = targetCamera.transform.position.y;
            float drift = road != null && road.rb != null ? road.rb.linearVelocity.y : 0f;

            spawnCarry += dt * settings.PerSecond * weatherLevel;
            while (spawnCarry >= 1f)
            {
                spawnCarry -= 1f;
                Spawn(camY + halfHeight);
            }

            GatherCars();
            Color tint = settings.Color;
            tint.a *= Level;

            foreach (Patch p in patches)
            {
                if (!p.Renderer.gameObject.activeSelf) continue;
                Transform t = p.Renderer.transform;
                Vector3 pos = t.position;
                pos.y += drift * dt;
                t.position = pos;

                if (pos.y + p.HalfSize.y * 2f < camY - halfHeight || Level <= 0f)
                {
                    Retire(p);
                    continue;
                }

                p.Renderer.color = tint;
                OnPatch(pos, p.HalfSize, weatherLevel, drift, dt);
                UpdateContacts(p, pos, drift, dt);
            }

            AfterContacts(dt);
        }

        // Traffic and the player, this frame.
        private void GatherCars()
        {
            Cars.Clear();
            if (player != null && player.isActiveAndEnabled) Cars.Add(new Car { Collider = player });
            if (spawner == null) return;
            IReadOnlyList<Spawner.ActiveObstacle> traffic = spawner.ActiveObstacles;
            for (int i = 0; i < traffic.Count; i++)
            {
                Collider2D c = traffic[i].Collider;
                if (c != null && c.gameObject.activeInHierarchy) Cars.Add(new Car { Collider = c, Traffic = traffic[i] });
            }
        }

        private void UpdateContacts(Patch p, Vector3 pos, float drift, float dt)
        {
            for (int i = p.Contacts.Count - 1; i >= 0; i--)
            {
                Collider2D c = p.Contacts[i].Collider;
                if (c == null || IndexOf(c) < 0 || !Overlaps(p, pos, c.bounds))
                {
                    spareContacts.Add(p.Contacts[i]);
                    p.Contacts.RemoveAt(i);
                }
            }

            foreach (Car car in Cars)
            {
                if (!Overlaps(p, pos, car.Collider.bounds)) continue;

                Contact contact = Find(p, car.Collider);
                if (contact != null)
                {
                    OnStay(car, contact, pos, drift, dt);
                    continue;
                }
                contact = spareContacts.Count > 0 ? spareContacts[spareContacts.Count - 1] : new Contact();
                if (spareContacts.Count > 0) spareContacts.RemoveAt(spareContacts.Count - 1);
                contact.Collider = car.Collider;
                contact.Carry = 0f;
                p.Contacts.Add(contact);
                OnEnter(car, pos, drift);
            }
        }

        protected int IndexOf(Collider2D c)
        {
            for (int i = 0; i < Cars.Count; i++)
                if (Cars[i].Collider == c) return i;
            return -1;
        }

        private static Contact Find(Patch p, Collider2D c)
        {
            foreach (Contact contact in p.Contacts)
                if (contact.Collider == c) return contact;
            return null;
        }

        private static bool Overlaps(Patch p, Vector3 pos, Bounds car) =>
            Mathf.Abs(car.center.x - pos.x) < car.extents.x + p.HalfSize.x &&
            Mathf.Abs(car.center.y - pos.y) < car.extents.y + p.HalfSize.y;

        // Whole particles due this frame at rate per second, carrying the fraction over.
        protected static int Take(Contact contact, float rate, float dt)
        {
            contact.Carry += rate * dt;
            int count = (int)contact.Carry;
            contact.Carry -= count;
            return count;
        }

        // A free patch drops in just above the top edge, away from the others.
        private void Spawn(float topY)
        {
            Patch free = null;
            foreach (Patch p in patches)
                if (!p.Renderer.gameObject.activeSelf) { free = p; break; }
            if (free == null) return;

            PatchSettings settings = Settings;
            float width = Random.Range(settings.Width.x, settings.Width.y);
            float height = width * Random.Range(settings.Aspect.x, settings.Aspect.y);
            float y = topY + height + Random.Range(0f, 1.5f);
            float x = 0f;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                x = Random.Range(-settings.RoadHalfWidth, settings.RoadHalfWidth);
                if (Clear(x, y, width, height)) break;
                if (attempt == 4) return; // crowded; skip this one
            }

            free.Renderer.sprite = sprites[Random.Range(0, sprites.Length)];
            free.Renderer.flipX = Random.value < 0.5f;
            free.Renderer.flipY = Random.value < 0.5f;
            free.Renderer.transform.position = new Vector3(x, y, PatchZ);
            free.Renderer.transform.localScale = new Vector3(width, height, 1f);
            free.HalfSize = new Vector2(width, height) * (0.5f * settings.HitScale);
            free.Renderer.color = new Color(settings.Color.r, settings.Color.g, settings.Color.b, 0f);
            free.Renderer.gameObject.SetActive(true);
        }

        private bool Clear(float x, float y, float width, float height)
        {
            foreach (Patch p in patches)
            {
                if (!p.Renderer.gameObject.activeSelf) continue;
                Vector3 other = p.Renderer.transform.position;
                if (Mathf.Abs(other.x - x) < width * 0.5f + p.HalfSize.x + 0.2f &&
                    Mathf.Abs(other.y - y) < height * 0.5f + p.HalfSize.y + 0.4f) return false;
            }
            return true;
        }

        private void Retire(Patch p)
        {
            spareContacts.AddRange(p.Contacts);
            p.Contacts.Clear();
            p.Renderer.gameObject.SetActive(false);
        }

        private Patch CreatePatch(int index)
        {
            var go = new GameObject(GetType().Name + index);
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 0; // same order as road and cars; Z puts it between them
            return new Patch { Renderer = sr };
        }

        // Particles emitted by hand (EmitParams), in world space.
        protected ParticleSystem CreateParticles(string name, Texture2D texture, int maxParticles, int sortingOrder)
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
            emission.enabled = false;
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

        protected static void SetDrag(ParticleSystem ps, float drag)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.drag = drag;
        }

        protected static void SetSizeCurve(ParticleSystem ps, AnimationCurve curve)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        protected static void SetFade(ParticleSystem ps, GradientAlphaKey[] alpha)
        {
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
            colorOverLifetime.color = gradient;
        }

        // Lumpy blob texture: colour(u, v, d) where d is 0 at the centre and 1 at the lumpy edge; alpha fades over
        // the last softEdge of d. lobes: extra edge wobble for rougher shapes.
        protected static Texture2D CreateBlobTexture(int size, int seed, float softEdge, float lobes, System.Func<float, float, float, Color> colour)
        {
            var rng = new System.Random(seed);
            float p1 = (float)rng.NextDouble() * 6.283f;
            float p2 = (float)rng.NextDouble() * 6.283f;
            float p3 = (float)rng.NextDouble() * 6.283f;

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
                    float edge = 0.8f + 0.09f * Mathf.Sin(3f * angle + p1) + 0.06f * Mathf.Sin(5f * angle + p2)
                                 + 0.03f * lobes * Mathf.Sin(8f * angle + p3);
                    float d = Mathf.Sqrt(u * u + v * v) / edge;
                    Color c = colour(u, v, d);
                    c.a *= Mathf.Clamp01((1f - d) / softEdge);
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
