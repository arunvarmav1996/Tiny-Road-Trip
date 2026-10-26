using System.Collections;
using System.Collections.Generic;
using TinyRoadTrip.Gameplay.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{

    public float laneDistance = 1.7f;   // Distance between lanes
    public float moveSpeed = 5f;       // Speed of lane transition
    public int numLanes = 3;            // Total lanes on the road (odd number preferred)
    public float dashSpeed = 1.0f;
    
    private int currentLane;            // 0 = middle lane
    private Vector3 targetPosition;
    
    private Vector2 maxMoveDir = new Vector2(2,2.1f);
    private Vector2 minMoveDir = new Vector2(-2, -4.75f);

    [Tooltip("Aspect-ratio lane-distance formula from the Difficulty-Setup branch. Off by default: ResponsiveCamera " +
             "already adapts the view, and using both scales the game twice.")]
    [SerializeField] private bool adaptLaneDistanceToAspect = false;


    private void Awake()
    {
        if (!adaptLaneDistanceToAspect) return;

        double loc = -0.790594 * ((float)Screen.height / Screen.width) + 2.52476f;
        laneDistance = (float)loc;
        Debug.Log("Screen Width" + Screen.width + " Screen Height " + Screen.height + "Lane Dis " + laneDistance);
    }
    
    // Start is called before the first frame update

    void Start()
    {
         // Start in the middle lane
        currentLane = numLanes / 2;
        targetPosition = transform.position;

    
        
    }

    // Update is called once per frame
    void Update()
    {
        HandleTouchInput();
        HandleInput();
        MoveToTargetLane();
    }

    [SerializeField] private float minSwipeDistance = 60f; // pixels (tune)

    private int activeFingerId = -1;
    private SwipeDetector swipe;

    // The lane change fires as soon as the drag passes minSwipeDistance (no need to lift the finger);
    // a quick flick that ends before any move frame is still caught on release. One move per touch.
    void HandleTouchInput()
    {
        if (Input.touchCount <= 0) return;

        // Track only ONE finger consistently
        Touch touch = Input.GetTouch(0);

        // If we already locked onto a finger, ignore other fingers
        if (activeFingerId != -1 && touch.fingerId != activeFingerId)
            return;

        SwipeDirection direction = SwipeDirection.None;
        switch (touch.phase)
        {
            case TouchPhase.Began:
                activeFingerId = touch.fingerId;
                swipe.Begin(touch.position.x, touch.position.y);
                return;
            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                direction = swipe.Move(touch.position.x, touch.position.y);
                break;
            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                direction = swipe.End(touch.position.x, touch.position.y);
                activeFingerId = -1;
                break;
        }

        switch (direction)
        {
            case SwipeDirection.Left: MoveLeft(); break;
            case SwipeDirection.Right: MoveRight(); break;
            case SwipeDirection.Up: MoveForward(); break;
            case SwipeDirection.Down: MoveBackward(); break;
        }
    }

    // Re-enabled after a pause: the touch that pressed Resume ends while this component is active,
    // so ignore any in-progress touch instead of reading it as a swipe.
    private void OnEnable()
    {
        if (swipe == null) swipe = new SwipeDetector(minSwipeDistance);
        activeFingerId = -1;
        swipe.Cancel();
    }
    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            MoveLeft();
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            MoveRight();
        }
        else if(Input.GetKeyDown(KeyCode.UpArrow))
        {
            MoveForward();
        }
        else if(Input.GetKeyDown(KeyCode.DownArrow))
        {
            MoveBackward();
        }
    }

    public void MoveForward()
    {
        if (transform.position.y < maxMoveDir.y)
        {
            //currentLane--;
            targetPosition = new Vector3(targetPosition.x , transform.position.y + laneDistance, transform.position.z);
            //audioManager.Whoosh(audioManager.whoosh);
        }
    }

    public void MoveBackward()
    {
        if (transform.position.y > minMoveDir.y)
        {
            
            targetPosition = new Vector3(targetPosition.x, transform.position.y - laneDistance, transform.position.z);
            //audioManager.Whoosh(audioManager.whoosh);
        }
    }

    public void MoveLeft()
    {
        if (transform.position.x>minMoveDir.x)
        {
            //currentLane--;
            targetPosition = new Vector3((targetPosition.x - laneDistance), transform.position.y, transform.position.z);
            
        }
    }

    public void MoveRight()
    {
        if (transform.position.x < maxMoveDir.x)
        {
            //currentLane++;
            targetPosition = new Vector3((targetPosition.x + laneDistance), transform.position.y, transform.position.z);
           
        }
    }

    void MoveToTargetLane()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, moveSpeed * Time.deltaTime);
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        AudioManager.instance.Play("PlayerCrash");

    }


}