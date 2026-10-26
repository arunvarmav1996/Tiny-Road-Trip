using TinyRoadTrip.Gameplay.Core;
using TMPro;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Fills the Game Over panel from RunStats when the panel opens (GameManager activates it after RunStats has
    // finalised the run). Animations use unscaled time because the game is frozen (timeScale 0) at game over.
    public class GameOverSummary : MonoBehaviour
    {
        [SerializeField] private RunStats runStats;

        [Header("Values")]
        [SerializeField] private TMP_Text distanceValue;
        [SerializeField] private TMP_Text bestValue;
        [SerializeField] private TMP_Text coinsValue;
        [SerializeField] private TMP_Text comboValue;
        [SerializeField] private string unitSuffix = "<size=60%> km</size>";

        [Header("New Best")]
        [SerializeField] private RectTransform newBestBadge;
        [SerializeField] private float badgePulseSpeed = 6f;
        [SerializeField] private float badgePulseAmount = 0.08f;

        private Vector3 badgeBaseScale = Vector3.one;
        private bool animateBadge;

        private void Awake()
        {
            if (newBestBadge != null) badgeBaseScale = newBestBadge.localScale;
        }

        private void OnEnable()
        {
            animateBadge = false;
            if (newBestBadge != null) newBestBadge.gameObject.SetActive(false);

            // The panel is also enabled briefly while the scene loads; only fill it once a run has ended.
            if (runStats == null || !runStats.HasResult) return;

            RunSummary run = runStats.LastRun;
            if (distanceValue != null) distanceValue.SetText(DistanceFormat.Kilometers(run.Meters) + unitSuffix);
            if (bestValue != null) bestValue.SetText(DistanceFormat.Kilometers(run.BestMeters) + unitSuffix);
            if (coinsValue != null) coinsValue.SetText("+{0}", run.Coins);
            if (comboValue != null)
            {
                if (run.BestCombo > 0) comboValue.SetText("x{0}", run.BestCombo);
                else comboValue.SetText("-");
            }

            if (run.IsNewBest && newBestBadge != null)
            {
                newBestBadge.gameObject.SetActive(true);
                animateBadge = true;
            }
        }

        private void Update()
        {
            if (!animateBadge || newBestBadge == null) return;
            float s = 1f + badgePulseAmount * Mathf.Sin(Time.unscaledTime * badgePulseSpeed);
            newBestBadge.localScale = badgeBaseScale * s;
        }
    }
}
