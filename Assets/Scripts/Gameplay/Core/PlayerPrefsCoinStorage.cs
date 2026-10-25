using UnityEngine;

namespace TinyRoadTrip.Gameplay.Core
{
    public sealed class PlayerPrefsCoinStorage : ICoinStorage
    {
        private readonly string key;

        public PlayerPrefsCoinStorage(string key)
        {
            this.key = key;
        }

        public int Load() => PlayerPrefs.GetInt(key, 0);

        public void Store(int balance) => PlayerPrefs.SetInt(key, balance);

        public void Flush() => PlayerPrefs.Save();
    }
}
