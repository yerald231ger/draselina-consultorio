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
    /// El aviso de privacidad integral vigente, en secciones, de la versión <see cref="VersionDelAvisoDePrivacidad"/>.
    /// Lo muestra la página del Aviso de privacidad.
    /// </summary>
    IReadOnlyList<SeccionDelAviso> TextoDelAvisoDePrivacidad { get; }

    /// <summary>
    /// El aviso de privacidad simplificado, de la misma versión: un párrafo que Tus datos muestra arriba de las casillas,
    /// con el enlace al aviso completo.
    /// </summary>
    IReadOnlyList<FragmentoDelAviso> AvisoDePrivacidadSimplificado { get; }

    /// <summary>
    /// El WhatsApp del consultorio, a 10 dígitos; nulo mientras no se conozca. Sin él, las pantallas dicen
    /// "escríbenos por WhatsApp" sin número (Gerardo, 2026-11-02).
    /// </summary>
    string? WhatsApp { get; }

    /// <summary>
    /// Quién atiende: lo que muestran la portada, Sobre mí y Padecimientos del sitio (FR-27), el pie y los datos
    /// estructurados para buscadores.
    /// </summary>
    DatosDeLaDoctora DatosDeLaDoctora { get; }
}

/// <summary>
/// Los datos de la doctora. Lo que es nulo es contenido pendiente de Selina: quien lo muestra lo marca como pendiente y
/// quien lo publica para buscadores lo omite (Gerardo, 2026-11-17).
/// </summary>
/// <param name="Nombre">"Dra. Selina Medina Castillo".</param>
/// <param name="Especialidad">"Traumatología y Ortopedia".</param>
/// <param name="CedulaProfesional">El número de la cédula profesional, sin universidad.</param>
/// <param name="CedulaDeEspecialidad">El número de la cédula de especialidad.</param>
/// <param name="ConsejoQueCertifica">El consejo que la certifica: "Consejo de Ortopedia".</param>
/// <param name="RegistroDeCertificacion">El registro de la certificación ante ese consejo.</param>
/// <param name="Formacion">Dónde estudió la carrera y la especialidad; nula mientras no se tenga.</param>
/// <param name="Semblanza">Semblanza breve escrita con Selina; los párrafos se separan con una línea en blanco. Nula mientras no se tenga.</param>
/// <param name="Afiliaciones">Los colegios a los que pertenece; nulas mientras no se tengan.</param>
/// <param name="Padecimientos">Los padecimientos que atiende, en el orden en que se muestran.</param>
public sealed record DatosDeLaDoctora(
    string Nombre,
    string Especialidad,
    string CedulaProfesional,
    string CedulaDeEspecialidad,
    string ConsejoQueCertifica,
    string RegistroDeCertificacion,
    string? Formacion,
    string? Semblanza,
    string? Afiliaciones,
    IReadOnlyList<Padecimiento> Padecimientos);

/// <summary>
/// Una categoría de "¿En qué te puedo ayudar?": una zona del cuerpo o un tipo de atención, que se despliega con la lista
/// de lo que Selina atiende en ella (Gerardo, 2026-11-23).
/// </summary>
/// <param name="Nombre">"Rodilla", "Hombro"…</param>
/// <param name="Atiende">Los padecimientos y tratamientos de la categoría, en el orden en que se muestran.</param>
public sealed record Padecimiento(string Nombre, IReadOnlyList<string> Atiende);

/// <summary>Los datos del consultorio.</summary>
/// <param name="Lugar">Hospital y consultorio: "Hospital Star Médica, consultorio 101".</param>
/// <param name="Direccion">Calle, número y colonia.</param>
/// <param name="Ciudad">Ciudad.</param>
/// <param name="EnlaceDelMapa">
/// Una búsqueda de Google Maps del lugar, la dirección y la ciudad: "Abrir en el mapa". Nulo si no hay enlace; quien lo
/// muestra lo omite entonces (Gerardo, 2026-11-08).
/// </param>
/// <param name="MapaIncrustado">
/// El mapa de Google Maps para un iframe, de la misma búsqueda que <paramref name="EnlaceDelMapa"/>. Nulo si no hay
/// mapa; el sitio lo omite entonces.
/// </param>
/// <param name="CodigoPostal">
/// El código postal del domicilio; nulo mientras no se tenga. El aviso de privacidad lo marca entonces como pendiente
/// (Gerardo, 2026-11-24).
/// </param>
public sealed record DatosDelConsultorio(
    string Lugar,
    string Direccion,
    string Ciudad,
    string? EnlaceDelMapa = null,
    string? MapaIncrustado = null,
    string? CodigoPostal = null);

/// <summary>Una sección del aviso de privacidad: su título y sus bloques, en orden.</summary>
/// <param name="Titulo">"Quién es responsable de tus datos".</param>
/// <param name="Bloques">Párrafos y listas.</param>
public sealed record SeccionDelAviso(string Titulo, IReadOnlyList<BloqueDelAviso> Bloques);

/// <summary>Un bloque de una sección del aviso: un <see cref="ParrafoDelAviso"/> o una <see cref="ListaDelAviso"/>.</summary>
public abstract record BloqueDelAviso;

/// <summary>Un párrafo del aviso.</summary>
/// <param name="Fragmentos">Su texto, en fragmentos.</param>
public sealed record ParrafoDelAviso(IReadOnlyList<FragmentoDelAviso> Fragmentos) : BloqueDelAviso;

/// <summary>Una lista del aviso, con viñetas o numerada.</summary>
/// <param name="Numerada">Numerada (1., 2., …); si no, con viñetas.</param>
/// <param name="Elementos">Cada elemento, en fragmentos.</param>
public sealed record ListaDelAviso(bool Numerada, IReadOnlyList<IReadOnlyList<FragmentoDelAviso>> Elementos) : BloqueDelAviso;

/// <summary>
/// Un tramo del texto del aviso: <see cref="TextoDelAviso"/>, <see cref="DatoDelAviso"/> o <see cref="EnlaceDelAviso"/>.
/// Los espacios entre tramos van dentro del texto.
/// </summary>
public abstract record FragmentoDelAviso;

/// <summary>Texto literal.</summary>
/// <param name="Texto">El texto, con sus espacios.</param>
/// <param name="EnNegritas">Se pinta en negritas.</param>
public sealed record TextoDelAviso(string Texto, bool EnNegritas = false) : FragmentoDelAviso;

/// <summary>Un dato de Contenido que puede faltar: quien lo pinta lo marca como pendiente si es nulo.</summary>
/// <param name="Valor">El dato; nulo mientras no exista.</param>
/// <param name="Pendiente">Qué falta, para la marca de pendiente: "correo para las solicitudes".</param>
/// <param name="EnNegritas">Se pinta en negritas.</param>
public sealed record DatoDelAviso(string? Valor, string Pendiente, bool EnNegritas = false) : FragmentoDelAviso;

/// <summary>Un enlace dentro del texto.</summary>
/// <param name="Texto">"Aviso de privacidad completo".</param>
/// <param name="Destino">"/aviso-de-privacidad".</param>
public sealed record EnlaceDelAviso(string Texto, string Destino) : FragmentoDelAviso;
