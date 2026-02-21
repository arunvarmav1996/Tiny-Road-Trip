using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // Start is called before the first frame update

    AudioManager audiomanager;
    public GameObject GameOverUI;

    private void Awake()
    {

        



    }
    void Start()
    {
        Time.timeScale = 1;
        AudioManager.instance.Play("BackgroundMusic");
        AudioManager.instance.Play("CarRev");

        if(GameOverUI != null)
        {
           GameOverUI.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Obstacle")
        {
            
            AudioManager.instance.Play("PlayerCrash");
            AudioManager.instance.Stop("CarRev");
            AudioManager.instance.Stop("BackgroundMusic");

            Debug.Log("Collsion Working !!!!!!!!!!!!");

        }

    }

    public void OnPlayerHitObstacle()
    {
        // Stops playing Audio
        Debug.Log("Game Over !");

        Time.timeScale = 0;

        AudioManager.instance.Stop("BackgroundMusic");
        AudioManager.instance.Stop("CarRev");
        AudioManager.instance.Play("GameOver");

        //GameOver UI
        if (GameOverUI!=null) 
        {
            GameOverUI.SetActive(true);
        }
                
    }

    public void RestartGame()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(1);

    }

    public void Close()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(0);
    }

}

