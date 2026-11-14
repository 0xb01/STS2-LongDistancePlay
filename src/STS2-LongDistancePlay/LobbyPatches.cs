using System;
using System.Collections.Generic;
using CouchCoop.Mod.HostUi;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace STS2LongDistancePlay;

[HarmonyPatch(typeof(QrHostOptions), nameof(QrHostOptions.Build))]
public static class QrHostOptionsBuildPatch
{
    public static void Postfix(ref IReadOnlyList<QrHostOption> __result)
    {
        try
        {
            string? url = CloudflareTunnelService.CurrentUrl;
            if (string.IsNullOrWhiteSpace(url)) return;

            var uri = new Uri(url);
            // Create WAN Cloudflare Quick Tunnel option
            // Setting AbsoluteUri guarantees CouchCoop generates the clean HTTPS URL without port 13337
            var wanOption = new QrHostOption(
                uri.Host,
                443,
                QrHostOptionKind.Override,
                null,
                url.TrimEnd('/') + "/",
                Enabled: true
            );

            var list = new List<QrHostOption> { wanOption };
            foreach (var opt in __result)
            {
                if (opt.Host.Contains("trycloudflare.com", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                list.Add(opt);
            }
            __result = list;
        }
        catch (Exception ex)
        {
            ModLog.Error($"QrHostOptionsBuildPatch error: {ex}");
        }
    }
}

[HarmonyPatch]
public static class CouchCoopQrDialogOpenPatch
{
    public static System.Reflection.MethodBase? TargetMethod()
    {
        return AccessTools.Method("CouchCoop.Mod.HostUi.CouchCoopQrDialog:Open");
    }

    public static void Prefix()
    {
        try
        {
            if (CloudflareTunnelService.Status == TunnelStatus.Stopped)
            {
                ModLog.Info("CouchCoop QR dialog opened; starting Cloudflare tunnel...");
                CloudflareTunnelService.Start();
            }
        }
        catch (Exception ex)
        {
            ModLog.Error($"CouchCoopQrDialogOpenPatch error: {ex}");
        }
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), "_Ready")]
public static class CharacterSelectScreenReadyPatch
{
    public static void Postfix(NCharacterSelectScreen __instance)
    {
        try
        {
            MountOverlay(__instance);
        }
        catch (Exception ex)
        {
            ModLog.Error($"CharacterSelectScreen _Ready error: {ex}");
        }
    }

    internal static void MountOverlay(Control screen)
    {
        if (!ModConfig.Instance.EnableInGameOverlay) return;

        if (screen.FindChild(LongDistancePlayOverlay.NodeName, recursive: false) != null)
        {
            return;
        }

        var overlay = new LongDistancePlayOverlay();
        screen.AddChild(overlay);

        if (ModConfig.Instance.AutoStart && CloudflareTunnelService.Status == TunnelStatus.Stopped)
        {
            CloudflareTunnelService.Start();
        }
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), "InitializeMultiplayerAsHost")]
public static class CharacterSelectScreenMultiplayerHostPatch
{
    public static void Postfix(NCharacterSelectScreen __instance)
    {
        try
        {
            CharacterSelectScreenReadyPatch.MountOverlay(__instance);
        }
        catch (Exception ex)
        {
            ModLog.Error($"CharacterSelectScreen InitializeMultiplayerAsHost error: {ex}");
        }
    }
}

[HarmonyPatch(typeof(NMultiplayerLoadGameScreen), "_Ready")]
public static class MultiplayerLoadGameScreenReadyPatch
{
    public static void Postfix(NMultiplayerLoadGameScreen __instance)
    {
        try
        {
            CharacterSelectScreenReadyPatch.MountOverlay(__instance);
        }
        catch (Exception ex)
        {
            ModLog.Error($"MultiplayerLoadGameScreen _Ready error: {ex}");
        }
    }
}
