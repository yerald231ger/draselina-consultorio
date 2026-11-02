using DraSelina.Consultorio.Contenido.Contracts;

namespace DraSelina.Consultorio.Contenido;

/// <summary>
/// <see cref="IContenidoApi"/> con el contenido que se despliega con la aplicación. Sin reglas de negocio (E-5).
/// </summary>
public sealed class ContenidoApi : IContenidoApi
{
    /// <summary>Los del mockup, confirmados como los correctos (Gerardo, 2026-11-02).</summary>
    public static readonly DatosDelConsultorio Consultorio = new(
        Lugar: "Hospital Star Médica, consultorio 101",
        Direccion: "Mariano Arista No. 735, Col. Barrio de Tequisquiapan",
        Ciudad: "San Luis Potosí");

    /// <summary>
    /// Borrador hasta que el abogado entregue el texto revisado; entonces cambia a su fecha de publicación, por
    /// ejemplo <c>2027-01</c> (Gerardo, 2026-11-02).
    /// </summary>
    public const string AvisoDePrivacidad = "2026-11-borrador";

    public DatosDelConsultorio DatosDelConsultorio => Consultorio;

    public string VersionDelAvisoDePrivacidad => AvisoDePrivacidad;
}
