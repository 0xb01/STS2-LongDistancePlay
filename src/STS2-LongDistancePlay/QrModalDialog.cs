using System;
using Godot;

namespace STS2LongDistancePlay;

public partial class QrModalDialog : Control
{
    private readonly string _url;
    private ColorRect? _scrim;
    private PanelContainer? _panel;
    private TextureRect? _qrRect;
    private Label? _urlLabel;
    private Button? _copyButton;
    private Button? _closeButton;

    public QrModalDialog(string url)
    {
        _url = url;
        Name = "LongDistancePlayQrModal";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Ready()
    {
        base._Ready();

        // 1. Scrim (dark transparent background)
        _scrim = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.75f),
            MouseFilter = MouseFilterEnum.Stop
        };
        _scrim.SetAnchorsPreset(LayoutPreset.FullRect);
        _scrim.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                Close();
            }
        };
        AddChild(_scrim);

        // 2. Card Panel
        _panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.16f, 0.98f),
            BorderColor = new Color(0.85f, 0.65f, 0.2f, 1f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12
        };
        _panel.AddThemeStyleboxOverride("panel", style);
        _panel.CustomMinimumSize = new Vector2(460, 520);

        _panel.SetAnchorsPreset(LayoutPreset.Center);
        _panel.GrowHorizontal = GrowDirection.Both;
        _panel.GrowVertical = GrowDirection.Both;
        AddChild(_panel);

        // 3. Margin & VBox
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        _panel.AddChild(margin);

        var vbox = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        vbox.AddThemeConstantOverride("separation", 14);
        margin.AddChild(vbox);

        // 4. Title
        var title = new Label
        {
            Text = "🌐 Long Distance Play",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.8f, 0.3f, 1f));
        vbox.AddChild(title);

        // Subtitle
        var subtitle = new Label
        {
            Text = "Scan to play in mobile browser or share with friends:",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        subtitle.AddThemeFontSizeOverride("font_size", 14);
        subtitle.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.8f, 1f));
        vbox.AddChild(subtitle);

        // 5. QR Code Image
        _qrRect = new TextureRect
        {
            CustomMinimumSize = new Vector2(260, 260),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest
        };
        try
        {
            _qrRect.Texture = QrHelper.GenerateQrTexture(_url);
        }
        catch (Exception ex)
        {
            ModLog.Error($"QR render error: {ex.Message}");
        }
        vbox.AddChild(_qrRect);

        // 6. URL label
        _urlLabel = new Label
        {
            Text = _url,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _urlLabel.AddThemeFontSizeOverride("font_size", 13);
        _urlLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1.0f, 1f));
        vbox.AddChild(_urlLabel);

        // 7. Button row
        var hbox = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        hbox.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(hbox);

        _copyButton = new Button
        {
            Text = "📋 Copy Link",
            CustomMinimumSize = new Vector2(140, 40)
        };
        _copyButton.Pressed += () =>
        {
            try
            {
                DisplayServer.ClipboardSet(_url);
                _copyButton.Text = "✓ Copied!";
                GetTree().CreateTimer(2.0).Timeout += () =>
                {
                    if (IsInstanceValid(_copyButton))
                    {
                        _copyButton.Text = "📋 Copy Link";
                    }
                };
            }
            catch (Exception ex)
            {
                ModLog.Warn($"Clipboard error: {ex.Message}");
            }
        };
        hbox.AddChild(_copyButton);

        _closeButton = new Button
        {
            Text = "Close",
            CustomMinimumSize = new Vector2(100, 40)
        };
        _closeButton.Pressed += Close;
        hbox.AddChild(_closeButton);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Close()
    {
        QueueFree();
    }
}
