using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CouchCoop.Mod;
using Godot;

namespace STS2LongDistancePlay;

public enum TunnelStatus
{
    Stopped,
    Starting,
    Running,
    Error
}

public static class CloudflareTunnelService
{
    private static readonly Regex TryCloudflareRegex = new(
        @"https://[a-zA-Z0-9-]+\.trycloudflare\.com",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private static readonly object SyncLock = new();
    private static Process? _process;

    public static TunnelStatus Status { get; private set; } = TunnelStatus.Stopped;
    public static string? CurrentUrl { get; private set; }
    public static string? LastError { get; private set; }

    public static event Action<TunnelStatus, string?>? StatusChanged;
    public static event Action<string>? UrlAvailable;

    public static string ModDirectory =>
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;

    public static string LocalBinaryPath =>
        Path.Combine(ModDirectory, "cloudflared.exe");

    public static bool IsBinaryAvailable() =>
        File.Exists(LocalBinaryPath) || FindOnPath("cloudflared.exe") != null;

    private static string? FindOnPath(string fileName)
    {
        var pathEnv = System.Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv)) return null;

        foreach (var path in pathEnv.Split(Path.PathSeparator))
        {
            try
            {
                var full = Path.Combine(path.Trim(), fileName);
                if (File.Exists(full)) return full;
            }
            catch { }
        }
        return null;
    }

    public static void KillOrphanProcesses()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("cloudflared"))
            {
                try
                {
                    ModLog.Info($"Cleaning up existing cloudflared process (PID: {proc.Id})");
                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(1000);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Error during orphan process cleanup: {ex.Message}");
        }
    }

    public static async Task<bool> EnsureBinaryDownloadedAsync(Action<string>? log = null)
    {
        if (IsBinaryAvailable()) return true;

        log ??= msg => ModLog.Info(msg);
        log("cloudflared.exe not found locally. Downloading latest release from Cloudflare...");

        const string downloadUrl = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";
        var tempFile = LocalBinaryPath + ".tmp";

        try
        {
            using var client = new System.Net.Http.HttpClient();
            using var response = await client.GetAsync(downloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (var fileStream = new System.IO.FileStream(tempFile, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None))
            {
                await stream.CopyToAsync(fileStream).ConfigureAwait(false);
            }

            if (File.Exists(LocalBinaryPath))
            {
                File.Delete(LocalBinaryPath);
            }
            File.Move(tempFile, LocalBinaryPath);
            log($"Successfully downloaded cloudflared.exe to {LocalBinaryPath}");
            return true;
        }
        catch (Exception ex)
        {
            log($"Failed to download cloudflared.exe: {ex.Message}");
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
            return false;
        }
    }

    public static void Start()
    {
        lock (SyncLock)
        {
            if (Status == TunnelStatus.Starting || Status == TunnelStatus.Running)
            {
                return;
            }

            SetStatus(TunnelStatus.Starting, "Starting tunnel...");
            CurrentUrl = null;
            LastError = null;
        }

        Task.Run(async () =>
        {
            try
            {
                if (!IsBinaryAvailable())
                {
                    bool downloaded = await EnsureBinaryDownloadedAsync().ConfigureAwait(false);
                    if (!downloaded)
                    {
                        SetStatus(TunnelStatus.Error, "Missing cloudflared.exe and download failed.");
                        return;
                    }
                }

                string binaryPath = File.Exists(LocalBinaryPath) ? LocalBinaryPath : (FindOnPath("cloudflared.exe") ?? LocalBinaryPath);
                var config = ModConfig.Instance;
                int port = config.TargetPort;

                string arguments;
                if (!string.IsNullOrWhiteSpace(config.TunnelToken))
                {
                    arguments = $"tunnel run --token {config.TunnelToken.Trim()}";
                }
                else
                {
                    arguments = $"tunnel --url http://127.0.0.1:{port} --no-autoupdate";
                }

                ModLog.Info($"Launching tunnel: {binaryPath} {arguments}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = binaryPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                lock (SyncLock)
                {
                    _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                    _process.OutputDataReceived += OnProcessOutput;
                    _process.ErrorDataReceived += OnProcessOutput;
                    _process.Exited += OnProcessExited;

                    if (!_process.Start())
                    {
                        SetStatus(TunnelStatus.Error, "Failed to start cloudflared process.");
                        return;
                    }

                    // Bind to Win32 Job Object so process dies if game exits or crashes
                    JobObjectManager.AddProcess(_process);

                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"Tunnel startup exception: {ex}");
                SetStatus(TunnelStatus.Error, ex.Message);
            }
        });
    }

    private static void OnProcessOutput(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data)) return;

        string line = e.Data.Trim();
        ModLog.Info($"[cloudflared] {line}");

        if (CurrentUrl == null)
        {
            var match = TryCloudflareRegex.Match(line);
            if (match.Success)
            {
                string url = match.Value;
                CurrentUrl = url;
                SetStatus(TunnelStatus.Running, url);

                ModLog.Info($">>> CLOUDFLARE WAN TUNNEL READY: {url} <<<");

                if (ModConfig.Instance.AutoCopyUrl)
                {
                    Callable.From(() =>
                    {
                        try
                        {
                            DisplayServer.ClipboardSet(url);
                            ModLog.Info("Tunnel URL copied to clipboard.");
                        }
                        catch (Exception ex)
                        {
                            ModLog.Warn($"Clipboard copy error: {ex.Message}");
                        }
                    }).CallDeferred();
                }

                // Notify listeners
                UrlAvailable?.Invoke(url);

                // Prompt CouchCoop host UI to update its panels
                Callable.From(() =>
                {
                    try
                    {
                        CouchCoopMod.RefreshQrHostPanelLayout();
                    }
                    catch { }
                }).CallDeferred();
            }
        }
    }

    private static void OnProcessExited(object? sender, EventArgs e)
    {
        lock (SyncLock)
        {
            int? exitCode = null;
            try { exitCode = _process?.ExitCode; } catch { }

            ModLog.Info($"cloudflared process exited. Code: {exitCode}");
            _process = null;

            if (Status != TunnelStatus.Stopped)
            {
                SetStatus(TunnelStatus.Stopped, $"Exited with code {exitCode}");
            }
        }
    }

    public static void Stop()
    {
        lock (SyncLock)
        {
            if (_process != null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Warn($"Error killing cloudflared: {ex.Message}");
                }
                finally
                {
                    _process.Dispose();
                    _process = null;
                }
            }

            CurrentUrl = null;
            SetStatus(TunnelStatus.Stopped, "Tunnel stopped.");
        }
    }

    public static void Restart()
    {
        Stop();
        Task.Delay(500).ContinueWith(_ => Start());
    }

    private static void SetStatus(TunnelStatus newStatus, string? detail = null)
    {
        Status = newStatus;
        if (newStatus == TunnelStatus.Error)
        {
            LastError = detail;
        }

        StatusChanged?.Invoke(newStatus, detail);
    }
}
