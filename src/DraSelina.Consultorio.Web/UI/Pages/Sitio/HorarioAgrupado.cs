using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.UI.Pages.Reserva;

namespace DraSelina.Consultorio.UI.Pages.Sitio;

/// <summary>Un renglón del horario del sitio: los días ("Lunes a viernes") y sus horas ("3:30 a 6:00 pm, con cita").</summary>
public sealed record GrupoDeHorario(string Dias, string Horas);

/// <summary>
/// Cómo se escribe la semana tipo en Ubicación y horarios (Decision de la épica 7, 2026-11-17): los días con los mismos
/// Tramos forman un grupo, en el orden de su primer día (lunes primero). Dentro de un grupo, una racha de 3 o más días
/// seguidos es un rango ("Lunes a viernes"), una de 2 se une con "y" ("Sábado y domingo") y un día suelto va por su
/// nombre; las piezas se unen con ", " y la última con " y " ("Lunes a miércoles y viernes").
/// </summary>
public static class HorarioAgrupado
{
    private static readonly DayOfWeek[] DeLunesADomingo =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];

    // En minúscula: Dias pone en mayúscula solo el inicio de la frase.
    private static readonly string[] Nombres = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    /// <summary>Los grupos de la semana; vacío si ningún día tiene Tramos.</summary>
    public static IReadOnlyList<GrupoDeHorario> Agrupar(IReadOnlyList<DiaDeLaBase> semana)
    {
        ArgumentNullException.ThrowIfNull(semana);

        // Posición de cada día con Tramos en la semana de lunes a domingo, con sus Tramos en orden.
        var conTramos = semana
            .Where(d => d.Tramos.Count > 0)
            .Select(d => (Posicion: Array.IndexOf(DeLunesADomingo, d.Dia), Tramos: d.Tramos.OrderBy(t => t.Inicio).ToList()))
            .OrderBy(d => d.Posicion)
            .ToList();

        var grupos = new List<(List<int> Posiciones, List<TramoDeLaAgenda> Tramos)>();
        foreach (var dia in conTramos)
        {
            var grupo = grupos.FirstOrDefault(g => g.Tramos.SequenceEqual(dia.Tramos));
            if (grupo.Posiciones is null)
            {
                grupos.Add(([dia.Posicion], dia.Tramos));
            }
            else
            {
                grupo.Posiciones.Add(dia.Posicion);
            }
        }

        return [.. grupos.Select(g => new GrupoDeHorario(Dias(g.Posiciones), Horas(g.Tramos)))];
    }

    /// <summary>"Lunes a viernes", "Lunes, miércoles y viernes", "Sábado y domingo".</summary>
    private static string Dias(List<int> posiciones)
    {
        var piezas = new List<string>();
        var inicio = 0;
        for (var i = 1; i <= posiciones.Count; i++)
        {
            if (i < posiciones.Count && posiciones[i] == posiciones[i - 1] + 1)
            {
                continue;
            }

            var racha = posiciones[inicio..i];
            piezas.AddRange(racha.Count >= 3
                ? [$"{Nombre(racha[0])} a {Nombre(racha[^1])}"]
                : racha.Select(Nombre));
            inicio = i;
        }

        var frase = Unir(piezas);
        return string.Concat(frase[..1].ToUpperInvariant(), frase[1..]);
    }

    /// <summary>"3:30 a 6:00 pm, con cita"; varios Tramos se unen con " y ".</summary>
    private static string Horas(List<TramoDeLaAgenda> tramos) =>
        $"{Unir([.. tramos.Select(Tramo)])}, con cita";

    /// <summary>"3:30 a 6:00 pm" si inicio y fin caen del mismo lado del mediodía; si no, "10:00 am a 1:00 pm".</summary>
    private static string Tramo(TramoDeLaAgenda tramo)
    {
        var fin = FormatoDeCita.Hora(tramo.Fin);
        var inicio = FormatoDeCita.Hora(tramo.Inicio);
        var mismoLado = tramo.Inicio.Hour < 12 == tramo.Fin.Hour < 12;
        return $"{(mismoLado ? inicio[..inicio.LastIndexOf(' ')] : inicio)} a {fin}";
    }

    private static string Nombre(int posicion) => Nombres[(int)DeLunesADomingo[posicion]];

    private static string Unir(List<string> piezas) => piezas.Count switch
    {
        1 => piezas[0],
        _ => $"{string.Join(", ", piezas[..^1])} y {piezas[^1]}",
    };
}
