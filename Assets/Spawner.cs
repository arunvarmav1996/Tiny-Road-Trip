using System.Collections;
using System.Collections.Generic;
using UnityEditor.Build.Content;
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
    


    private float timer;
    private float _obstacleSpawnTime;
    private float _obstacleSpeed;

    private float timeAlive;

    private void Start()
    {
        
        ResetFactors();
            
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;
        timeAlive += Time.deltaTime;

        if (timer>= _obstacleSpawnTime)
        {
            SpawnObstacle();
            timer = 0f;

        }

        CalculateFactors();

      
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
        rb.velocity = new Vector2(0,-_obstacleSpeed);

        Destroy(obs,6f);
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
