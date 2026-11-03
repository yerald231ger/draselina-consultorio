namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// El paquete que recibe la <see cref="Cita"/>: los tramos vigentes de su fecha más los valores de las reglas de
/// reserva (anticipación mínima, ventana, umbral de cancelación tardía y tope semanal). El agregado nunca lee
/// configuración (T-1): el handler arma este paquete con <see cref="ReglasDeReserva"/> y
/// <see cref="AgendaAtencion.TramosVigentes"/>. Inmutable y válido desde que se construye (T-8).
/// </summary>
public sealed record ReglasDeAgenda
{
    private ReglasDeAgenda(
        TramosVigentes tramosVigentes,
        int anticipacionMinimaDias,
        int ventanaMeses,
        int umbralCancelacionTardiaMin,
        int topeSemanalPorTelefono)
    {
        TramosVigentes = tramosVigentes;
        AnticipacionMinimaDias = anticipacionMinimaDias;
        VentanaMeses = ventanaMeses;
        UmbralCancelacionTardiaMin = umbralCancelacionTardiaMin;
        TopeSemanalPorTelefono = topeSemanalPorTelefono;
    }

    /// <summary>Los tramos que valen para la fecha local de la Cita (INV-V1).</summary>
    public TramosVigentes TramosVigentes { get; }

    /// <summary>Días calendario desde hoy antes de los cuales no se reserva. Hoy 1: desde mañana.</summary>
    public int AnticipacionMinimaDias { get; }

    /// <summary>Meses hacia adelante hasta los que se reserva. Hoy 2.</summary>
    public int VentanaMeses { get; }

    /// <summary>Minutos del umbral de cancelación tardía (RN-17). Hoy 120.</summary>
    public int UmbralCancelacionTardiaMin { get; }

    /// <summary>Citas en línea por teléfono en una semana (RN-18). Hoy 3.</summary>
    public int TopeSemanalPorTelefono { get; }

    /// <summary>El paquete con los tramos vigentes de la fecha de la Cita y los valores de <paramref name="reglas"/>.</summary>
    public static ReglasDeAgenda Crear(TramosVigentes tramosVigentes, ReglasDeReserva reglas)
    {
        ArgumentNullException.ThrowIfNull(tramosVigentes);
        ArgumentNullException.ThrowIfNull(reglas);

        return new ReglasDeAgenda(
            tramosVigentes,
            reglas.AnticipacionMinimaDias,
            reglas.VentanaMeses,
            reglas.UmbralCancelacionTardiaMin,
            reglas.TopeSemanalPorTelefono);
    }
}
