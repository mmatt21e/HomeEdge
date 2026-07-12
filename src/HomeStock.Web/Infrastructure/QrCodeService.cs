using QRCoder;

namespace HomeStock.Web.Infrastructure;

/// <summary>Generates QR codes as inline SVG (crisp at any size, no image encoding needed).</summary>
public class QrCodeService
{
    /// <summary>Returns an SVG document string encoding <paramref name="payload"/>.</summary>
    public string SvgFor(string payload, int pixelsPerModule = 5)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var svg = new SvgQRCode(data);
        return svg.GetGraphic(pixelsPerModule);
    }

    /// <summary>Returns a data: URI wrapping the SVG, suitable for an &lt;img src&gt; or download.</summary>
    public string SvgDataUri(string payload, int pixelsPerModule = 5)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(SvgFor(payload, pixelsPerModule));
        return "data:image/svg+xml;base64," + Convert.ToBase64String(bytes);
    }
}
