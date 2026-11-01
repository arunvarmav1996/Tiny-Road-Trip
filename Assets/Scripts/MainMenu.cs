using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Tooltip("Hidden in browser builds, where a page cannot close itself.")]
    [SerializeField] private GameObject quitButton;

    private void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (quitButton != null) quitButton.SetActive(false);
#endif
    }

    public void PlayGame()
    {
        SceneManager.LoadScene(1);

    }

    public void Close()
    {
        Time.timeScale = 1.0f;
        SceneManager.LoadSceneAsync(0);
    }

    // Quit button. Application.Quit() does nothing in the Editor (stop Play mode instead)
    // and nothing in WebGL builds; Apple discourages quit buttons on iOS.
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
