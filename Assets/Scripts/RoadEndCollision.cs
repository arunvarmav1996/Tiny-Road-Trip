using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoadEndCollision : MonoBehaviour
{


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {    
            collision.gameObject.SetActive(false);
        }

    }
}
