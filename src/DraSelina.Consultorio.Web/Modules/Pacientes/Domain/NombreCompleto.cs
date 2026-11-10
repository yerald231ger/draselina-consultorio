using System.Globalization;
using System.Text;

namespace DraSelina.Consultorio.Pacientes.Domain;

/// <summary>
/// El nombre de un Paciente, en tres formas (T-9):
/// <list type="bullet">
/// <item><see cref="Original"/>: como lo escribió, sin espacios al principio ni al final.</item>
/// <item><see cref="Normalizado"/>: con los espacios de más quitados. Con el teléfono es la identidad del Paciente;
/// mayúsculas y acentos los ignora la collation ICU de la base, no el código (RN-25, RN-26).</item>
/// <item><see cref="Busqueda"/>: además, en minúsculas y sin acentos, para buscar con <c>LIKE</c>.</item>
/// </list>
/// </summary>
public sealed record NombreCompleto
{
    /// <summary>El largo máximo, en caracteres, del nombre como se escribió.</summary>
    public const int LargoMaximo = 120;

    private NombreCompleto(string original, string normalizado, string busqueda)
    {
        Original = original;
        Normalizado = normalizado;
        Busqueda = busqueda;
    }

    public string Original { get; }

    public string Normalizado { get; }

    public string Busqueda { get; }

    /// <summary>
    /// El nombre, si tiene al menos una letra y a lo más <see cref="LargoMaximo"/> caracteres; si no, nulo.
    /// </summary>
    public static NombreCompleto? Crear(string? texto)
    {
        var original = texto?.Trim() ?? "";
        if (original.Length is 0 or > LargoMaximo || !original.Any(char.IsLetter))
        {
            return null;
        }

        var normalizado = SinEspaciosDeMas(original);
        return new NombreCompleto(original, normalizado, SinAcentos(normalizado).ToLowerInvariant());
    }

    /// <summary>
    /// Un texto en la forma de <see cref="Busqueda"/>: sin espacios al principio, al final ni de más, en minúsculas y sin
    /// acentos (AD-18). Así se compara lo que escribe el Administrador con lo guardado: "  López " da "lopez". No valida:
    /// vacío si el texto es nulo o solo espacios.
    /// </summary>
    public static string ParaBuscar(string? texto) =>
        SinAcentos(SinEspaciosDeMas(texto?.Trim() ?? "")).ToLowerInvariant();

    /// <summary>Reconstitución desde la base: no valida (T-6).</summary>
    internal static NombreCompleto Reconstituir(string original, string normalizado, string busqueda) =>
        new(original, normalizado, busqueda);

    public override string ToString() => Original;

    private static string SinEspaciosDeMas(string texto) =>
        string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // "José Ñúñez" -> "Jose Nunez": se separa cada letra de su acento y se quitan los acentos.
    private static string SinAcentos(string texto)
    {
        var separado = texto.Normalize(NormalizationForm.FormD);
        var sin = new StringBuilder(separado.Length);
        foreach (var caracter in separado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                sin.Append(caracter);
            }
        }

        return sin.ToString().Normalize(NormalizationForm.FormC);
    }
}
