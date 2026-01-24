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
    
    AudioManager audioManager;

    private void Awake()
    {
        audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>();
    }
    
    // Start is called before the first frame update

    void Start()
    {
         // Start in the middle lane
        currentLane = numLanes / 2;
        targetPosition = transform.position;

        audioManager.PlaySFX(audioManager.carRev);
        
    }

    // Update is called once per frame
    void Update()
    {
        HandleInput();
        MoveToTargetLane();
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
        
        {
            //currentLane--;
            targetPosition = new Vector3(targetPosition.x , transform.position.y + laneDistance, transform.position.z);
            Debug.Log("Moving Forward");
            //audioManager.Whoosh(audioManager.whoosh);
        }
    }

    public void MoveBackward()
    {
        
        {
            
            targetPosition = new Vector3(targetPosition.x, transform.position.y - laneDistance, transform.position.z);
            Debug.Log("Moving Backward");
            //audioManager.Whoosh(audioManager.whoosh);
        }
    }

    public void MoveLeft()
    {
        //if (currentLane > 0)
        {
            //currentLane--;
            targetPosition = new Vector3((targetPosition.x - laneDistance), transform.position.y, transform.position.z);
            audioManager.Whoosh(audioManager.whoosh);
        }
    }

    public void MoveRight()
    {
        //if (currentLane < numLanes - 1)
        {
            //currentLane++;
            targetPosition = new Vector3((targetPosition.x + laneDistance), transform.position.y, transform.position.z);
            audioManager.Whoosh(audioManager.whoosh);
        }
    }

    void MoveToTargetLane()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, moveSpeed * Time.deltaTime);
    }
    
    void OnCollisionEnter2D(Collision2D collision)
{    
            Debug.Log(collision.gameObject.name);
        audioManager.PlaySFX(audioManager.crash);
        


    }


}