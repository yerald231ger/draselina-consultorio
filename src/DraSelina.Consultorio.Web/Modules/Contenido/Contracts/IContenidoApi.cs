namespace DraSelina.Consultorio.Contenido.Contracts;

/// <summary>
/// Lo que Contenido ofrece a la interfaz y a otros módulos (AD-3, AD-15): los datos del consultorio, su WhatsApp y
/// la versión vigente del aviso de privacidad con su texto. Contenido no tiene esquema: su contenido se despliega con la aplicación (AD-2).
/// </summary>
public interface IContenidoApi
{
    /// <summary>Dónde se atiende: lo que se muestra al confirmar una Cita y lo que llevan los correos.</summary>
    DatosDelConsultorio DatosDelConsultorio { get; }

    /// <summary>
    /// La versión del aviso de privacidad vigente: la que acepta quien agenda una Cita y queda en su Consentimiento
    /// (AD-24).
    /// </summary>
    string VersionDelAvisoDePrivacidad { get; }

    /// <summary>
    /// El texto del aviso de privacidad vigente, en párrafos, de la versión <see cref="VersionDelAvisoDePrivacidad"/>.
    /// Lo muestra la página del Aviso de privacidad.
    /// </summary>
    IReadOnlyList<string> TextoDelAvisoDePrivacidad { get; }

    /// <summary>
    /// El WhatsApp del consultorio, a 10 dígitos; nulo mientras no se conozca. Sin él, las pantallas dicen
    /// "escríbenos por WhatsApp" sin número (Gerardo, 2026-11-02).
    /// </summary>
    string? WhatsApp { get; }
}

/// <summary>Los datos del consultorio.</summary>
/// <param name="Lugar">Hospital y consultorio: "Hospital Star Médica, consultorio 101".</param>
/// <param name="Direccion">Calle, número y colonia.</param>
/// <param name="Ciudad">Ciudad.</param>
/// <param name="EnlaceDelMapa">
/// Una búsqueda de Google Maps del lugar, la dirección y la ciudad: "Abrir en el mapa". Nulo si no hay enlace; quien lo
/// muestra lo omite entonces (Gerardo, 2026-11-08).
/// </param>
public sealed record DatosDelConsultorio(string Lugar, string Direccion, string Ciudad, string? EnlaceDelMapa = null);
