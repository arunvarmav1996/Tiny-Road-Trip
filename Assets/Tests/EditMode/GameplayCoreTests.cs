using NUnit.Framework;
using TinyRoadTrip.Gameplay.Core;

namespace TinyRoadTrip.Gameplay.Tests
{
    public class ComboTrackerTests
    {
        [Test]
        public void ConsecutiveNearMisses_IncreaseMultiplierUpToCap()
        {
            var combo = new ComboTracker(basePoints: 25, maxMultiplier: 3, timeout: 3f);

            Assert.AreEqual(25, combo.Register(0f).Points);
            NearMissResult second = combo.Register(1f);
            Assert.AreEqual(2, second.Combo);
            Assert.AreEqual(50, second.Points);
            Assert.AreEqual(75, combo.Register(2f).Points);
            NearMissResult fourth = combo.Register(3f);
            Assert.AreEqual(4, fourth.Combo);
            Assert.AreEqual(3, fourth.Multiplier, "multiplier is capped");
            Assert.AreEqual(75, fourth.Points);
        }

        [Test]
        public void ComboResets_AfterTimeout()
        {
            var combo = new ComboTracker(25, 5, 3f);
            combo.Register(0f);
            combo.Register(1f);

            Assert.IsFalse(combo.Tick(3.9f), "still inside timeout");
            Assert.IsTrue(combo.Tick(4.1f), "expires once");
            Assert.IsFalse(combo.Tick(5f), "only reports expiry once");
            Assert.AreEqual(0, combo.Combo);
            Assert.AreEqual(1, combo.Register(10f).Combo);
        }

        [Test]
        public void RegisterAfterTimeout_StartsNewCombo()
        {
            var combo = new ComboTracker(25, 5, 3f);
            combo.Register(0f);
            NearMissResult late = combo.Register(5f);
            Assert.AreEqual(1, late.Combo);
            Assert.AreEqual(25, late.Points);
        }

        [Test]
        public void Reset_ClearsCombo()
        {
            var combo = new ComboTracker(25, 5, 3f);
            combo.Register(0f);
            combo.Register(0.5f);
            combo.Reset();
            Assert.AreEqual(0, combo.Combo);
            Assert.AreEqual(1, combo.Register(0.6f).Combo);
        }
    }

    public class CoinWalletTests
    {
        private sealed class FakeStorage : ICoinStorage
        {
            public int Stored;
            public int Flushes;
            public FakeStorage(int initial) { Stored = initial; }
            public int Load() => Stored;
            public void Store(int balance) => Stored = balance;
            public void Flush() => Flushes++;
        }

        [Test]
        public void Add_UpdatesRunAndPersistentBalance()
        {
            var storage = new FakeStorage(100);
            var wallet = new CoinWallet(storage);

            wallet.Add(3);
            wallet.Add(2);

            Assert.AreEqual(5, wallet.RunCoins);
            Assert.AreEqual(105, wallet.Balance);
            Assert.AreEqual(105, storage.Stored);
        }

        [Test]
        public void NewWallet_LoadsBalance_ButStartsRunAtZero()
        {
            var storage = new FakeStorage(42);
            var wallet = new CoinWallet(storage);
            Assert.AreEqual(42, wallet.Balance);
            Assert.AreEqual(0, wallet.RunCoins);
        }

        [Test]
        public void Save_FlushesOnlyWhenDirty()
        {
            var storage = new FakeStorage(0);
            var wallet = new CoinWallet(storage);
            wallet.Save();
            Assert.AreEqual(0, storage.Flushes);
            wallet.Add(1);
            wallet.Save();
            wallet.Save();
            Assert.AreEqual(1, storage.Flushes);
        }

        [Test]
        public void IgnoresNonPositiveAmounts_AndNegativeStoredBalance()
        {
            var wallet = new CoinWallet(new FakeStorage(-5));
            Assert.AreEqual(0, wallet.Balance);
            wallet.Add(0);
            wallet.Add(-3);
            Assert.AreEqual(0, wallet.RunCoins);
        }

        [Test]
        public void TrySpend_RequiresSufficientBalance()
        {
            var wallet = new CoinWallet(new FakeStorage(10));
            Assert.IsFalse(wallet.TrySpend(11));
            Assert.IsTrue(wallet.TrySpend(10));
            Assert.AreEqual(0, wallet.Balance);
        }

        [Test]
        public void Add_ClampsAtIntMax()
        {
            var wallet = new CoinWallet(new FakeStorage(int.MaxValue - 1));
            wallet.Add(5);
            Assert.AreEqual(int.MaxValue, wallet.Balance);
        }
    }

    public class EffectTimerTests
    {
        [Test]
        public void ExpiresExactlyOnce()
        {
            var timer = new EffectTimer();
            timer.Start(1f);
            Assert.IsFalse(timer.Tick(0.6f));
            Assert.AreEqual(0.4f, timer.Normalized, 1e-4f);
            Assert.IsTrue(timer.Tick(0.6f));
            Assert.IsFalse(timer.IsRunning);
            Assert.IsFalse(timer.Tick(0.1f));
        }

        [Test]
        public void Restart_RefreshesToFullDuration()
        {
            var timer = new EffectTimer();
            timer.Start(5f);
            timer.Tick(4f);
            timer.Start(5f);
            Assert.AreEqual(5f, timer.Remaining, 1e-4f);
        }
    }

    public class SpeedModifiersTests
    {
        [Test]
        public void SlowTimeAndNitro_Multiply()
        {
            var speed = new SpeedModifiers();
            Assert.AreEqual(1f, speed.Combined);
            speed.Set(SpeedSource.SlowTime, 0.5f);
            speed.Set(SpeedSource.Nitro, 1.5f);
            Assert.AreEqual(0.75f, speed.Combined, 1e-5f);
            speed.Clear(SpeedSource.SlowTime);
            Assert.AreEqual(1.5f, speed.Combined, 1e-5f);
            speed.ClearAll();
            Assert.AreEqual(1f, speed.Combined);
        }

        [Test]
        public void SettingSameSourceTwice_DoesNotStack()
        {
            var speed = new SpeedModifiers();
            speed.Set(SpeedSource.Nitro, 1.5f);
            speed.Set(SpeedSource.Nitro, 1.5f);
            Assert.AreEqual(1.5f, speed.Combined, 1e-5f);
        }
    }

    public class GameplayMathTests
    {
        [Test]
        public void HorizontalGap_SeparatedOverlappingAndTouching()
        {
            Assert.AreEqual(0.5f, GameplayMath.HorizontalGap(0f, 1f, 1.5f, 2f), 1e-5f);
            Assert.AreEqual(0.5f, GameplayMath.HorizontalGap(1.5f, 2f, 0f, 1f), 1e-5f);
            Assert.AreEqual(0f, GameplayMath.HorizontalGap(0f, 1f, 1f, 2f), 1e-5f);
            Assert.Less(GameplayMath.HorizontalGap(0f, 1f, 0.8f, 2f), 0f);
        }

        [Test]
        public void NearMiss_RequiresNonNegativeGapWithinDistance()
        {
            Assert.IsTrue(GameplayMath.IsNearMiss(0.2f, 0.3f));
            Assert.IsTrue(GameplayMath.IsNearMiss(0f, 0.3f));
            Assert.IsFalse(GameplayMath.IsNearMiss(0.4f, 0.3f), "adjacent-lane cruising is not a near miss");
            Assert.IsFalse(GameplayMath.IsNearMiss(-0.1f, 0.3f), "overlap means a collision, not a miss");
        }

        [Test]
        public void PickWeighted_RespectsWeightsAndZeroes()
        {
            float[] w = { 70f, 0f, 30f };
            Assert.AreEqual(0, GameplayMath.PickWeighted(w, 3, 0f));
            Assert.AreEqual(0, GameplayMath.PickWeighted(w, 3, 0.69f));
            Assert.AreEqual(2, GameplayMath.PickWeighted(w, 3, 0.71f));
            Assert.AreEqual(2, GameplayMath.PickWeighted(w, 3, 0.9999f));
            Assert.AreEqual(-1, GameplayMath.PickWeighted(new[] { 0f, 0f }, 2, 0.5f));
        }
    }
}
