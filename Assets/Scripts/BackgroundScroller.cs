using System.Collections;
using System.Collections.Generic;
using TinyRoadTrip.Gameplay;
using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{

    public BoxCollider2D collider;
    [Tooltip("World units per second the road moves down at normal speed: how fast the player seems to drive. " +
             "Keep below the slowest traffic speed (Spawner difficulty), or oncoming cars look like they reverse.")]
    [SerializeField] private float scrollSpeed = 3f;
    [Tooltip("The road image repeats every this many pixels (one pair of lane dashes x 2). The loop jumps by exactly " +
             "this much so the jump is invisible; any other distance makes the lane dashes hop.")]
    [SerializeField] private float loopPixels = 408f;
    [Tooltip("The road jumps back up one loop when it gets this low. Set so the sprite covers the view on 20:9 phones.")]
    [SerializeField] private float wrapBelowY = -2.89f;
    private float loopHeight;
    private float multiplier = 1f;

    [Tooltip("Optional. Scales the scroll speed for Slow-Time / Nitro.")]
    [SerializeField] private GameplaySpeedController speedController;

    // World units per second the road moves down at normal speed (collectibles ride the road at this speed).
    public float BaseScrollSpeed => scrollSpeed;

    // World units per second the road is moving right now (negative: down the screen).
    public float Velocity => -scrollSpeed * multiplier;

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
        collider.enabled =false;

        // Moved in Update, once per rendered frame: physics steps at 50 Hz would judder against the screen's 60.
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null) body.simulated = false;

        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        loopHeight = sprite.bounds.size.y / sprite.sprite.rect.height * loopPixels;

        if (speedController != null)
        {
            multiplier = speedController.Multiplier;
            speedController.MultiplierChanged += ApplySpeedMultiplier;
        }
    }

    void OnDestroy()
    {
        if (speedController != null)
            speedController.MultiplierChanged -= ApplySpeedMultiplier;
    }

    private void ApplySpeedMultiplier(float value)
    {
        multiplier = value;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 position = transform.position;
        position.y += Velocity * Time.deltaTime;
        if (position.y < wrapBelowY) position.y += loopHeight;
        transform.position = position;
    }
}
