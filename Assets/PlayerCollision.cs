using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PlayerCollision : MonoBehaviour
{
    
    [SerializeField]private GameManager gameManager;

    /*private void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
    }*/

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.CompareTag("Obstacle"))
        {
            gameManager.OnPlayerHitObstacle();
        }
    }


}
