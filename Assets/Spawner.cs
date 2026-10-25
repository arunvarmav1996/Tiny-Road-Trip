using System.Collections;
using System.Collections.Generic;
using TinyRoadTrip.Gameplay;
using UnityEngine;
using Random = UnityEngine.Random;
using System;

public class Spawner : MonoBehaviour
{

    [Header("SpawnerSettings")]
    public List<Sprite> obstacleSprites;
    public GameObject obstaclePrefab;
    public Transform[] lanePositions;
    public float spawnInterval = 1.5f;
        [Range(0, 1)] public float obstacleSpawnTimeFactor = 0.1f;
    public float obstacleSpeed = 5f;
        [Range(0, 1)] public float obstacleSpeedFactor = 0.2f;

    [Header("Gameplay Speed (Slow-Time / Nitro)")]
    [Tooltip("Optional. When set, obstacle speed and spawn timing follow the gameplay speed multiplier.")]
    [SerializeField] private GameplaySpeedController speedController;

    [Header("Cleanup")]
    [Tooltip("Obstacles below this world Y are removed (below the bottom of every supported screen).")]
    [SerializeField] private float despawnBelowY = -12f;
    [Tooltip("Safety net: obstacles are destroyed after this many seconds even if they never leave the screen.")]
    [SerializeField] private float maxObstacleLifetime = 60f;

    // Live obstacle record shared with near-miss detection, collectible placement and the shield.
    public sealed class ActiveObstacle
    {
        public GameObject GameObject;
        public Rigidbody2D Body;
        public Collider2D Collider;
        public float BaseSpeed;
        public float PreviousY;
        public bool NearMissResolved;
        public bool WasHit;
    }

    private readonly List<ActiveObstacle> activeObstacles = new List<ActiveObstacle>();
    public IReadOnlyList<ActiveObstacle> ActiveObstacles => activeObstacles;

    private float timer;
    private float _obstacleSpawnTime;
    private float _obstacleSpeed;

    private float timeAlive;

    private float SpeedMultiplier => speedController != null ? speedController.Multiplier : 1f;

    private void Start()
    {

        ResetFactors();

        if (speedController != null)
            speedController.MultiplierChanged += ApplySpeedMultiplier;

    }

    private void OnDestroy()
    {
        if (speedController != null)
            speedController.MultiplierChanged -= ApplySpeedMultiplier;
    }

    // Update is called once per frame
    void Update()
    {
        // Spawn timing follows world speed so the spacing between cars stays the same during Slow-Time/Nitro.
        // timeAlive (the difficulty clock) is unchanged.
        timer += Time.deltaTime * SpeedMultiplier;
        timeAlive += Time.deltaTime;

        if (timer>= _obstacleSpawnTime)
        {
            SpawnObstacle();
            timer = 0f;

        }

        CalculateFactors();

        CullObstacles();

    }

    void SpawnObstacle()
    {
        if(lanePositions.Length == 0 || obstacleSprites.Count == 0)
        return;

        int randomLane = Random.Range(0, lanePositions.Length);
        int randomSpriteIndex = Random.Range(0, obstacleSprites.Count);

        GameObject obs = Instantiate(obstaclePrefab, lanePositions[randomLane].position, Quaternion.identity);

        SpriteRenderer sr = obs.GetComponent<SpriteRenderer>();
        if(sr!=null)
        sr.sprite = obstacleSprites[randomSpriteIndex];

        Rigidbody2D rb = obs.GetComponent<Rigidbody2D>();
        if(rb==null)
        rb=obs.AddComponent<Rigidbody2D>();

        rb.gravityScale = 0;
        rb.linearVelocity = new Vector2(0,-_obstacleSpeed * SpeedMultiplier);

        activeObstacles.Add(new ActiveObstacle
        {
            GameObject = obs,
            Body = rb,
            Collider = obs.GetComponent<Collider2D>(),
            BaseSpeed = _obstacleSpeed,
            PreviousY = obs.transform.position.y,
        });

        // Previously Destroy(obs, 6f): a fixed lifetime would remove slowed cars mid-screen,
        // so cleanup is now distance-based (CullObstacles) with this as a safety net.
        Destroy(obs,maxObstacleLifetime);
    }

    // Removes an obstacle immediately (used when the shield absorbs a hit).
    public void DespawnObstacle(Collider2D obstacleCollider)
    {
        for (int i = activeObstacles.Count - 1; i >= 0; i--)
        {
            ActiveObstacle o = activeObstacles[i];
            if (o.Collider != obstacleCollider) continue;
            o.WasHit = true;
            activeObstacles.RemoveAt(i);
            if (o.GameObject != null) Destroy(o.GameObject);
            return;
        }
    }

    public void MarkHit(Collider2D obstacleCollider)
    {
        for (int i = 0; i < activeObstacles.Count; i++)
        {
            if (activeObstacles[i].Collider == obstacleCollider)
            {
                activeObstacles[i].WasHit = true;
                return;
            }
        }
    }

    private void CullObstacles()
    {
        for (int i = activeObstacles.Count - 1; i >= 0; i--)
        {
            ActiveObstacle o = activeObstacles[i];
            if (o.GameObject == null)
            {
                activeObstacles.RemoveAt(i);
            }
            else if (o.Body.position.y < despawnBelowY)
            {
                Destroy(o.GameObject);
                activeObstacles.RemoveAt(i);
            }
        }
    }

    private void ApplySpeedMultiplier(float multiplier)
    {
        for (int i = 0; i < activeObstacles.Count; i++)
        {
            ActiveObstacle o = activeObstacles[i];
            if (o.Body != null)
                o.Body.linearVelocity = new Vector2(0, -o.BaseSpeed * multiplier);
        }
    }

    private void ResetFactors()
    {
        timeAlive = 1f;

        _obstacleSpawnTime = spawnInterval;
        _obstacleSpeed = obstacleSpeed;
    }

    private void CalculateFactors()
    {
        _obstacleSpawnTime = spawnInterval / Mathf.Pow(timeAlive, obstacleSpawnTimeFactor);
        _obstacleSpeed = obstacleSpeed * Mathf.Pow(timeAlive, obstacleSpeedFactor);
    }
}
