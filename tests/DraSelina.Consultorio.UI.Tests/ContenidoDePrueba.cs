using DraSelina.Consultorio.Contenido;
using DraSelina.Consultorio.Contenido.Contracts;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// <see cref="ContenidoApi"/> con el WhatsApp, la doctora o el consultorio que pida la prueba; lo demás, el contenido
/// real.
/// Lo comparten las pruebas del sitio.
/// </summary>
internal sealed class ContenidoDePrueba : IContenidoApi
{
    private readonly ContenidoApi _real = new();

    /// <summary>El WhatsApp que ve la página; nulo, el de Contenido.</summary>
    public string? WhatsAppDePrueba { get; set; }

    /// <summary>La página no ve ningún WhatsApp, aunque Contenido tenga uno.</summary>
    public bool SinWhatsApp { get; set; }

    /// <summary>Los datos de la doctora que ve la página; nulos, los de Contenido.</summary>
    public DatosDeLaDoctora? DoctoraDePrueba { get; set; }

    /// <summary>Los datos del consultorio que ve la página; nulos, los de Contenido.</summary>
    public DatosDelConsultorio? ConsultorioDePrueba { get; set; }

    public DatosDelConsultorio DatosDelConsultorio => ConsultorioDePrueba ?? _real.DatosDelConsultorio;

    public string VersionDelAvisoDePrivacidad => _real.VersionDelAvisoDePrivacidad;

    public IReadOnlyList<SeccionDelAviso> TextoDelAvisoDePrivacidad => _real.TextoDelAvisoDePrivacidad;

    public IReadOnlyList<FragmentoDelAviso> AvisoDePrivacidadSimplificado => _real.AvisoDePrivacidadSimplificado;

    public string? WhatsApp => SinWhatsApp ? null : WhatsAppDePrueba ?? _real.WhatsApp;

    public DatosDeLaDoctora DatosDeLaDoctora => DoctoraDePrueba ?? _real.DatosDeLaDoctora;
}
