using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{

    [Header("SpawnerSettings")]
    public List<Sprite> obstacleSprites;
    public GameObject obstaclePrefab;
    public Transform[] lanePositions;
    public float spawnInterval = 1.5f;
    public float obstacleSpeed = 5f;

    private float timer;

    public float spawnSpeed = 5f;     // base speed
    public float speedIncrease = 0.2f;
    public float nextThreshold = 10f; // score at which speed increases next
    
    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;

        if (timer>= spawnInterval)
        {
            SpawnObstacle();
            timer = 0f;

        }

        //float currentScore = ScoreManager.Instance.score;

    /*if (currentScore >= nextThreshold)
    {
        spawnSpeed += speedIncrease;
        nextThreshold += 10f;     // set next goal
        Debug.Log("Speed Increased! New Speed = " + spawnSpeed);
    }*/

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
        rb.velocity = new Vector2(0,-obstacleSpeed);

        Destroy(obs,6f);
    }
}
