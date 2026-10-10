using System;
using MagicIsland.Tools;
internal static class VerifyToolGate
{
    static int checks;
    static void Check(bool value) { checks++; if (!value) throw new Exception("Tool gate check " + checks); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentOutOfRangeException) { rejected = true; } Check(rejected); }
    public static int Main()
    {
        var gate = new ToolActionGate(); Check(!gate.Busy);
        Check(gate.TryBegin(0.55) && gate.Busy); Check(!gate.TryBegin(1) && gate.Remaining == 0.55);
        gate.Tick(10, true); Check(gate.Remaining == 0.55);
        gate.Tick(0.2, false); Check(gate.Busy && Math.Abs(gate.Remaining - 0.35) < 0.0001);
        gate.Tick(0.4, false); Check(!gate.Busy && gate.Remaining == 0);
        Check(gate.TryBegin(1)); gate.Reset(); Check(!gate.Busy);
        Reject(() => gate.TryBegin(0)); Reject(() => gate.TryBegin(double.NaN)); Reject(() => gate.TryBegin(double.PositiveInfinity));
        Reject(() => gate.Tick(-1, false)); Reject(() => gate.Tick(double.NaN, true));
        Check(!gate.Busy); Console.WriteLine("PASS: " + checks + " tool gate assertions"); return 0;
    }
}
