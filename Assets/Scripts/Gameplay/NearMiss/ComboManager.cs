using System;
using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Near-miss combo scoring. Points go into the existing Visual Scripting score through ScoreBridge.
    public class ComboManager : MonoBehaviour
    {
        [SerializeField] private ScoreBridge scoreBridge;
        [SerializeField] private GameManager gameManager;

        [Tooltip("Points for one near miss; multiplied by the combo multiplier.")]
        [SerializeField, Min(0)] private int basePoints = 25;
        [Tooltip("Highest combo multiplier (combo count is uncapped, the multiplier is not).")]
        [SerializeField, Min(1)] private int maxMultiplier = 5;
        [Tooltip("Gameplay seconds without a near miss before the combo resets.")]
        [SerializeField, Min(0.1f)] private float comboTimeout = 3f;

        private ComboTracker tracker;

        public int Combo => tracker.Combo;

        public event Action<NearMissResult> NearMissAwarded;
        public event Action ComboReset;

        // Subscribed in Awake/OnDestroy because "enabled" is toggled to tick only while a combo is live.
        private void Awake()
        {
            tracker = new ComboTracker(basePoints, maxMultiplier, comboTimeout);
            if (gameManager != null) gameManager.GameOver += ResetCombo;
            enabled = false;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.GameOver -= ResetCombo;
        }

        public void RegisterNearMiss()
        {
            NearMissResult result = tracker.Register(Time.time);
            if (scoreBridge != null) scoreBridge.AddPoints(result.Points);
            enabled = true; // tick only while a combo can time out
            NearMissAwarded?.Invoke(result);
        }

        public void ResetCombo()
        {
            if (tracker.Combo == 0) return;
            tracker.Reset();
            enabled = false;
            ComboReset?.Invoke();
        }

        private void Update()
        {
            if (tracker.Tick(Time.time))
            {
                enabled = false;
                ComboReset?.Invoke();
            }
        }
    }
}
