namespace DraSelina.Consultorio.Web;

/// <summary>
/// Cuánto espera un evento después de cada entrega fallida, y cuándo deja de intentarse. Es el único lugar
/// que lo dice.
/// </summary>
public static class CalendarioDeReintentos
{
    /// <summary>La espera después de la primera falla, de la segunda, y así hasta la sexta.</summary>
    public static IReadOnlyList<TimeSpan> Esperas { get; } =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(4),
        TimeSpan.FromHours(12),
    ];

    /// <summary>Los intentos que tiene un evento: el primero y uno después de cada espera. Hoy, siete.</summary>
    public static int IntentosMaximos => Esperas.Count + 1;

    /// <summary>
    /// La espera que sigue a la falla número <paramref name="intentosFallidos"/>, o nulo si ya no hay otro
    /// intento: el evento queda <c>Fallido</c>.
    /// </summary>
    public static TimeSpan? EsperaTras(int intentosFallidos) =>
        intentosFallidos >= 1 && intentosFallidos <= Esperas.Count ? Esperas[intentosFallidos - 1] : null;
}
