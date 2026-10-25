using System.Collections;
using System.Collections.Generic;
using TinyRoadTrip.Gameplay;
using UnityEngine;


public class PlayerCollision : MonoBehaviour
{

    [SerializeField] private GameManager gameManager;

    [Header("Power-Ups (optional)")]
    [Tooltip("When set, an active shield absorbs the hit instead of ending the run.")]
    [SerializeField] private PowerUpManager powerUps;
    [Tooltip("When set, any obstacle collision resets the near-miss combo.")]
    [SerializeField] private ComboManager combo;

    private void Start()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.CompareTag("Obstacle"))
        {
            if (combo != null) combo.ResetCombo();
            if (powerUps != null && powerUps.TryAbsorbObstacleHit(collision.collider)) return;

            gameManager.OnPlayerHitObstacle();
        }
    }


}
