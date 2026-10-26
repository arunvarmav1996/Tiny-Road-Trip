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

    public class RunDistanceTests
    {
        [Test]
        public void TravelScalesWithSpeedMultiplier_AndBonusAdds()
        {
            var run = new RunDistance();
            run.AddTravel(1f, 100f / 3f, 1f);      // 1 km per 30 s
            Assert.AreEqual(33.333, run.TravelMeters, 1e-3);
            run.AddTravel(1f, 100f / 3f, 0.5f);    // Slow-Time
            Assert.AreEqual(50.0, run.TravelMeters, 1e-3);
            run.AddBonus(25);
            Assert.AreEqual(75.0, run.TotalMeters, 1e-3);
        }

        [Test]
        public void ThirtySecondsAtNormalSpeed_IsOneKilometre()
        {
            var run = new RunDistance();
            for (int i = 0; i < 1800; i++) run.AddTravel(1f / 60f, 100f / 3f, 1f);
            Assert.AreEqual(1000.0, run.TravelMeters, 0.01);
        }

        [Test]
        public void IgnoresInvalidInput()
        {
            var run = new RunDistance();
            run.AddTravel(-1f, 33f, 1f);
            run.AddTravel(1f, 33f, 0f);
            run.AddBonus(-10);
            Assert.AreEqual(0.0, run.TotalMeters);
        }

        [Test]
        public void Kilometres_FloorToTwoDecimals()
        {
            Assert.AreEqual("0.00", DistanceFormat.Kilometers(0));
            Assert.AreEqual("0.00", DistanceFormat.Kilometers(9.99));
            Assert.AreEqual("1.23", DistanceFormat.Kilometers(1239.9));
            Assert.AreEqual("12.50", DistanceFormat.Kilometers(12500));
            Assert.AreEqual(123, DistanceFormat.DisplayStep(1239.9));
        }

        [Test]
        public void BestScore_OnlyBeatenByLongerRun()
        {
            Assert.IsTrue(BestScore.Submit(500, 0).IsNewBest);
            BestResult same = BestScore.Submit(500, 500);
            Assert.IsFalse(same.IsNewBest);
            Assert.AreEqual(500, same.Best);
            BestResult worse = BestScore.Submit(300, 500);
            Assert.IsFalse(worse.IsNewBest);
            Assert.AreEqual(500, worse.Best);
            BestResult better = BestScore.Submit(800, 500);
            Assert.IsTrue(better.IsNewBest);
            Assert.AreEqual(800, better.Best);
            Assert.IsFalse(BestScore.Submit(0, 0).IsNewBest, "standing still is never a best");
        }
    }

    public class SwipeDetectorTests
    {
        [Test]
        public void FiresWhileDragging_OnceThresholdIsPassed()
        {
            var swipe = new SwipeDetector(60f);
            swipe.Begin(100f, 100f);
            Assert.AreEqual(SwipeDirection.None, swipe.Move(130f, 100f), "below threshold");
            Assert.AreEqual(SwipeDirection.Right, swipe.Move(165f, 100f), "fires mid-drag, no release needed");
            Assert.AreEqual(SwipeDirection.None, swipe.Move(400f, 100f), "only one move per touch");
            Assert.AreEqual(SwipeDirection.None, swipe.End(400f, 100f), "release after firing does nothing");
        }

        [Test]
        public void QuickFlick_IsCaughtOnRelease()
        {
            var swipe = new SwipeDetector(60f);
            swipe.Begin(0f, 0f);
            Assert.AreEqual(SwipeDirection.Down, swipe.End(5f, -90f));
        }

        [Test]
        public void Tap_DoesNothing()
        {
            var swipe = new SwipeDetector(60f);
            swipe.Begin(0f, 0f);
            Assert.AreEqual(SwipeDirection.None, swipe.Move(10f, 5f));
            Assert.AreEqual(SwipeDirection.None, swipe.End(12f, 6f));
        }

        [Test]
        public void DominantAxis_PicksDirection()
        {
            var swipe = new SwipeDetector(60f);
            swipe.Begin(0f, 0f); Assert.AreEqual(SwipeDirection.Left, swipe.End(-80f, 30f));
            swipe.Begin(0f, 0f); Assert.AreEqual(SwipeDirection.Up, swipe.End(20f, 70f));
        }

        [Test]
        public void Cancel_IgnoresRestOfTouch_UntilNextBegin()
        {
            var swipe = new SwipeDetector(60f);
            swipe.Cancel();
            Assert.AreEqual(SwipeDirection.None, swipe.Move(500f, 0f));
            Assert.AreEqual(SwipeDirection.None, swipe.End(500f, 0f), "the tap that pressed Resume is not a swipe");
            swipe.Begin(0f, 0f);
            Assert.AreEqual(SwipeDirection.Left, swipe.Move(-70f, 0f));
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
