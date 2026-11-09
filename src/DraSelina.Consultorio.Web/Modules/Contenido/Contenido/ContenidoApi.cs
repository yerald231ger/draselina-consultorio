using DraSelina.Consultorio.Contenido.Contracts;

namespace DraSelina.Consultorio.Contenido;

/// <summary>
/// <see cref="IContenidoApi"/> con el contenido que se despliega con la aplicación. Sin reglas de negocio (E-5).
/// </summary>
public sealed class ContenidoApi : IContenidoApi
{
    private const string Lugar = "Hospital Star Médica, consultorio 101";
    private const string Direccion = "Mariano Arista No. 735, Col. Barrio de Tequisquiapan";
    private const string Ciudad = "San Luis Potosí";

    /// <summary>
    /// Los del mockup, confirmados como los correctos (Gerardo, 2026-11-02), con su enlace al mapa (Gerardo,
    /// 2026-11-08).
    /// </summary>
    public static readonly DatosDelConsultorio Consultorio = new(
        Lugar: Lugar,
        Direccion: Direccion,
        Ciudad: Ciudad,
        EnlaceDelMapa: BusquedaEnGoogleMaps(Lugar, Direccion, Ciudad));

    /// <summary>
    /// Borrador hasta que el abogado entregue el texto revisado; entonces cambia a su fecha de publicación, por
    /// ejemplo <c>2027-01</c> (Gerardo, 2026-11-02).
    /// </summary>
    public const string AvisoDePrivacidad = "2026-11-borrador";

    /// <summary>
    /// Provisional y breve: dice que el aviso está en revisión y nombra al responsable, la finalidad y la dirección del
    /// consultorio. No es un texto legal; se reemplaza por el revisado, con su versión, antes del primer Paciente
    /// (Gerardo, 2026-11-02).
    /// </summary>
    public static readonly IReadOnlyList<string> TextoDelAviso =
    [
        "Nuestro aviso de privacidad está en revisión. Mientras se publica la versión completa, esto es lo esencial.",
        "Responsable de tus datos: Dra. Selina Medina Castillo.",
        "Para qué los usamos: para agendar y gestionar tu cita.",
        $"Dónde nos encuentras: {Consultorio.Lugar}, {Consultorio.Direccion}, {Consultorio.Ciudad}.",
    ];

    /// <summary>Todavía no se conoce (Gerardo, 2026-11-02). Al tenerlo, basta con poner aquí sus 10 dígitos.</summary>
    public const string? WhatsAppDelConsultorio = null;

    public DatosDelConsultorio DatosDelConsultorio => Consultorio;

    /// <summary>
    /// Una búsqueda de Google Maps (<c>https://www.google.com/maps/search/?api=1&amp;query=...</c>) con lugar, dirección
    /// y ciudad separados por coma y codificados en la URL.
    /// </summary>
    public static string BusquedaEnGoogleMaps(string lugar, string direccion, string ciudad) =>
        $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString($"{lugar}, {direccion}, {ciudad}")}";

    public string VersionDelAvisoDePrivacidad => AvisoDePrivacidad;

    public IReadOnlyList<string> TextoDelAvisoDePrivacidad => TextoDelAviso;

    public string? WhatsApp => WhatsAppDelConsultorio;
}
