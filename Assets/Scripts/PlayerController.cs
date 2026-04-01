using System.Collections;
using System.Collections.Generic;
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


    private void Awake()
    {
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

    private Vector2 startTouch;
    private int activeFingerId = -1;
    private bool swipeConsumed = false;
    void HandleTouchInput()
    {
        if (Input.touchCount <= 0) return;

        // Track only ONE finger consistently
        Touch touch = Input.GetTouch(0);

        // If we already locked onto a finger, ignore other fingers
        if (activeFingerId != -1 && touch.fingerId != activeFingerId)
            return;

        if (touch.phase == TouchPhase.Began)
        {
            activeFingerId = touch.fingerId;
            startTouch = touch.position;
            swipeConsumed = false;
            return;
        }

        // You can trigger on Ended OR on Moved once it crosses threshold (your choice)
        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            if (swipeConsumed)
            {
                ResetTouch();
                return;
            }

            Vector2 delta = touch.position - startTouch;

            // Ignore tiny swipes/taps
            if (delta.magnitude < minSwipeDistance)
            {
                ResetTouch();
                return;
            }

            swipeConsumed = true;

            // Decide direction WITHOUT normalize (more stable)
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                if (delta.x < 0f) MoveLeft();
                else MoveRight();
            }
            else
            {
                if (delta.y > 0f) MoveForward();
                else MoveBackward();
            }

            ResetTouch();
        }
    }

    private void ResetTouch()
    {
        activeFingerId = -1;
        swipeConsumed = false;
    }
    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            Debug.Log("Left Button Pressed !");
            MoveLeft();
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            Debug.Log("Right Button Pressed !");
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
            Debug.Log("Moving Forward");
            //audioManager.Whoosh(audioManager.whoosh);
        }
    }

    public void MoveBackward()
    {
        if (transform.position.y > minMoveDir.y)
        {
            
            targetPosition = new Vector3(targetPosition.x, transform.position.y - laneDistance, transform.position.z);
            Debug.Log("Moving Backward");
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
        Debug.Log(collision.gameObject.name);

    }


}