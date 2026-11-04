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
    [Tooltip("Minimum window between cars, in player lengths: any two cars are side by side in a row or at least this " +
             "far apart bumper to bumper. Eases from the easy factor to the hard factor as difficulty rises.")]
    [SerializeField] private TinyRoadTrip.Gameplay.Core.TrafficGaps gaps = new TinyRoadTrip.Gameplay.Core.TrafficGaps();

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
    [Tooltip("Re-enables collisions between the player and a pooled car when it is reused (the shield's " +
             "invulnerability window disables them per car). Its height is the player length the gaps are based on.")]
    [SerializeField] private Collider2D playerCollider;

    // Live obstacle record shared with near-miss detection, collectible placement and the shield.
    public sealed class ActiveObstacle
    {
        public GameObject GameObject;
        public Rigidbody2D Body;
        public Collider2D Collider;
        public float BaseSpeed;
        public float Length;             // collider height, for the gap rule
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

    private float carLength = 1.5f;   // from the obstacle prefab's collider, for cars about to spawn
    private float PlayerLength => playerCollider != null ? playerCollider.bounds.size.y : carLength;
    private float ClearGap => gaps.MinClearGap(PlayerLength, difficulty.Level(timeAlive));
    private float lastPatternHeight;  // how far above the spawn line the last pattern's top row started

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
        carLength = PrefabCarLength();

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
            SpawnPattern();
            // A multi-row or lifted pattern starts higher up; hold the next spawn until its top row has entered.
            timer = _obstacleSpeed > 0f ? -lastPatternHeight / _obstacleSpeed : 0f;

        }

        CalculateFactors();

        CullObstacles();

        if (useVariety) UpdateTraffic();

    }

    private readonly System.Random patternRandom = new System.Random();
    private readonly List<TinyRoadTrip.Gameplay.Core.CarSlot> patternSlots = new List<TinyRoadTrip.Gameplay.Core.CarSlot>();
    private readonly List<TinyRoadTrip.Gameplay.Core.GapCar> gapExisting = new List<TinyRoadTrip.Gameplay.Core.GapCar>();
    private readonly List<TinyRoadTrip.Gameplay.Core.GapCar> gapIncoming = new List<TinyRoadTrip.Gameplay.Core.GapCar>();

    // Returns the number of rows spawned.
    int SpawnPattern()
    {
        lastPatternHeight = 0f;
        if (lanePositions.Length == 0 || obstacleSprites.Count == 0)
            return 1;

        var kind = TinyRoadTrip.Gameplay.Core.TrafficPatternKind.Single;
        if (usePatterns && !IsCalmPhase)
            kind = TinyRoadTrip.Gameplay.Core.TrafficPatterns.Pick(difficulty.Level(timeAlive), patternRandom.NextDouble());

        int rows = TinyRoadTrip.Gameplay.Core.TrafficPatterns.Build(kind, lanePositions.Length, patternRandom, patternSlots);
        bool single = kind == TinyRoadTrip.Gameplay.Core.TrafficPatternKind.Single;

        // Rows of a pattern are a full window apart, and the whole pattern is raised above the spawn line until
        // every car is side by side with, or a window away from, the cars already on the road.
        float clear = ClearGap;
        float rowGap = Mathf.Max(patternRowGap, TinyRoadTrip.Gameplay.Core.TrafficGaps.CentreDistance(carLength, carLength, clear));
        gapIncoming.Clear();
        foreach (var slot in patternSlots)
            gapIncoming.Add(new TinyRoadTrip.Gameplay.Core.GapCar(slot.Lane, lanePositions[slot.Lane].position.y + slot.Row * rowGap, carLength));
        gapExisting.Clear();
        foreach (ActiveObstacle o in activeObstacles)
            if (o.Body != null) gapExisting.Add(new TinyRoadTrip.Gameplay.Core.GapCar(o.Lane, o.Body.position.y, o.Length));
        float lift = gaps.Lift(gapExisting, gapIncoming, clear, lanePositions.Length);

        foreach (var slot in patternSlots)
            SpawnObstacle(slot.Lane, slot.Row * rowGap + lift, single);
        lastPatternHeight = (Mathf.Max(1, rows) - 1) * rowGap + lift;
        return Mathf.Max(1, rows);
    }

    // Height of the obstacle prefab's collider, the length of a car that is about to spawn.
    private float PrefabCarLength()
    {
        if (obstaclePrefab == null) return carLength;
        float scaleY = Mathf.Abs(obstaclePrefab.transform.localScale.y);
        var box = obstaclePrefab.GetComponent<BoxCollider2D>();
        if (box != null) return box.size.y * scaleY;
        var capsule = obstaclePrefab.GetComponent<CapsuleCollider2D>();
        if (capsule != null) return capsule.size.y * scaleY;
        var circle = obstaclePrefab.GetComponent<CircleCollider2D>();
        if (circle != null) return circle.radius * 2f * scaleY;
        return carLength;
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
        // A pooled car outlives runs (and scene reloads) and may still be turned from skidding on snow.
        obs.transform.rotation = Quaternion.identity;
        rb.rotation = 0f;

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
            Length = col != null ? col.bounds.size.y : carLength,
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
        float clear = ClearGap;
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

            // Never catch up on a car ahead (lower on screen) to closer than a clear window: in the same lane
            // that is following, in another lane it keeps the window the player needs between the two.
            for (int j = i - 1; j >= 0; j--)
            {
                ActiveObstacle ahead = activeObstacles[j];
                float dy = y - laneCars[j].Y;
                if (laneCars[j].Lane != o.Lane && dy <= gaps.AlignTolerance) continue; // side by side in a row
                float need = TinyRoadTrip.Gameplay.Core.TrafficGaps.CentreDistance(o.Length, ahead.Length, clear);
                speed = TinyRoadTrip.Gameplay.Core.TrafficVariety.FollowSpeed(speed, ahead.BaseSpeed, dy, need);
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
                if (o.Signal == null || o.Collider == null || !variety.CanChangeLane(laneCars, index, o.TargetLane, laneCount, LaneChangeClearance(o)))
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
                if (!variety.CanChangeLane(laneCars, index, o.TargetLane, laneCount, LaneChangeClearance(o)))
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

    // Centre-to-centre room a lane-changer needs in its target lane: a clear window on either side.
    private float LaneChangeClearance(ActiveObstacle o)
    {
        return TinyRoadTrip.Gameplay.Core.TrafficGaps.CentreDistance(o.Length, carLength, ClearGap);
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
