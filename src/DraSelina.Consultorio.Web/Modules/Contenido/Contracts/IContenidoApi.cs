namespace DraSelina.Consultorio.Contenido.Contracts;

/// <summary>
/// Lo que Contenido ofrece a la interfaz y a otros módulos (AD-3, AD-15): los datos del consultorio y la versión
/// vigente del aviso de privacidad. Contenido no tiene esquema: su contenido se despliega con la aplicación (AD-2).
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
}

/// <summary>Los datos del consultorio.</summary>
/// <param name="Lugar">Hospital y consultorio: "Hospital Star Médica, consultorio 101".</param>
/// <param name="Direccion">Calle, número y colonia.</param>
/// <param name="Ciudad">Ciudad.</param>
public sealed record DatosDelConsultorio(string Lugar, string Direccion, string Ciudad);
