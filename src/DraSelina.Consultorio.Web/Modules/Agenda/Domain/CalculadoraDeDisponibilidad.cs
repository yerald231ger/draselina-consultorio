namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// El único servicio de dominio del sistema y la única implementación del cálculo de disponibilidad y de la rejilla
/// (T-11, T-12, T-21). Es puro: no toca la base ni lee el reloj; recibe la agenda de atención, las Citas activas, la
/// duración del tipo de visita, las reglas de reserva y <c>now</c>. Las fechas y horas son locales de Ciudad de México;
/// las Citas, instantes UTC (AD-17).
/// </summary>
public static class CalculadoraDeDisponibilidad
{
    /// <summary>
    /// Las fechas de la ventana de reserva que tienen tramos vigentes, en orden, cada una con sus horarios libres. Una
    /// fecha con tramos y sin horarios libres va con la lista vacía; las fechas sin tramos no van. Por cada fecha:
    /// pide a la agenda sus tramos vigentes (INV-V1); dentro de cada tramo arma la rejilla desde su inicio, en pasos
    /// de la duración, solo los que caben completos; quita los que traslapan una Cita activa de cualquier origen
    /// (<c>[inicio, fin)</c>, AD-8) y los que no son posteriores a <paramref name="now"/>. Nada se guarda (RN-3).
    /// </summary>
    /// <param name="agenda">La agenda de atención, con las custom de hoy (fecha local) en adelante.</param>
    /// <param name="citasActivas">Los rangos de las Citas no canceladas de la ventana, de cualquier origen.</param>
    /// <param name="duracion">La duración del tipo de visita; mayor que cero.</param>
    /// <param name="reglas">La anticipación mínima y la ventana.</param>
    /// <param name="now">El instante actual, del reloj del llamador (T-20).</param>
    public static IReadOnlyList<DisponibilidadDelDia> Calcular(
        AgendaAtencion agenda,
        IEnumerable<RangoDeTiempo> citasActivas,
        TimeSpan duracion,
        ReglasDeReserva reglas,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(agenda);
        ArgumentNullException.ThrowIfNull(citasActivas);
        ArgumentNullException.ThrowIfNull(reglas);
        ValidarDuracion(duracion);

        var ocupados = citasActivas.ToArray();
        var ventana = Ventana(reglas, now);
        var dias = new List<DisponibilidadDelDia>();

        for (var fecha = ventana.PrimeraFecha; fecha <= ventana.UltimaFecha; fecha = fecha.AddDays(1))
        {
            var vigentes = agenda.TramosVigentes(fecha);
            if (vigentes.SinTramos)
            {
                continue;
            }

            var horarios = new List<HorarioLibre>();
            foreach (var tramo in vigentes.Tramos)
            {
                foreach (var hora in Rejilla(tramo, duracion))
                {
                    var inicio = HoraLocal.AUtc(fecha, hora);
                    var rango = RangoDeTiempo.Crear(inicio, inicio + duracion);
                    if (inicio > now && !ocupados.Any(rango.Traslapa))
                    {
                        horarios.Add(new HorarioLibre(fecha, hora));
                    }
                }
            }

            dias.Add(new DisponibilidadDelDia(fecha, horarios));
        }

        return dias;
    }

    /// <summary>
    /// Las fechas locales que abarca la ventana de reserva. Con anticipación de N días, la primera es hoy + N (con 1,
    /// mañana; con 0, hoy); la última, hoy + <see cref="ReglasDeReserva.VentanaMeses"/> meses, inclusive. "Hoy" es la
    /// fecha local de <paramref name="now"/> en Ciudad de México (AD-17).
    /// </summary>
    public static VentanaDeReserva Ventana(ReglasDeReserva reglas, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reglas);
        return Ventana(reglas.AnticipacionMinimaDias, reglas.VentanaMeses, now);
    }

    /// <summary>La misma ventana de reserva, con los valores que trae la Cita en sus <see cref="ReglasDeAgenda"/> (INV-C5).</summary>
    public static VentanaDeReserva Ventana(ReglasDeAgenda reglas, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reglas);
        return Ventana(reglas.AnticipacionMinimaDias, reglas.VentanaMeses, now);
    }

    private static VentanaDeReserva Ventana(int anticipacionMinimaDias, int ventanaMeses, DateTimeOffset now)
    {
        var hoy = HoraLocal.DesdeUtc(now).Fecha;
        return new VentanaDeReserva(hoy.AddDays(anticipacionMinimaDias), hoy.AddMonths(ventanaMeses));
    }

    /// <summary>
    /// Si <paramref name="hora"/> es el inicio de un horario de la rejilla de alguno de los tramos vigentes: desde el
    /// inicio del tramo, en pasos de la duración, y cabe completo (INV-C5). No mira Citas, ventana ni reloj.
    /// </summary>
    public static bool EstaEnLaRejilla(TramosVigentes vigentes, TimeOnly hora, TimeSpan duracion)
    {
        ArgumentNullException.ThrowIfNull(vigentes);
        ValidarDuracion(duracion);

        return vigentes.Tramos.Any(tramo => Rejilla(tramo, duracion).Contains(hora));
    }

    // La rejilla de un tramo: desde su inicio, en pasos de la duración, solo los que caben completos. En TimeSpan y no
    // en TimeOnly, que da la vuelta a medianoche.
    private static IEnumerable<TimeOnly> Rejilla(Tramo tramo, TimeSpan duracion)
    {
        var fin = tramo.Fin.ToTimeSpan();
        for (var inicio = tramo.Inicio.ToTimeSpan(); inicio + duracion <= fin; inicio += duracion)
        {
            yield return TimeOnly.FromTimeSpan(inicio);
        }
    }

    private static void ValidarDuracion(TimeSpan duracion)
    {
        if (duracion <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duracion), duracion, "La duración es mayor que cero.");
        }
    }
}

/// <summary>Una hora de inicio que hoy se puede reservar. Resultado del cálculo; nunca se guarda (RN-3).</summary>
/// <param name="Fecha">Fecha local del consultorio.</param>
/// <param name="Hora">Hora local del consultorio.</param>
public sealed record HorarioLibre(DateOnly Fecha, TimeOnly Hora);

/// <summary>Una fecha de la ventana con tramos vigentes y sus horarios libres; vacía si está llena.</summary>
/// <param name="Fecha">Fecha local del consultorio.</param>
/// <param name="Horarios">En orden de hora.</param>
public sealed record DisponibilidadDelDia(DateOnly Fecha, IReadOnlyList<HorarioLibre> Horarios);

/// <summary>Las fechas locales de la ventana de reserva, las dos inclusive.</summary>
/// <param name="PrimeraFecha">Hoy más la anticipación mínima.</param>
/// <param name="UltimaFecha">Hoy más la ventana en meses.</param>
public sealed record VentanaDeReserva(DateOnly PrimeraFecha, DateOnly UltimaFecha)
{
    /// <summary>Si la fecha local cae dentro de la ventana, las dos puntas inclusive.</summary>
    public bool Contiene(DateOnly fecha) => PrimeraFecha <= fecha && fecha <= UltimaFecha;

    /// <summary>Los instantes UTC que cubren la ventana: desde el inicio de la primera fecha hasta el de la siguiente a la última.</summary>
    public (DateTimeOffset Desde, DateTimeOffset Hasta) EnUtc() =>
        (HoraLocal.AUtc(PrimeraFecha, TimeOnly.MinValue), HoraLocal.AUtc(UltimaFecha.AddDays(1), TimeOnly.MinValue));
}
