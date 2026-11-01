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
    [Tooltip("Single cars get their own speed and some change lane (with a turn-signal blink) as difficulty rises.")]
    [SerializeField] private bool useVariety = true;
    [SerializeField] private TinyRoadTrip.Gameplay.Core.TrafficVariety variety = new TinyRoadTrip.Gameplay.Core.TrafficVariety();

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

        public int Lane;
        public bool Varied;              // own speed; matches a row's speed rather than close its way through
        public LaneChange Change;
        public int TargetLane;
        public float ChangeY;            // Pending: start signalling here. Signalling: start moving here. Moving: Y the move began.
        public float FromX;
        public TurnSignal Signal;
    }

    public enum LaneChange { None, Pending, Signalling, Moving }

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

        if (useVariety) UpdateTraffic();

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
        bool single = kind == TinyRoadTrip.Gameplay.Core.TrafficPatternKind.Single;
        foreach (var slot in patternSlots)
            SpawnObstacle(slot.Lane, slot.Row * patternRowGap, single);
        return Mathf.Max(1, rows);
    }

    // single: a lone car (not part of a pattern), which may get its own speed or change lane.
    void SpawnObstacle(int lane, float heightAboveSpawn, bool single)
    {
        float level = difficulty.Level(timeAlive);
        float speedFactor = 1f;
        bool changesLane = false;
        if (useVariety && single)
        {
            speedFactor = variety.RollSpeedFactor(level, patternRandom.NextDouble(), patternRandom.NextDouble());
            changesLane = lanePositions.Length > 1 && variety.RollLaneChange(level, patternRandom.NextDouble());
        }
        float speed = _obstacleSpeed * speedFactor;

        int randomSpriteIndex = Random.Range(0, obstacleSprites.Count);
        Vector3 spawnPosition = lanePositions[lane].position + new Vector3(0f, heightAboveSpawn, 0f);
        Vector2 velocity = new Vector2(0,-speed * SpeedMultiplier);

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

        TurnSignal signal = obs.GetComponent<TurnSignal>();
        if (signal != null) signal.Off(); // a reused car may have been blinking
        else if (changesLane) signal = obs.AddComponent<TurnSignal>();

        activeObstacles.Add(new ActiveObstacle
        {
            GameObject = obs,
            Body = rb,
            Collider = col,
            BaseSpeed = speed,
            PreviousY = obs.transform.position.y,
            Pooled = pooled,
            Lane = lane,
            Varied = speedFactor != 1f,
            Change = changesLane ? LaneChange.Pending : LaneChange.None,
            ChangeY = variety.RollStartY(patternRandom.NextDouble()),
            Signal = signal,
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

    private readonly List<TinyRoadTrip.Gameplay.Core.LaneCar> laneCars = new List<TinyRoadTrip.Gameplay.Core.LaneCar>();
    private static readonly ObstacleYComparer byY = new ObstacleYComparer();

    private sealed class ObstacleYComparer : IComparer<ActiveObstacle>
    {
        public int Compare(ActiveObstacle a, ActiveObstacle b) => a.Body.position.y.CompareTo(b.Body.position.y);
    }

    // Speed variety and lane changes. Runs after CullObstacles, so every record has a live body.
    private void UpdateTraffic()
    {
        // Lowest first: the car ahead of any car in its lane has a smaller index and is settled first.
        activeObstacles.Sort(byY);
        laneCars.Clear();
        for (int i = 0; i < activeObstacles.Count; i++)
            laneCars.Add(new TinyRoadTrip.Gameplay.Core.LaneCar(activeObstacles[i].Lane, activeObstacles[i].Body.position.y));

        int laneCount = lanePositions.Length;
        for (int i = 0; i < activeObstacles.Count; i++)
        {
            ActiveObstacle o = activeObstacles[i];
            float y = laneCars[i].Y;
            float speed = o.BaseSpeed;

            if (o.Varied && variety.WouldCloseRow(laneCars, i, laneCount))
            {
                // Keep pace with the row instead of closing it.
                int nearest = NearestOtherLane(i, variety.RowBand + variety.RowGuardMargin);
                if (nearest >= 0) speed = activeObstacles[nearest].BaseSpeed;
                o.Varied = false;
            }

            for (int j = i - 1; j >= 0; j--)
            {
                if (laneCars[j].Lane != o.Lane) continue;
                speed = variety.FollowSpeed(speed, activeObstacles[j].BaseSpeed, y - laneCars[j].Y);
                break;
            }

            if (speed != o.BaseSpeed)
            {
                o.BaseSpeed = speed;
                o.Body.linearVelocity = new Vector2(0, -speed * SpeedMultiplier);
            }

            UpdateLaneChange(o, i, y, laneCount);
        }
    }

    private void UpdateLaneChange(ActiveObstacle o, int index, float y, int laneCount)
    {
        switch (o.Change)
        {
            case LaneChange.Pending:
                if (y > o.ChangeY) return;
                o.TargetLane = TinyRoadTrip.Gameplay.Core.TrafficVariety.PickTargetLane(o.Lane, laneCount, patternRandom.NextDouble());
                if (o.Signal == null || o.Collider == null || !variety.CanChangeLane(laneCars, index, o.TargetLane, laneCount))
                {
                    o.Change = LaneChange.None; // no room: this car stays in its lane
                    return;
                }
                o.Change = LaneChange.Signalling;
                o.ChangeY = y - variety.SignalDistance;
                o.Signal.Show(o.TargetLane < o.Lane ? -1 : 1, o.Collider.bounds);
                return;

            case LaneChange.Signalling:
                if (y > o.ChangeY) return;
                if (!variety.CanChangeLane(laneCars, index, o.TargetLane, laneCount))
                {
                    o.Change = LaneChange.None;
                    o.Signal.Off();
                    return;
                }
                o.Change = LaneChange.Moving;
                o.ChangeY = y;
                o.FromX = o.Body.position.x;
                o.Lane = o.TargetLane;
                laneCars[index] = new TinyRoadTrip.Gameplay.Core.LaneCar(o.Lane, y);
                return;

            case LaneChange.Moving:
            {
                float t = variety.MoveDistance > 0f ? Mathf.Clamp01((o.ChangeY - y) / variety.MoveDistance) : 1f;
                float x = Mathf.Lerp(o.FromX, lanePositions[o.TargetLane].position.x, Mathf.SmoothStep(0f, 1f, t));
                o.Body.position = new Vector2(x, o.Body.position.y);
                if (t >= 1f)
                {
                    o.Change = LaneChange.None;
                    o.Signal.Off();
                }
                return;
            }
        }
    }

    // Index of the vertically closest car in another lane within band of car i, or -1.
    private int NearestOtherLane(int i, float band)
    {
        int best = -1;
        float bestDy = band;
        for (int j = 0; j < laneCars.Count; j++)
        {
            if (j == i || laneCars[j].Lane == laneCars[i].Lane) continue;
            float dy = Mathf.Abs(laneCars[j].Y - laneCars[i].Y);
            if (dy < bestDy) { bestDy = dy; best = j; }
        }
        return best;
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
