using System.Collections;
using System.Collections.Generic;
using TinyRoadTrip.Gameplay;
using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{

    public BoxCollider2D collider;
    public Rigidbody2D rb;
    private float height;
    private float scrollspeed =-2f;

    [Tooltip("Optional. Scales the scroll speed for Slow-Time / Nitro.")]
    [SerializeField] private GameplaySpeedController speedController;

    // World units per second the road moves down at normal speed (collectibles ride the road at this speed).
    public float BaseScrollSpeed => -scrollspeed;

    [Tooltip("Aspect-ratio road-width formula from the Difficulty-Setup branch. Off by default: ResponsiveCamera " +
             "already adapts the view, and using both scales the game twice.")]
    [SerializeField] private bool adaptWidthToAspect = false;


    // Start is called before the first frame update
    public void Awake()
    {
        if (!adaptWidthToAspect) return;

        double roadWidth = -0.269954 * ((float)Screen.height /(float) Screen.width) + 0.93991f;
        Debug.Log($"roadWidth {roadWidth} and screenRatio : {(float)Screen.height/(float)Screen.width}");
        gameObject.transform.localScale = new Vector3((float)roadWidth,
            gameObject.gameObject.transform.localScale.y,
            gameObject.transform.localScale.z);
    }
    void Start()
    {
        // Assigned in the scene; fall back to the component so a missing reference cannot break the scroller.
        if (collider == null)
            collider = GetComponent<BoxCollider2D>();
        rb = GetComponent<Rigidbody2D>();
        height = collider.size.y;
        collider.enabled =false;

        float multiplier = 1f;
        if (speedController != null)
        {
            multiplier = speedController.Multiplier;
            speedController.MultiplierChanged += ApplySpeedMultiplier;
        }
        ApplySpeedMultiplier(multiplier);
    }

    void OnDestroy()
    {
        if (speedController != null)
            speedController.MultiplierChanged -= ApplySpeedMultiplier;
    }

    private void ApplySpeedMultiplier(float multiplier)
    {
        rb.linearVelocity = new Vector2(0,scrollspeed * multiplier);
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
