using Godot;
using QRCoder;

namespace STS2LongDistancePlay;

public static class QrHelper
{
    public static ImageTexture GenerateQrTexture(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(data);
        byte[] pngBytes = qrCode.GetGraphic(10);

        var image = new Image();
        image.LoadPngFromBuffer(pngBytes);
        return ImageTexture.CreateFromImage(image);
    }
}
