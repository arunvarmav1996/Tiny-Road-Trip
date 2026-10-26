using System;
using TinyRoadTrip.Gameplay.Core;
using TMPro;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    public readonly struct RunSummary
    {
        public readonly double Meters;
        public readonly double BestMeters;
        public readonly bool IsNewBest;
        public readonly int Coins;
        public readonly int BestCombo;

        public RunSummary(double meters, double bestMeters, bool isNewBest, int coins, int bestCombo)
        {
            Meters = meters;
            BestMeters = bestMeters;
            IsNewBest = isNewBest;
            Coins = coins;
            BestCombo = bestCombo;
        }
    }

    // The run's score: distance in kilometres. Replaces the old Visual Scripting point score on the HUD.
    // Distance = steady travel (1 km per 30 s at normal speed, scaled by Slow-Time/Nitro) + near-miss bonus metres.
    // On game over it saves the best distance and exposes the run summary for the Game Over panel.
    public class RunStats : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameplaySpeedController speedController;
        [SerializeField] private ComboManager comboManager;
        [SerializeField] private CoinManager coinManager;

        [Header("HUD")]
        [Tooltip("The big score text at the top of the screen (ScoreUI).")]
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private string unitSuffix = "<size=45%> km</size>";

        [Header("Tuning")]
        [Tooltip("Metres per gameplay second at normal speed. 33.33 = 1 km every 30 s.")]
        [SerializeField, Min(0f)] private float metersPerSecond = 1000f / 30f;
        [Tooltip("Bonus metres per near-miss point (near miss = 25 points x combo multiplier).")]
        [SerializeField, Min(0f)] private float bonusMetersPerPoint = 1f;
        [Tooltip("PlayerPrefs key for the best distance (metres).")]
        [SerializeField] private string bestDistanceKey = "TinyRoadTrip.BestDistanceMeters";

        private readonly RunDistance run = new RunDistance();
        private long shownStep = -1;
        private int bestCombo;
        private bool finished;

        public double Meters => run.TotalMeters;
        public bool HasResult { get; private set; }
        public RunSummary LastRun { get; private set; }
        public double SavedBestMeters => PlayerPrefs.GetFloat(bestDistanceKey, 0f);

        public event Action<RunSummary> RunFinished;

        private void Awake()
        {
            if (gameManager != null) gameManager.GameOver += Finish;
            if (comboManager != null) comboManager.NearMissAwarded += OnNearMiss;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.GameOver -= Finish;
            if (comboManager != null) comboManager.NearMissAwarded -= OnNearMiss;
        }

        private void Start()
        {
            RefreshHud();
        }

        private void Update()
        {
            if (finished) return;
            float multiplier = speedController != null ? speedController.Multiplier : 1f;
            run.AddTravel(Time.deltaTime, metersPerSecond, multiplier);
            RefreshHud();
        }

        private void OnNearMiss(NearMissResult result)
        {
            if (finished) return;
            run.AddBonus(result.Points * bonusMetersPerPoint);
            bestCombo = Mathf.Max(bestCombo, result.Combo);
            RefreshHud();
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;

            BestResult best = BestScore.Submit(run.TotalMeters, SavedBestMeters);
            if (best.IsNewBest)
            {
                PlayerPrefs.SetFloat(bestDistanceKey, (float)best.Best);
                PlayerPrefs.Save();
            }

            int coins = coinManager != null ? coinManager.RunCoins : 0;
            LastRun = new RunSummary(run.TotalMeters, best.Best, best.IsNewBest, coins, bestCombo);
            HasResult = true;
            RunFinished?.Invoke(LastRun);
        }

        // Rebuilds the text only when the shown value changes (every 10 m).
        private void RefreshHud()
        {
            if (distanceText == null) return;
            long step = DistanceFormat.DisplayStep(run.TotalMeters);
            if (step == shownStep) return;
            shownStep = step;
            distanceText.SetText(DistanceFormat.Kilometers(run.TotalMeters) + unitSuffix);
        }
    }
}
