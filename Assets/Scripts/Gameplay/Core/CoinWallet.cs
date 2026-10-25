using System;

namespace TinyRoadTrip.Gameplay.Core
{
    public interface ICoinStorage
    {
        int Load();
        void Store(int balance);   // cheap, in-memory write
        void Flush();              // persist to disk
    }

    // Run coins reset every scene load; the balance persists through ICoinStorage.
    // TrySpend is the hook for a future car shop / upgrade system.
    public sealed class CoinWallet
    {
        private readonly ICoinStorage storage;
        private bool dirty;

        public int Balance { get; private set; }
        public int RunCoins { get; private set; }

        public CoinWallet(ICoinStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            Balance = Math.Max(0, storage.Load());
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            RunCoins = SafeAdd(RunCoins, amount);
            Balance = SafeAdd(Balance, amount);
            storage.Store(Balance);
            dirty = true;
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || amount > Balance) return false;
            Balance -= amount;
            storage.Store(Balance);
            dirty = true;
            return true;
        }

        public void Save()
        {
            if (!dirty) return;
            storage.Flush();
            dirty = false;
        }

        private static int SafeAdd(int a, int b)
        {
            long sum = (long)a + b;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }
    }
}
