using System.Globalization;
using System.Security.Cryptography;
using DraSelina.Consultorio.Kernel;
using Microsoft.AspNetCore.DataProtection;

namespace DraSelina.Consultorio.UI.Pages.Reserva;

/// <summary>
/// La verificación anti-bots de Tus datos (FR-7, CAP-5), sin scripts ni servicios de terceros (Gerardo, 2026-11-04):
/// un campo trampa que una persona no ve y un tiempo mínimo entre pintar el formulario y enviarlo. El instante en que
/// se pintó viaja en un campo oculto, protegido con Data Protection: alterado o ausente, no pasa.
/// </summary>
/// <remarks>
/// Vive en la interfaz, no en el dominio ni en el handler (AD-12): protege el formulario, no es una regla de la Cita.
/// La página la comprueba antes de llamar al handler; el motivo del rechazo no se dice ni se registra (AD-23).
/// </remarks>
public sealed class VerificacionAntiBots(IDataProtectionProvider proveedor, IClock reloj)
{
    /// <summary>El nombre del campo oculto con el instante protegido en que se pintó el formulario.</summary>
    public const string CampoDePintado = "Pintado";

    /// <summary>El nombre del campo trampa: un nombre que un bot llena y una persona nunca ve.</summary>
    public const string CampoTrampa = "SitioWeb";

    /// <summary>Lo menos que tarda una persona entre ver el formulario y enviarlo.</summary>
    public static readonly TimeSpan TiempoMinimo = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Lo más que puede tener el instante: nadie tarda 2 horas en llenar Tus datos, y así un bot no reutiliza un
    /// mismo valor por días. El reintento tras "horario tomado" conserva el instante y cabe de sobra en ese tiempo.
    /// </summary>
    public static readonly TimeSpan TiempoMaximo = TimeSpan.FromHours(2);

    private readonly IDataProtector _protector = proveedor.CreateProtector("Reserva.TusDatos.Pintado");

    /// <summary>El instante actual, protegido: el valor del campo <see cref="CampoDePintado"/> al pintar el formulario.</summary>
    public string Pintar() => _protector.Protect(reloj.UtcNow.UtcTicks.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Si el envío parece de una persona: el campo trampa vacío y el formulario pintado, por este sitio, hace al
    /// menos <see cref="TiempoMinimo"/> y como mucho <see cref="TiempoMaximo"/>.
    /// </summary>
    public bool Pasa(string? pintado, string? trampa) =>
        string.IsNullOrEmpty(trampa)
        && EsLegitimo(pintado)
        && reloj.UtcNow - PintadoEn(pintado)!.Value >= TiempoMinimo;

    /// <summary>
    /// Si el campo oculto trae un instante legítimo y no vencido (<see cref="TiempoMaximo"/>), aunque todavía no pase
    /// el tiempo mínimo. Si no, la página pinta uno nuevo.
    /// </summary>
    public bool EsLegitimo(string? pintado) =>
        PintadoEn(pintado) is { } instante && reloj.UtcNow - instante <= TiempoMaximo;

    private DateTimeOffset? PintadoEn(string? pintado)
    {
        if (string.IsNullOrEmpty(pintado))
        {
            return null;
        }

        try
        {
            return long.TryParse(_protector.Unprotect(pintado), NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                   && ticks >= DateTimeOffset.MinValue.UtcTicks && ticks <= DateTimeOffset.MaxValue.UtcTicks
                ? new DateTimeOffset(ticks, TimeSpan.Zero)
                : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
