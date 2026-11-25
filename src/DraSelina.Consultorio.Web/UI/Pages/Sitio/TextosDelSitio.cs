using DraSelina.Consultorio.Contenido.Contracts;

namespace DraSelina.Consultorio.UI.Pages.Sitio;

/// <summary>
/// Lo que leen los buscadores en <c>/</c> y en el Aviso de privacidad (FR-29; entrada 7.4): título con especialidad y
/// ciudad y meta description, con nombre y ciudad de Contenido (AD-15). Los textos son los que aprobó Gerardo (2026-11-17;
/// la description de Inicio, 2026-11-24).
/// </summary>
public static class TextosDelSitio
{
    /// <summary>"Traumatóloga en San Luis Potosí · Dra. Selina Medina Castillo".</summary>
    public static string TituloDeInicio(IContenidoApi contenido) =>
        $"Traumatóloga en {contenido.DatosDelConsultorio.Ciudad} · {contenido.DatosDeLaDoctora.Nombre}";

    /// <summary>"Aviso de privacidad · Traumatóloga en San Luis Potosí · Dra. Selina Medina Castillo".</summary>
    public static string TituloDelAviso(IContenidoApi contenido) => $"Aviso de privacidad · {TituloDeInicio(contenido)}";

    /// <summary>
    /// Especialidad y ciudad, lo que más se busca, el hospital y la llamada a agendar, en menos de 160 caracteres para
    /// que Google no la corte; el nombre ya va en el título (Gerardo, 2026-11-24). Las zonas son un resumen fijo, no la
    /// lista de Padecimientos: si Selina deja de atender alguna, se quita aquí también.
    /// </summary>
    public static string DescriptionDeInicio(IContenidoApi contenido) =>
        $"Traumatóloga y ortopedista en {contenido.DatosDelConsultorio.Ciudad}. Atiende rodilla, hombro, cadera, columna, "
        + "fracturas y lesiones deportivas en Star Médica. Agenda tu cita en línea.";

    /// <summary>"Aviso de privacidad del consultorio de la Dra. …, traumatóloga en San Luis Potosí."</summary>
    public static string DescriptionDelAviso(IContenidoApi contenido) =>
        $"Aviso de privacidad del consultorio de la {contenido.DatosDeLaDoctora.Nombre}, traumatóloga en "
        + $"{contenido.DatosDelConsultorio.Ciudad}.";

}
