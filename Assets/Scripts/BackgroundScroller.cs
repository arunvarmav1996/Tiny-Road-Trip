using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{

    public BoxCollider2D collider;
    public Rigidbody2D rb;
    private float height;
    private float scrollspeed =-2f;


    // Start is called before the first frame update
    void Start()
    {
        collider = GetComponent<BoxCollider2D>();
        rb = GetComponent<Rigidbody2D>();
        height = collider.size.y;
        collider.enabled =false;

        rb.velocity = new Vector2(0,scrollspeed);
    }

    // Update is called once per frame
    void Update()
    {
        if(transform.position.y<-height)
        {
            Vector2 resetPosition = new Vector2(0, height*1f);
            transform.position = (Vector2)transform.position +resetPosition;
        }
    }
}
