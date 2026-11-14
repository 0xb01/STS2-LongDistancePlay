using System;
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

        // 3. Pre-kill any orphaned cloudflared processes and setup Job Object
        CloudflareTunnelService.KillOrphanProcesses();
        JobObjectManager.Initialize();

        // 4. Ensure cleanup on process exit
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            ModLog.Info("Process exiting, stopping tunnel...");
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
}
