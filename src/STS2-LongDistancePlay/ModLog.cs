using System;
using MegaCrit.Sts2.Core.Logging;

namespace STS2LongDistancePlay;

public static class ModLog
{
    public static void Info(string msg)
    {
        try { Log.Info($"[STS2-LongDistancePlay] {msg}"); } catch { }
        Console.WriteLine($"[STS2-LongDistancePlay] {msg}");
    }

    public static void Warn(string msg)
    {
        try { Log.Warn($"[STS2-LongDistancePlay] {msg}"); } catch { }
        Console.WriteLine($"[STS2-LongDistancePlay] WARN: {msg}");
    }

    public static void Error(string msg)
    {
        try { Log.Error($"[STS2-LongDistancePlay] {msg}"); } catch { }
        Console.WriteLine($"[STS2-LongDistancePlay] ERROR: {msg}");
    }
}
