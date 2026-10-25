using System;
using System.Collections.Generic;
using TinyRoadTrip.Gameplay.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TinyRoadTrip.Gameplay
{
    // Spawns pooled coins / power-ups in the existing obstacle lanes, moves them with the road,
    // applies the coin magnet and hands pickups to CoinManager / PowerUpManager.
    public class CollectibleSpawner : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("Existing obstacle spawner: provides lane positions and live obstacles for fair placement.")]
        [SerializeField] private Spawner obstacleSpawner;
        [Tooltip("Road scroller: collectibles move at the road's speed.")]
        [SerializeField] private BackgroundScroller road;
        [SerializeField] private GameplaySpeedController speedController;
        [SerializeField] private Transform player;
        [SerializeField] private CoinManager coinManager;
        [SerializeField] private PowerUpManager powerUpManager;

        [Header("Content")]
        [SerializeField] private Collectible collectiblePrefab;
        [SerializeField] private CollectibleDefinition[] definitions = Array.Empty<CollectibleDefinition>();

        [Header("Spawn Timing (gameplay seconds, scaled by Slow-Time/Nitro)")]
        [SerializeField, Min(0f)] private float initialDelay = 3f;
        [SerializeField, Min(0.1f)] private float spawnInterval = 1.6f;

        [Header("Coins")]
        [SerializeField, Min(1)] private int coinRowMin = 3;
        [SerializeField, Min(1)] private int coinRowMax = 5;
        [SerializeField, Min(0.1f)] private float coinRowSpacing = 0.9f;

        [Header("Power-Up Limits")]
        [Tooltip("Max power-up pickups on screen at the same time.")]
        [SerializeField, Min(0)] private int maxPowerUpPickups = 1;
        [Tooltip("Minimum gameplay seconds between two power-up spawns.")]
        [SerializeField, Min(0f)] private float minPowerUpInterval = 8f;

        [Header("Fair Placement")]
        [Tooltip("Lane is unusable if an obstacle is within this distance of the spawn area (world units).")]
        [SerializeField, Min(0f)] private float obstacleClearance = 1.6f;
        [Tooltip("Max horizontal offset for an obstacle to count as being in the same lane.")]
        [SerializeField, Min(0f)] private float laneTolerance = 0.4f;

        [Header("Cleanup / Pooling")]
        [SerializeField] private float despawnBelowY = -12f;
        [SerializeField, Min(0)] private int prewarmCount = 16;

        [Header("Look")]
        [SerializeField] private float powerUpPulseAmount = 0.08f;
        [SerializeField] private float powerUpPulseSpeed = 5f;
        [Tooltip("Z position (in front of the road, behind cars and the player).")]
        [SerializeField] private float collectibleZ = -0.05f;

        private readonly List<Collectible> active = new List<Collectible>();
        private readonly Stack<Collectible> pool = new Stack<Collectible>();
        private float[] weightBuffer = Array.Empty<float>();
        private readonly List<int> laneBuffer = new List<int>();

        private float timer;
        private float elapsed;
        private float lastPowerUpSpawn = float.NegativeInfinity;

        // (definition, world position)
        public event Action<CollectibleDefinition, Vector3> Collected;

        public IReadOnlyList<Collectible> ActiveCollectibles => active;

        private float SpeedMultiplier => speedController != null ? speedController.Multiplier : 1f;
        private float RoadSpeed => road != null ? road.BaseScrollSpeed : 2f;

        private void Awake()
        {
            weightBuffer = new float[definitions.Length];
            for (int i = 0; i < prewarmCount; i++)
                pool.Push(CreateInstance());
        }

        private void OnEnable()
        {
            if (speedController != null) speedController.MultiplierChanged += ApplySpeedMultiplier;
        }

        private void OnDisable()
        {
            if (speedController != null) speedController.MultiplierChanged -= ApplySpeedMultiplier;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            timer += dt * SpeedMultiplier;

            if (elapsed >= initialDelay && timer >= spawnInterval)
            {
                timer = 0f;
                TrySpawn();
            }

            UpdateActive();
        }

        private void UpdateActive()
        {
            CoinMagnetEffect magnet = powerUpManager != null ? powerUpManager.Magnet : null;
            bool magnetOn = magnet != null && magnet.IsActive && player != null;
            float radiusSqr = magnetOn ? magnet.Radius * magnet.Radius : 0f;
            Vector2 playerPos = player != null ? (Vector2)player.position : Vector2.zero;
            float pulse = 1f + powerUpPulseAmount * Mathf.Sin(Time.time * powerUpPulseSpeed);

            for (int i = active.Count - 1; i >= 0; i--)
            {
                Collectible c = active[i];
                Vector2 pos = c.Rigidbody.position;

                if (c.Definition.kind == CollectibleKind.Coin)
                {
                    if (!c.Magnetized && magnetOn && (playerPos - pos).sqrMagnitude <= radiusSqr)
                        c.Magnetized = true;

                    // Magnetized coins keep homing even if the magnet expires, so none are left hanging mid-road.
                    if (c.Magnetized && player != null)
                    {
                        Vector2 toPlayer = playerPos - pos;
                        float distance = toPlayer.magnitude;
                        float collectDistance = magnet != null ? magnet.CollectDistance : 0.3f;
                        float pullSpeed = magnet != null ? magnet.PullSpeed : 10f;
                        if (distance <= collectDistance)
                        {
                            Collect(c);
                            continue;
                        }
                        c.Rigidbody.linearVelocity = toPlayer / distance * pullSpeed;
                    }
                }
                else
                {
                    c.transform.localScale = c.BaseScale * pulse;
                }

                if (pos.y < despawnBelowY)
                    Release(c);
            }
        }

        private void TrySpawn()
        {
            if (obstacleSpawner == null || obstacleSpawner.lanePositions == null || obstacleSpawner.lanePositions.Length == 0)
                return;

            CollectibleDefinition definition = PickDefinition();
            if (definition == null) return;

            int rowLength = definition.kind == CollectibleKind.Coin ? Random.Range(coinRowMin, coinRowMax + 1) : 1;
            float spawnHeight = (rowLength - 1) * coinRowSpacing;

            int lane = PickClearLane(spawnHeight);
            if (lane < 0) return; // every lane blocked: skip this roll rather than spawn unfairly

            Vector3 lanePos = obstacleSpawner.lanePositions[lane].position;
            for (int i = 0; i < rowLength; i++)
                SpawnOne(definition, new Vector3(lanePos.x, lanePos.y + i * coinRowSpacing, collectibleZ));

            if (definition.IsPowerUp) lastPowerUpSpawn = elapsed;
        }

        private CollectibleDefinition PickDefinition()
        {
            bool powerUpsAllowed = CountActivePowerUps() < maxPowerUpPickups
                                   && elapsed - lastPowerUpSpawn >= minPowerUpInterval;

            for (int i = 0; i < definitions.Length; i++)
            {
                CollectibleDefinition d = definitions[i];
                weightBuffer[i] = d == null || (d.IsPowerUp && !powerUpsAllowed) ? 0f : d.spawnWeight;
            }

            int index = GameplayMath.PickWeighted(weightBuffer, definitions.Length, Random.value);
            return index >= 0 ? definitions[index] : null;
        }

        private int PickClearLane(float spawnHeight)
        {
            Transform[] lanes = obstacleSpawner.lanePositions;
            IReadOnlyList<Spawner.ActiveObstacle> obstacles = obstacleSpawner.ActiveObstacles;
            laneBuffer.Clear();

            for (int lane = 0; lane < lanes.Length; lane++)
            {
                Vector3 p = lanes[lane].position;
                float bottom = p.y - obstacleClearance;
                float top = p.y + spawnHeight + obstacleClearance;
                if (!IsAreaClear(p.x, bottom, top, obstacles)) continue;
                laneBuffer.Add(lane);
            }

            return laneBuffer.Count == 0 ? -1 : laneBuffer[Random.Range(0, laneBuffer.Count)];
        }

        private bool IsAreaClear(float x, float bottom, float top, IReadOnlyList<Spawner.ActiveObstacle> obstacles)
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                Spawner.ActiveObstacle o = obstacles[i];
                if (o.Body == null) continue;
                Vector2 op = o.Body.position;
                if (Mathf.Abs(op.x - x) <= laneTolerance && op.y >= bottom && op.y <= top) return false;
            }
            for (int i = 0; i < active.Count; i++)
            {
                Vector2 cp = active[i].Rigidbody.position;
                if (Mathf.Abs(cp.x - x) <= laneTolerance && cp.y >= bottom && cp.y <= top) return false;
            }
            return true;
        }

        private int CountActivePowerUps()
        {
            int count = 0;
            for (int i = 0; i < active.Count; i++)
                if (active[i].Definition.IsPowerUp) count++;
            return count;
        }

        private void SpawnOne(CollectibleDefinition definition, Vector3 position)
        {
            Collectible c = pool.Count > 0 ? pool.Pop() : CreateInstance();
            c.Spawn(this, definition, position);
            c.Rigidbody.linearVelocity = new Vector2(0f, -RoadSpeed * SpeedMultiplier);
            active.Add(c);
        }

        public void Collect(Collectible c)
        {
            if (!c.TryMarkCollected()) return;

            CollectibleDefinition d = c.Definition;
            if (d.kind == CollectibleKind.Coin)
            {
                if (coinManager != null) coinManager.AddCoins(d.coinValue);
            }
            else if (powerUpManager != null)
            {
                powerUpManager.Activate(d);
            }

            if (!string.IsNullOrEmpty(d.pickupSound) && AudioManager.instance != null)
                AudioManager.instance.Play(d.pickupSound);

            Collected?.Invoke(d, c.transform.position);
            Release(c);
        }

        private void Release(Collectible c)
        {
            active.Remove(c);
            c.Rigidbody.linearVelocity = Vector2.zero;
            c.gameObject.SetActive(false);
            pool.Push(c);
        }

        private void ApplySpeedMultiplier(float multiplier)
        {
            Vector2 velocity = new Vector2(0f, -RoadSpeed * multiplier);
            for (int i = 0; i < active.Count; i++)
                if (!active[i].Magnetized) active[i].Rigidbody.linearVelocity = velocity;
        }

        private Collectible CreateInstance()
        {
            Collectible c = Instantiate(collectiblePrefab, transform);
            c.gameObject.SetActive(false);
            return c;
        }
    }
}
