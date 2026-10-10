using System;
using MagicIsland.TimeSystem;
internal static class VerifyClock
{
    static int checks;
    static void Check(bool value) { checks++; if (!value) throw new Exception("Clock check " + checks); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentOutOfRangeException) { rejected = true; } Check(rejected); }
    public static int Main()
    {
        var clock = new ClockState(); Check(clock.Minute == 360);
        clock.Tick(30, 2, false); Check(clock.Minute == 375);
        clock.Tick(100, 1, true); Check(clock.Minute == 375);
        clock.Tick(0.5, 1, false); Check(clock.Minute == 375.5 && clock.DisplayMinute == 375);
        clock.Restore(1439.5); clock.Tick(999, 1, false); Check(clock.Minute == 1440 && clock.Finished);
        clock.Tick(999, 1, false); Check(clock.Minute == 1440);
        Reject(() => clock.Restore(double.NaN)); Reject(() => clock.Restore(-1)); Reject(() => clock.Restore(1441));
        Reject(() => clock.Tick(1, 0, false)); Reject(() => clock.Tick(double.PositiveInfinity, 1, false));
        Check(clock.Minute == 1440); clock.NextMorning(); Check(clock.Minute == 360 && !clock.Finished);
        Console.WriteLine("PASS: " + checks + " clock assertions"); return 0;
    }
}
