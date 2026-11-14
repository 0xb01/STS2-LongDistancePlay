using System;
using CouchCoop.Mod;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace STS2LongDistancePlay;

[ModInitializer("Init")]
public static class ModEntry
{
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        // 0. Headless Client check:
        // CouchCoop launches child SlayTheSpire2.exe processes for each connected browser seat.
        // A headless seat must NEVER initialize the host tunnel, kill processes, or attach overlays.
        if (IsHeadlessClient())
        {
            ModLog.Info("Headless client seat detected; skipping host tunnel services.");
            return;
        }

        ModLog.Info("Initializing Long Distance Play (Cloudflare WAN)...");

        // 1. Configure CouchCoop environment variables:
        // Disable origin and local network checks so remote web browsers and WebSockets are allowed
        Environment.SetEnvironmentVariable("COUCHCOOP_WS_ORIGIN_CHECK", "0");
        Environment.SetEnvironmentVariable("COUCHCOOP_SECURE_ORIGIN", "0");

        // Clear any lingering ADVERTISED_HOST override so CouchCoop does not append :13337
        Environment.SetEnvironmentVariable("COUCHCOOP_ADVERTISED_HOST", null);

        // 2. Load configuration
        var config = ModConfig.Instance;
        ModLog.Info($"Config loaded (AutoStart={config.AutoStart}, Port={config.TargetPort})");

        // 3. Pre-kill any orphaned cloudflared processes from prior runs and setup Job Object
        CloudflareTunnelService.KillOrphanProcesses();
        JobObjectManager.Initialize();

        // 4. Ensure cleanup on process exit
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            ModLog.Info("Host process exiting, stopping tunnel...");
            CloudflareTunnelService.Stop();
        };

        // 5. Apply Harmony Patches
        try
        {
            var harmony = new Harmony("com.sts2.longdistanceplay");
            harmony.PatchAll();
            ModLog.Info("Harmony patches applied successfully.");
        }
        catch (Exception ex)
        {
            ModLog.Error($"Harmony patch failed: {ex}");
        }

        // 6. Pre-check / prepare cloudflared binary
        if (!CloudflareTunnelService.IsBinaryAvailable())
        {
            ModLog.Info("Binary not yet present, queuing background download.");
            _ = CloudflareTunnelService.EnsureBinaryDownloadedAsync();
        }
        else
        {
            ModLog.Info("cloudflared.exe verified.");
        }
    }

    private static bool IsHeadlessClient()
    {
        if (Environment.GetEnvironmentVariable("COUCHCOOP_HEADLESS_CLIENT") == "1") return true;
        try
        {
            if (CouchCoopMod.IsHeadlessClient) return true;
        }
        catch { }

        string cmd = Environment.CommandLine;
        if (cmd.Contains("headless", StringComparison.OrdinalIgnoreCase) ||
            cmd.Contains("COUCHCOOP_HEADLESS", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }
}
