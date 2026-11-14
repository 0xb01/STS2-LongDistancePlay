using System;
using Godot;

namespace STS2LongDistancePlay;

public partial class LongDistancePlayOverlay : Control
{
    public const string NodeName = "LongDistancePlayOverlay";

    private Label? _statusLabel;
    private Label? _urlLabel;
    private Button? _copyButton;
    private Button? _qrButton;
    private Button? _restartButton;

    public LongDistancePlayOverlay()
    {
        Name = NodeName;
        CustomMinimumSize = new Vector2(340, 110);
        MouseFilter = MouseFilterEnum.Pass;
    }

    public override void _Ready()
    {
        base._Ready();

        // Position in top-right area of screen
        SetAnchorsPreset(LayoutPreset.TopRight);
        OffsetLeft = -360;
        OffsetTop = 20;
        OffsetRight = -20;
        OffsetBottom = 130;

        // Card Panel styling
        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.11f, 0.15f, 0.92f),
            BorderColor = new Color(0.85f, 0.65f, 0.2f, 0.9f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
        panel.AddThemeStyleboxOverride("panel", style);
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vbox);

        // Header: Title + Status
        var headerHbox = new HBoxContainer();
        vbox.AddChild(headerHbox);

        var title = new Label
        {
            Text = "🌐 Long Distance Play",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.35f, 1f));
        headerHbox.AddChild(title);

        _statusLabel = new Label
        {
            Text = "🟡 Connecting...",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 12);
        headerHbox.AddChild(_statusLabel);

        // URL display
        _urlLabel = new Label
        {
            Text = "Generating tunnel link...",
            ClipText = true,
            AutowrapMode = TextServer.AutowrapMode.Off
        };
        _urlLabel.AddThemeFontSizeOverride("font_size", 12);
        _urlLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1f, 1f));
        vbox.AddChild(_urlLabel);

        // Action Buttons
        var buttonsHbox = new HBoxContainer();
        buttonsHbox.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(buttonsHbox);

        _copyButton = new Button
        {
            Text = "📋 Copy Link",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28)
        };
        _copyButton.AddThemeFontSizeOverride("font_size", 12);
        _copyButton.Pressed += OnCopyPressed;
        buttonsHbox.AddChild(_copyButton);

        _qrButton = new Button
        {
            Text = "📱 QR",
            CustomMinimumSize = new Vector2(56, 28)
        };
        _qrButton.AddThemeFontSizeOverride("font_size", 12);
        _qrButton.Pressed += OnQrPressed;
        buttonsHbox.AddChild(_qrButton);

        _restartButton = new Button
        {
            Text = "🔄",
            CustomMinimumSize = new Vector2(36, 28),
            TooltipText = "Restart Tunnel"
        };
        _restartButton.AddThemeFontSizeOverride("font_size", 12);
        _restartButton.Pressed += () => CloudflareTunnelService.Restart();
        buttonsHbox.AddChild(_restartButton);

        // Subscribe to tunnel events
        CloudflareTunnelService.StatusChanged += OnTunnelStatusChanged;
        CloudflareTunnelService.UrlAvailable += OnTunnelUrlAvailable;

        RefreshUi(CloudflareTunnelService.Status, CloudflareTunnelService.CurrentUrl);
    }

    public override void _ExitTree()
    {
        CloudflareTunnelService.StatusChanged -= OnTunnelStatusChanged;
        CloudflareTunnelService.UrlAvailable -= OnTunnelUrlAvailable;
        base._ExitTree();
    }

    private void OnTunnelStatusChanged(TunnelStatus status, string? detail)
    {
        Callable.From(() => RefreshUi(status, CloudflareTunnelService.CurrentUrl)).CallDeferred();
    }

    private void OnTunnelUrlAvailable(string url)
    {
        Callable.From(() => RefreshUi(TunnelStatus.Running, url)).CallDeferred();
    }

    private void RefreshUi(TunnelStatus status, string? url)
    {
        if (!IsInstanceValid(this) || _statusLabel == null || _urlLabel == null || _copyButton == null || _qrButton == null)
        {
            return;
        }

        switch (status)
        {
            case TunnelStatus.Running:
                _statusLabel.Text = "🟢 Live";
                _statusLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.9f, 0.3f, 1f));
                _urlLabel.Text = url ?? "";
                _copyButton.Disabled = false;
                _qrButton.Disabled = false;
                break;

            case TunnelStatus.Starting:
                _statusLabel.Text = "🟡 Connecting...";
                _statusLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.2f, 1f));
                _urlLabel.Text = "Generating tunnel link...";
                _copyButton.Disabled = true;
                _qrButton.Disabled = true;
                break;

            case TunnelStatus.Error:
                _statusLabel.Text = "🔴 Error";
                _statusLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.3f, 0.3f, 1f));
                _urlLabel.Text = CloudflareTunnelService.LastError ?? "Tunnel error";
                _copyButton.Disabled = true;
                _qrButton.Disabled = true;
                break;

            case TunnelStatus.Stopped:
            default:
                _statusLabel.Text = "⚪ Stopped";
                _statusLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f, 1f));
                _urlLabel.Text = "Tunnel offline";
                _copyButton.Disabled = true;
                _qrButton.Disabled = true;
                break;
        }
    }

    private void OnCopyPressed()
    {
        if (CloudflareTunnelService.CurrentUrl is { Length: > 0 } url)
        {
            try
            {
                DisplayServer.ClipboardSet(url);
                if (_copyButton != null)
                {
                    _copyButton.Text = "✓ Copied!";
                    GetTree().CreateTimer(2.0).Timeout += () =>
                    {
                        if (IsInstanceValid(_copyButton))
                        {
                            _copyButton.Text = "📋 Copy Link";
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn($"Clipboard error: {ex.Message}");
            }
        }
    }

    private void OnQrPressed()
    {
        if (CloudflareTunnelService.CurrentUrl is { Length: > 0 } url)
        {
            // Mount full-screen QR modal dialog onto root viewport / screen
            var dialog = new QrModalDialog(url);
            var parent = GetParent() ?? GetTree().Root;
            parent.AddChild(dialog);
        }
    }
}
