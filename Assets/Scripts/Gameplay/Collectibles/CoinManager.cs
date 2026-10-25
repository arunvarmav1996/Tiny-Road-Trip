using System;
using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Run coins + persistent balance. Writes PlayerPrefs on every pickup (in memory) and flushes
    // to disk on game over, app pause/quit and scene unload (restart / close).
    public class CoinManager : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [Tooltip("PlayerPrefs key for the persistent coin balance.")]
        [SerializeField] private string saveKey = "TinyRoadTrip.TotalCoins";

        private CoinWallet wallet;

        public int RunCoins => wallet.RunCoins;
        public int TotalCoins => wallet.Balance;

        // (runCoins, totalCoins)
        public event Action<int, int> CoinsChanged;

        private void Awake()
        {
            wallet = new CoinWallet(new PlayerPrefsCoinStorage(saveKey));
        }

        private void OnEnable()
        {
            if (gameManager != null) gameManager.GameOver += Save;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.GameOver -= Save;
            Save();
        }

        private void Start()
        {
            CoinsChanged?.Invoke(wallet.RunCoins, wallet.Balance);
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            wallet.Add(amount);
            CoinsChanged?.Invoke(wallet.RunCoins, wallet.Balance);
        }

        // Hook for a future shop / upgrade system.
        public bool TrySpend(int amount)
        {
            if (!wallet.TrySpend(amount)) return false;
            Save();
            CoinsChanged?.Invoke(wallet.RunCoins, wallet.Balance);
            return true;
        }

        public void Save()
        {
            wallet?.Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        private void OnApplicationQuit()
        {
            Save();
        }
    }
}
