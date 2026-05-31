using QRCoder;

namespace Eventiq.EventService.Application.Utility;

public static class QrCodeGenerator
{
    public static string GenerateBase64Png(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using var code = new PngByteQRCode(data);
        return Convert.ToBase64String(code.GetGraphic(5));
    }
}
