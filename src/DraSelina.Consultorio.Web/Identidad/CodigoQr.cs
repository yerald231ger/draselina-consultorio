using QRCoder;

namespace DraSelina.Consultorio.Web.Identidad;

/// <summary>
/// El QR del enrolamiento como SVG, generado en el servidor con QRCoder (MIT): la página no lleva scripts (Gerardo,
/// 2026-11-09). Lo que lleva adentro es la clave del segundo factor: nunca va a registros.
/// </summary>
public static class CodigoQr
{
    /// <summary>El SVG del QR de <paramref name="texto"/>, con su zona de silencio, escalable por su viewBox.</summary>
    public static string Svg(string texto)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(datos).GetGraphic(
            pixelsPerModule: 5,
            darkColorHex: "#3A1F29",
            lightColorHex: "#FFFFFF",
            drawQuietZones: true,
            sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}
