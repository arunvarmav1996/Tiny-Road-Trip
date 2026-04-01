using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VehicleScript : MonoBehaviour
{
    public Rigidbody2D rb;
    public SpriteRenderer spriteRenderer;
    public float vehicleSpeed;
    public List<Sprite> obstacleSprites;
    public void SetVehicleStats(Vector2 newVelocity, Sprite sprite){
        rb.velocity = Vector2.zero;
        rb.velocity = newVelocity;
        spriteRenderer.sprite = sprite;
        //Invoke("DisableObjectAreUse", 6);
    }
}
