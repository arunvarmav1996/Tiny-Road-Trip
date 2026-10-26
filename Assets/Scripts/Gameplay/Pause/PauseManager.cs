using System;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Pause button, pause screen, and automatic pause when the app is minimised or loses focus.
    // Pausing freezes time (timeScale 0) and audio; everything already runs on scaled time
    // (spawning, power-up timers, distance, combo timeout), so it all stops together.
    // The game never auto-resumes: the player returns to the pause screen and taps Resume.
    public class PauseManager : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [Tooltip("Disabled while paused so taps on the pause screen are not read as swipes.")]
        [SerializeField] private PlayerController player;

        [Header("UI")]
        [SerializeField] private GameObject pauseButton;
        [SerializeField] private GameObject pausePanel;

        [Header("Behaviour")]
        [Tooltip("Pause when the app loses focus (notification shade, incoming call, app switcher).")]
        [SerializeField] private bool pauseOnFocusLoss = true;
        [Tooltip("Also pause on focus loss inside the Unity Editor (off: clicking another window would pause play mode).")]
        [SerializeField] private bool pauseOnFocusLossInEditor = false;
        [Tooltip("Android Back / Escape toggles pause.")]
        [SerializeField] private bool backButtonToggles = true;

        private float timeScaleBeforePause = 1f;
        private bool gameOver;

        public bool IsPaused { get; private set; }

        public event Action<bool> PauseChanged;

        private void Awake()
        {
            if (gameManager != null) gameManager.GameOver += OnGameOver;
            if (pausePanel != null) pausePanel.SetActive(false);
            if (pauseButton != null) pauseButton.SetActive(true);
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.GameOver -= OnGameOver;
            // Leaving the scene (Restart / Main Menu) must never leave audio muted.
            AudioListener.pause = false;
        }

        private void Update()
        {
            if (backButtonToggles && Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsPaused) Resume();
                else Pause();
            }
        }

        // App sent to background (home button, app switcher, screen off).
        private void OnApplicationPause(bool paused)
        {
            if (paused) Pause();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus || !pauseOnFocusLoss) return;
#if UNITY_EDITOR
            if (!pauseOnFocusLossInEditor) return;
#endif
            Pause();
        }

        public void Pause()
        {
            if (IsPaused || gameOver) return;
            IsPaused = true;

            timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (player != null) player.enabled = false;

            if (pausePanel != null) pausePanel.SetActive(true);
            if (pauseButton != null) pauseButton.SetActive(false);
            PauseChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;

            Time.timeScale = timeScaleBeforePause;
            AudioListener.pause = false;
            if (player != null) player.enabled = true;

            if (pausePanel != null) pausePanel.SetActive(false);
            if (pauseButton != null) pauseButton.SetActive(!gameOver);
            PauseChanged?.Invoke(false);
        }

        // Pause screen buttons. GameManager restores timeScale; audio is un-muted here and in OnDestroy.
        public void Restart()
        {
            AudioListener.pause = false;
            if (gameManager != null) gameManager.RestartGame();
        }

        public void MainMenu()
        {
            AudioListener.pause = false;
            if (gameManager != null) gameManager.Close();
        }

        private void OnGameOver()
        {
            gameOver = true;
            if (pauseButton != null) pauseButton.SetActive(false);
        }
    }
}
