using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace STS2LongDistancePlay;

public sealed class ModConfig
{
    private static readonly string ConfigPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".",
        "config.json"
    );

    public bool AutoStart { get; set; } = true;
    public bool AutoCopyUrl { get; set; } = true;
    public int TargetPort { get; set; } = 13337;
    public string TunnelToken { get; set; } = "";
    public bool EnableInGameOverlay { get; set; } = true;

    private static ModConfig? _instance;
    public static ModConfig Instance => _instance ??= Load();

    public static ModConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<ModConfig>(json);
                if (loaded != null)
                {
                    _instance = loaded;
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[STS2-LongDistancePlay] Failed to read config: {ex.Message}");
        }

        var defaultConfig = new ModConfig();
        defaultConfig.Save();
        _instance = defaultConfig;
        return defaultConfig;
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[STS2-LongDistancePlay] Failed to save config: {ex.Message}");
        }
    }
}
