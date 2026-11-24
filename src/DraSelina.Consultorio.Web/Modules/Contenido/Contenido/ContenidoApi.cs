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
    /// 2026-11-08) y el mapa incrustado del sitio (entrada 7.1), los dos de la misma búsqueda.
    /// </summary>
    public static readonly DatosDelConsultorio Consultorio = new(
        Lugar: Lugar,
        Direccion: Direccion,
        Ciudad: Ciudad,
        EnlaceDelMapa: BusquedaEnGoogleMaps(Lugar, Direccion, Ciudad),
        MapaIncrustado: MapaIncrustadoDeGoogleMaps(Lugar, Direccion, Ciudad));

    /// <summary>
    /// Nombre, especialidad, cédulas y certificación son los del mockup. Formación, afiliaciones y la semblanza los dio
    /// Selina (Gerardo, 2026-11-23): medicina en la UANL, la especialidad en la UASLP, y los colegios de los sellos de
    /// la sesión del Colegio de Ortopedia de San Luis Potosí. Los padecimientos son la lista completa por zona, sin
    /// pediatría; Selina va diciendo cuáles se quitan (Gerardo, 2026-11-23).
    /// </summary>
    public static readonly DatosDeLaDoctora Doctora = new(
        Nombre: "Dra. Selina Medina Castillo",
        Especialidad: "Traumatología y Ortopedia",
        CedulaProfesional: "12093135",
        CedulaDeEspecialidad: "14899218",
        ConsejoQueCertifica: "Consejo de Ortopedia",
        RegistroDeCertificacion: "24/8379/2",
        Formacion: "Medicina, Universidad Autónoma de Nuevo León · Traumatología y Ortopedia, Universidad Autónoma de San Luis Potosí",
        Semblanza:
            "Soy médica cirujana egresada de la Universidad Autónoma de Nuevo León y especialista en Traumatología y "
            + "Ortopedia por la Universidad Autónoma de San Luis Potosí. Estoy certificada por el Consejo de Ortopedia y "
            + "soy miembro del Colegio de Ortopedia de San Luis Potosí y de la Federación Mexicana de Colegios de Ortopedia "
            + "y Traumatología.\n\n"
            + "Atiendo lesiones y padecimientos de huesos, articulaciones, músculos y tendones, desde una fractura o una "
            + "lesión deportiva hasta el dolor que no se quita con los años. Mi objetivo es que entiendas qué tienes, qué "
            + "opciones existen y que vuelvas a moverte con confianza.",
        Afiliaciones:
            "Miembro del Colegio de Ortopedia de San Luis Potosí, A.C. y de la Federación Mexicana de Colegios de "
            + "Ortopedia y Traumatología (FEMECOT).",
        Padecimientos:
        [
            new("Rodilla",
            [
                "Lesión de ligamento cruzado (anterior o posterior)",
                "Lesión de menisco",
                "Condromalacia o dolor de la rótula",
                "Artrosis (desgaste) de rodilla",
                "Reemplazo de rodilla (prótesis)",
                "Tendinitis rotuliana",
                "Artroscopia de rodilla",
            ]),
            new("Hombro",
            [
                "Lesión del manguito rotador",
                "Hombro congelado (capsulitis adhesiva)",
                "Luxación o inestabilidad de hombro",
                "Pinzamiento o bursitis de hombro",
                "Artrosis de hombro y prótesis de hombro",
                "Artroscopia de hombro",
            ]),
            new("Cadera",
            [
                "Artrosis de cadera",
                "Reemplazo de cadera (prótesis)",
                "Pinzamiento de cadera (choque femoroacetabular)",
                "Lesión del labrum de cadera",
                "Bursitis trocantérica (dolor al costado de la cadera)",
                "Artroscopia de cadera",
            ]),
            new("Columna",
            [
                "Dolor de espalda baja (lumbalgia)",
                "Dolor de cuello (cervicalgia)",
                "Hernia de disco",
                "Ciática",
            ]),
            new("Mano, muñeca y codo",
            [
                "Síndrome del túnel carpiano",
                "Dedo en gatillo",
                "Tendinitis de De Quervain (dolor en la muñeca, del lado del pulgar)",
                "Codo de tenista o de golfista (epicondilitis)",
                "Quiste en la muñeca (ganglión)",
            ]),
            new("Pie y tobillo",
            [
                "Esguince de tobillo",
                "Inestabilidad de tobillo",
                "Fascitis plantar (dolor en el talón)",
                "Juanete (hallux valgus)",
                "Tendinitis o ruptura del tendón de Aquiles",
            ]),
            new("Fracturas y traumatismos",
            [
                "Fracturas en general (yeso o cirugía)",
                "Fractura de cadera en adultos mayores",
                "Luxaciones",
                "Seguimiento posquirúrgico de fracturas",
            ]),
            new("Lesiones deportivas",
            [
                "Esguinces y desgarros musculares",
                "Tendinitis por deporte",
                "Regreso al deporte después de una lesión",
            ]),
            new("Otros",
            [
                "Osteoporosis",
                "Artritis",
                "Infiltraciones (por ejemplo con esteroide o ácido hialurónico)",
                "Plasma rico en plaquetas",
            ]),
        ]);

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
        $"Responsable de tus datos: {Doctora.Nombre}.",
        "Para qué los usamos: para agendar y gestionar tu cita.",
        $"Dónde nos encuentras: {Consultorio.Lugar}, {Consultorio.Direccion}, {Consultorio.Ciudad}.",
    ];

    /// <summary>
    /// El de Selina, personal y de trabajo: el sitio lo muestra para WhatsApp y para llamar (Gerardo, 2026-11-23).
    /// </summary>
    public const string? WhatsAppDelConsultorio = "8126992831";

    public DatosDelConsultorio DatosDelConsultorio => Consultorio;

    public DatosDeLaDoctora DatosDeLaDoctora => Doctora;

    /// <summary>
    /// Una búsqueda de Google Maps (<c>https://www.google.com/maps/search/?api=1&amp;query=...</c>) con lugar, dirección
    /// y ciudad separados por coma y codificados en la URL.
    /// </summary>
    public static string BusquedaEnGoogleMaps(string lugar, string direccion, string ciudad) =>
        $"https://www.google.com/maps/search/?api=1&query={TextoDeBusqueda(lugar, direccion, ciudad)}";

    /// <summary>
    /// El mapa incrustado de Google Maps, sin clave (<c>https://www.google.com/maps?q=...&amp;output=embed</c>): la
    /// misma búsqueda que <see cref="BusquedaEnGoogleMaps"/>. Es el src del iframe de Ubicación y horarios.
    /// </summary>
    public static string MapaIncrustadoDeGoogleMaps(string lugar, string direccion, string ciudad) =>
        $"https://www.google.com/maps?q={TextoDeBusqueda(lugar, direccion, ciudad)}&output=embed";

    // Lugar, dirección y ciudad separados por coma y codificados en la URL: lo que buscan el enlace y el mapa.
    private static string TextoDeBusqueda(string lugar, string direccion, string ciudad) =>
        Uri.EscapeDataString($"{lugar}, {direccion}, {ciudad}");

    public string VersionDelAvisoDePrivacidad => AvisoDePrivacidad;

    public IReadOnlyList<string> TextoDelAvisoDePrivacidad => TextoDelAviso;

    public string? WhatsApp => WhatsAppDelConsultorio;
}
