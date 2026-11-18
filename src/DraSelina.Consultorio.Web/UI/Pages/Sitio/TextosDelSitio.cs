using DraSelina.Consultorio.Contenido.Contracts;

namespace DraSelina.Consultorio.UI.Pages.Sitio;

/// <summary>
/// Lo que leen los buscadores en <c>/</c> y en el Aviso de privacidad (FR-29; entrada 7.4): título con especialidad y
/// ciudad y meta description, con nombre, ciudad y padecimientos de Contenido (AD-15). Los textos son los que aprobó
/// Gerardo (2026-11-17).
/// </summary>
public static class TextosDelSitio
{
    /// <summary>"Traumatóloga en San Luis Potosí · Dra. Selina Medina Castillo".</summary>
    public static string TituloDeInicio(IContenidoApi contenido) =>
        $"Traumatóloga en {contenido.DatosDelConsultorio.Ciudad} · {contenido.DatosDeLaDoctora.Nombre}";

    /// <summary>"Aviso de privacidad · Traumatóloga en San Luis Potosí · Dra. Selina Medina Castillo".</summary>
    public static string TituloDelAviso(IContenidoApi contenido) => $"Aviso de privacidad · {TituloDeInicio(contenido)}";

    /// <summary>Nombre, especialidad y ciudad, los padecimientos y la llamada a agendar.</summary>
    public static string DescriptionDeInicio(IContenidoApi contenido)
    {
        var padecimientos = contenido.DatosDeLaDoctora.Padecimientos.Select(p => p.Nombre.ToLowerInvariant()).ToList();
        var lista = padecimientos.Count switch
        {
            0 => "",
            1 => $" {Mayuscula(padecimientos[0])}.",
            _ => $" {Mayuscula(string.Join(", ", padecimientos[..^1]))} y {padecimientos[^1]}.",
        };
        return $"{contenido.DatosDeLaDoctora.Nombre}, traumatóloga y ortopedista en {contenido.DatosDelConsultorio.Ciudad}."
            + $"{lista} Agenda tu cita en línea.";
    }

    /// <summary>"Aviso de privacidad del consultorio de la Dra. …, traumatóloga en San Luis Potosí."</summary>
    public static string DescriptionDelAviso(IContenidoApi contenido) =>
        $"Aviso de privacidad del consultorio de la {contenido.DatosDeLaDoctora.Nombre}, traumatóloga en "
        + $"{contenido.DatosDelConsultorio.Ciudad}.";

    private static string Mayuscula(string texto) => texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];
}
