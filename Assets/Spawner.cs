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

    [Header("Difficulty")]
    [Tooltip("Car speed and spawn spacing over the run: fast early ramp that levels off, plus busy/calm traffic waves.")]
    [SerializeField] private TinyRoadTrip.Gameplay.Core.DifficultyCurve difficulty = new TinyRoadTrip.Gameplay.Core.DifficultyCurve();
    [Tooltip("Spawn traffic patterns (pairs, walls with gaps, zigzags) as difficulty rises. Off = one random car per spawn.")]
    [SerializeField] private bool usePatterns = true;
    [Tooltip("Vertical spacing (world units) between the rows of a multi-row pattern.")]
    [SerializeField] private float patternRowGap = 2.6f;

    [Header("Screen Adaptation")]
    [Tooltip("Aspect-ratio lane formula from the Difficulty-Setup branch. Off by default: ResponsiveCamera already " +
             "adapts the view, and using both scales the game twice. Turn on together with the matching toggles on " +
             "PlayerController and BackgroundScroller only if ResponsiveCamera is removed.")]
    [SerializeField] private bool adaptLanesToAspect = false;

    [Header("Gameplay Speed (Slow-Time / Nitro)")]
    [Tooltip("Optional. When set, obstacle speed and spawn timing follow the gameplay speed multiplier.")]
    [SerializeField] private GameplaySpeedController speedController;

    [Header("Cleanup")]
    [Tooltip("Obstacles below this world Y are removed (below the bottom of every supported screen).")]
    [SerializeField] private float despawnBelowY = -12f;
    [Tooltip("Safety net: non-pooled obstacles are destroyed after this many seconds even if they never leave the screen.")]
    [SerializeField] private float maxObstacleLifetime = 60f;
    [Tooltip("Optional. Re-enables collisions between the player and a pooled car when it is reused " +
             "(the shield's invulnerability window disables them per car).")]
    [SerializeField] private Collider2D playerCollider;

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
        public bool Pooled;
    }

    private readonly List<ActiveObstacle> activeObstacles = new List<ActiveObstacle>();
    public IReadOnlyList<ActiveObstacle> ActiveObstacles => activeObstacles;

    private float timer;
    private float _obstacleSpawnTime;
    private float _obstacleSpeed;

    private float timeAlive;

    private float SpeedMultiplier => speedController != null ? speedController.Multiplier : 1f;

    private void Awake()
    {
        if (!adaptLanesToAspect) return;

        double loc = -0.790594 * ((float)Screen.height / Screen.width) + 2.52476f;
        lanePositions[0].position = new Vector3((float)loc * 2,lanePositions[0].position.y, lanePositions[0].position.z);
        lanePositions[1].position = new Vector3((float)loc, lanePositions[1].position.y, lanePositions[1].position.z);
        lanePositions[3].position = new Vector3(-(float)loc, lanePositions[3].position.y, lanePositions[3].position.z);
        lanePositions[4].position = new Vector3(-(float)loc * 2, lanePositions[4].position.y, lanePositions[4].position.z);

    }

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

        // The pool lives in DontDestroyOnLoad: hand back every car this scene still owns so none
        // keep driving into the next scene (e.g. Close -> MainMenu).
        foreach (ActiveObstacle o in activeObstacles)
            if (o.Pooled && o.GameObject != null) o.GameObject.SetActive(false);
        activeObstacles.Clear();
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
            int rows = SpawnPattern();
            // A multi-row pattern is taller than one car; hold the next spawn until its last row has entered.
            timer = _obstacleSpeed > 0f ? -(rows - 1) * patternRowGap / _obstacleSpeed : 0f;

        }

        CalculateFactors();

        CullObstacles();

    }

    private readonly System.Random patternRandom = new System.Random();
    private readonly List<TinyRoadTrip.Gameplay.Core.CarSlot> patternSlots = new List<TinyRoadTrip.Gameplay.Core.CarSlot>();

    // Returns the number of rows spawned.
    int SpawnPattern()
    {
        if (lanePositions.Length == 0 || obstacleSprites.Count == 0)
            return 1;

        var kind = TinyRoadTrip.Gameplay.Core.TrafficPatternKind.Single;
        if (usePatterns && !IsCalmPhase)
            kind = TinyRoadTrip.Gameplay.Core.TrafficPatterns.Pick(difficulty.Level(timeAlive), patternRandom.NextDouble());

        int rows = TinyRoadTrip.Gameplay.Core.TrafficPatterns.Build(kind, lanePositions.Length, patternRandom, patternSlots);
        foreach (var slot in patternSlots)
            SpawnObstacle(slot.Lane, slot.Row * patternRowGap);
        return Mathf.Max(1, rows);
    }

    void SpawnObstacle(int lane, float heightAboveSpawn)
    {
        int randomSpriteIndex = Random.Range(0, obstacleSprites.Count);
        Vector3 spawnPosition = lanePositions[lane].position + new Vector3(0f, heightAboveSpawn, 0f);
        Vector2 velocity = new Vector2(0,-_obstacleSpeed * SpeedMultiplier);

        GameObject obs;
        bool pooled = ObjectSpawner.spawnerInstance != null;
        if (pooled)
        {
            obs = ObjectSpawner.spawnerInstance.GetVehicle(spawnPosition);
            RemoveRecord(obs); // a reused car must not keep its previous record
        }
        else
        {
            obs = Instantiate(obstaclePrefab, spawnPosition, Quaternion.identity);
            // Previously Destroy(obs, 6f): a fixed lifetime would remove slowed cars mid-screen,
            // so cleanup is distance-based (CullObstacles) with this as a safety net.
            Destroy(obs,maxObstacleLifetime);
        }

        Rigidbody2D rb = obs.GetComponent<Rigidbody2D>();
        if(rb==null)
        rb=obs.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;

        VehicleScript vehicle = obs.GetComponent<VehicleScript>();
        if (vehicle != null)
        {
            vehicle.SetVehicleStats(velocity, obstacleSprites[randomSpriteIndex]);
        }
        else
        {
            SpriteRenderer sr = obs.GetComponent<SpriteRenderer>();
            if(sr!=null)
            sr.sprite = obstacleSprites[randomSpriteIndex];
            rb.linearVelocity = velocity;
        }

        Collider2D col = obs.GetComponent<Collider2D>();
        if (pooled && playerCollider != null && col != null)
            Physics2D.IgnoreCollision(playerCollider, col, false);

        activeObstacles.Add(new ActiveObstacle
        {
            GameObject = obs,
            Body = rb,
            Collider = col,
            BaseSpeed = _obstacleSpeed,
            PreviousY = obs.transform.position.y,
            Pooled = pooled,
        });
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
            Release(o);
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
            if (o.GameObject == null || !o.GameObject.activeInHierarchy)
            {
                // Destroyed, or returned to the pool elsewhere (RoadEndCollision, ObjectSpawner.DisableAllObjects).
                activeObstacles.RemoveAt(i);
            }
            else if (o.Body.position.y < despawnBelowY)
            {
                activeObstacles.RemoveAt(i);
                Release(o);
            }
        }
    }

    private void RemoveRecord(GameObject obs)
    {
        for (int i = activeObstacles.Count - 1; i >= 0; i--)
            if (activeObstacles[i].GameObject == obs) activeObstacles.RemoveAt(i);
    }

    private static void Release(ActiveObstacle o)
    {
        if (o.GameObject == null) return;
        if (o.Pooled)
        {
            o.Body.linearVelocity = Vector2.zero;
            o.GameObject.SetActive(false);
        }
        else
        {
            Destroy(o.GameObject);
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

    // True during a wave's calm phase (fewer cars).
    public bool IsCalmPhase => difficulty.IsCalm(timeAlive);

    private void ResetFactors()
    {
        timeAlive = 0f;
        CalculateFactors();
    }

    private void CalculateFactors()
    {
        _obstacleSpawnTime = difficulty.SpawnInterval(timeAlive);
        _obstacleSpeed = difficulty.Speed(timeAlive);
    }
}
