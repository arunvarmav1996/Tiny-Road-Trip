using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
   
    public void PlayGame()
    {
        SceneManager.LoadScene(1);

    }

    public void Close()
    {
        Time.timeScale = 1.0f;
        SceneManager.LoadSceneAsync(0);
    }
}
