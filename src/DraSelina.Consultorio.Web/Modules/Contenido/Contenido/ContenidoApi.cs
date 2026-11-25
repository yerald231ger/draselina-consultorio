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
    /// 2026-11-08) y el mapa incrustado del sitio (entrada 7.1), los dos de la misma búsqueda. Sin código postal todavía.
    /// </summary>
    public static readonly DatosDelConsultorio Consultorio = new(
        Lugar: Lugar,
        Direccion: Direccion,
        Ciudad: Ciudad,
        EnlaceDelMapa: BusquedaEnGoogleMaps(Lugar, Direccion, Ciudad),
        MapaIncrustado: MapaIncrustadoDeGoogleMaps(Lugar, Direccion, Ciudad),
        CodigoPostal: null); // Aún no se tiene: el aviso lo marca como pendiente (Gerardo, 2026-11-24).

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
    /// La versión del texto que aprobó Selina (Gerardo, 2026-11-24). Cambia cada vez que cambia el texto; las Citas ya
    /// agendadas conservan la que aceptaron (AD-24).
    /// </summary>
    public const string AvisoDePrivacidad = "2026-11-24-borrador";

    /// <summary>
    /// El correo para las solicitudes de derechos sobre los datos. Aún no existe: el aviso lo marca como pendiente
    /// (Gerardo, 2026-11-24).
    /// </summary>
    public const string? CorreoDePrivacidadDelConsultorio = null;

    /// <summary>El que dice "Qué falta" la marca de pendiente del correo.</summary>
    public const string CorreoPendiente = "correo para las solicitudes";

    /// <summary>El que dice "Qué falta" la marca de pendiente del código postal.</summary>
    public const string CodigoPostalPendiente = "código postal";

    /// <summary>La página del aviso completo.</summary>
    public const string RutaDelAviso = "/aviso-de-privacidad";

    /// <summary>
    /// El aviso integral que aprobó Selina (Gerardo, 2026-11-24), literal: la pestaña "Borrador del aviso de privacidad"
    /// del documento del aviso, copiada en <c>aviso-de-privacidad-aprobado.md</c>. El correo y el código postal van como
    /// datos que pueden faltar; la línea de la versión la pinta la página con <see cref="AvisoDePrivacidad"/>.
    /// </summary>
    public static readonly IReadOnlyList<SeccionDelAviso> AvisoIntegral =
    [
        new("Quién es responsable de tus datos",
        [
            Parrafo(
                new TextoDelAviso(
                    "La Dra. Selina Medina Castillo, médica especialista en Traumatología y Ortopedia, es responsable del "
                    + "tratamiento de tus datos personales. Su domicilio es Hospital Star Médica, consultorio 101, Mariano "
                    + "Arista No. 735, Col. Barrio de Tequisquiapan, "),
                CodigoPostalDelAviso(),
                new TextoDelAviso(", San Luis Potosí, S.L.P.")),
        ]),
        new("Qué datos recabamos",
        [
            Vinetas(
                [
                    new TextoDelAviso("De identificación y contacto:", EnNegritas: true),
                    new TextoDelAviso(" nombre completo, teléfono y, si lo das, correo electrónico."),
                ],
                [
                    new TextoDelAviso("Sensibles:", EnNegritas: true),
                    new TextoDelAviso(
                        " el motivo de tu consulta, porque revela tu estado de salud. También puede serlo la nota que el "
                        + "consultorio agregue a tu cita."),
                ],
                [
                    new TextoDelAviso("De tus citas:", EnNegritas: true),
                    new TextoDelAviso(" fecha, hora, tipo de consulta, y si se canceló, se reprogramó o no te presentaste."),
                ]),
            Parrafo("No pedimos datos financieros ni patrimoniales."),
        ]),
        new("Para qué los usamos",
        [
            Parrafo("Usamos tus datos solo para:"),
            Numerada(
                "Agendar, confirmar, reprogramar o cancelar tu cita.",
                "Enviarte por correo la confirmación, el recordatorio del día anterior y los avisos de cambios o cancelaciones.",
                "Que la doctora prepare tu consulta: recibe cada mañana la lista de citas del día con la hora, tu nombre y el motivo.",
                "Reconocerte cuando vuelvas y llevar el historial de tus citas.",
                "Evitar el abuso del sistema de reservas, por ejemplo con un límite de citas por teléfono."),
            Parrafo(
                "Todas son necesarias para darte el servicio. No usamos tus datos para publicidad, no los vendemos y no los "
                + "usamos para ningún otro fin."),
        ]),
        new("Cómo das tu consentimiento",
        [
            Parrafo(
                "Al reservar en línea marcas dos casillas: una para aceptar este aviso y otra, aparte, para el uso del motivo "
                + "de tu consulta. Guardamos la fecha, la hora y la versión del aviso que aceptaste. Si el consultorio agenda "
                + "tu cita por teléfono o WhatsApp, registra el consentimiento que diste por ese medio."),
        ]),
        new("Con quién compartimos tus datos",
        [
            Parrafo(
                "No transferimos tus datos a terceros. Los guardan y procesan, solo por cuenta nuestra y bajo nuestras "
                + "instrucciones, los proveedores que alojan el sistema y envían los correos (Microsoft Azure). Solo los "
                + "entregaríamos a una autoridad que los pida conforme a la ley."),
        ]),
        new("Cuánto tiempo los guardamos",
        [
            Parrafo(
                "Hasta 5 años después de tu última cita. Después los borramos o los dejamos sin nada que te identifique."),
        ]),
        new("Tus derechos",
        [
            Parrafo("Puedes pedir en cualquier momento:"),
            Vinetas(
                [new TextoDelAviso("Acceso:", EnNegritas: true), new TextoDelAviso(" saber qué datos tenemos de ti.")],
                [new TextoDelAviso("Rectificación:", EnNegritas: true), new TextoDelAviso(" corregirlos.")],
                [new TextoDelAviso("Cancelación:", EnNegritas: true), new TextoDelAviso(" que los borremos.")],
                [new TextoDelAviso("Oposición:", EnNegritas: true), new TextoDelAviso(" que dejemos de usarlos para un fin.")],
                [
                    new TextoDelAviso("Revocación:", EnNegritas: true),
                    new TextoDelAviso(" retirar tu consentimiento, sin efecto sobre lo ya hecho."),
                ],
                [new TextoDelAviso("Limitación:", EnNegritas: true), new TextoDelAviso(" restringir su uso o divulgación.")]),
            Parrafo(
                new TextoDelAviso("Escríbenos a "),
                CorreoDelAviso(enNegritas: true),
                new TextoDelAviso(" o entrega tu solicitud por escrito en el consultorio. Incluye:")),
            Numerada(
                "Tu nombre y cómo quieres que te respondamos.",
                "Una identificación, o el documento que acredite a tu representante.",
                "Qué datos son y qué derecho quieres ejercer.",
                "Cualquier dato que nos ayude a encontrarlos, por ejemplo la fecha de tu cita."),
            Parrafo(
                "Te respondemos en un máximo de 20 días. Si procede, lo hacemos efectivo en los 15 días siguientes. "
                + "Cualquiera de los dos plazos se puede ampliar una vez, por el mismo tiempo, si las circunstancias lo "
                + "justifican. Es gratuito; solo se cobrarían copias o envíos. Si no te respondemos o no estás de acuerdo con "
                + "la respuesta, puedes acudir a la Secretaría Anticorrupción y Buen Gobierno."),
            Parrafo(
                "Si retiras tu consentimiento para el motivo de tu consulta, dejaremos de usarlo. Es posible que entonces no "
                + "podamos agendar tu cita en línea."),
        ]),
        new("Cookies",
        [
            Parrafo(
                "Este sitio no usa cookies de publicidad ni de rastreo. Solo usa las técnicas que necesitan los formularios "
                + "para ser seguros."),
        ]),
        new("Cambios a este aviso",
        [
            Parrafo(
                "Cualquier cambio se publica en esta página, con su nueva versión y fecha. La versión que aceptaste queda "
                + "registrada con tu cita."),
        ]),
    ];

    /// <summary>
    /// El aviso simplificado que aprobó Selina (Gerardo, 2026-11-24), literal: Tus datos lo muestra arriba de las
    /// casillas, con "Aviso de privacidad completo" como enlace a la página del aviso.
    /// </summary>
    public static readonly IReadOnlyList<FragmentoDelAviso> AvisoSimplificado =
    [
        new TextoDelAviso(
            "La Dra. Selina Medina Castillo (Hospital Star Médica, consultorio 101, San Luis Potosí) usa tu nombre, "
            + "teléfono, correo y el motivo de tu consulta, que es un dato de salud, solo para agendar tu cita, avisarte de "
            + "ella y que la doctora la prepare. No los compartimos ni los usamos para publicidad. Para limitar su uso o "
            + "ejercer tus derechos, escribe a "),
        CorreoDelAviso(enNegritas: false),
        new TextoDelAviso(". Lee el "),
        new EnlaceDelAviso("Aviso de privacidad completo", RutaDelAviso),
        new TextoDelAviso("."),
    ];

    private static DatoDelAviso CorreoDelAviso(bool enNegritas) =>
        new(CorreoDePrivacidadDelConsultorio, CorreoPendiente, enNegritas);

    private static DatoDelAviso CodigoPostalDelAviso() =>
        new(Consultorio.CodigoPostal is { } cp ? $"C.P. {cp}" : null, CodigoPostalPendiente);

    private static ParrafoDelAviso Parrafo(string texto) => new([new TextoDelAviso(texto)]);

    private static ParrafoDelAviso Parrafo(params FragmentoDelAviso[] fragmentos) => new(fragmentos);

    private static ListaDelAviso Vinetas(params IReadOnlyList<FragmentoDelAviso>[] elementos) => new(false, elementos);

    private static ListaDelAviso Numerada(params string[] elementos) =>
        new(true, [.. elementos.Select(e => (IReadOnlyList<FragmentoDelAviso>)[new TextoDelAviso(e)])]);

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

    public IReadOnlyList<SeccionDelAviso> TextoDelAvisoDePrivacidad => AvisoIntegral;

    public IReadOnlyList<FragmentoDelAviso> AvisoDePrivacidadSimplificado => AvisoSimplificado;

    public string? WhatsApp => WhatsAppDelConsultorio;
}
