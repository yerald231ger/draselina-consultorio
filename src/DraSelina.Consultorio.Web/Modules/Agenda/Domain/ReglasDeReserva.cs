namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// Los valores configurables de la reserva. Agregado único en el sistema: protege que sus valores sean válidos. No es
/// <c>ReglasDeAgenda</c>: ese es el paquete que el handler arma con estos valores más los tramos vigentes para la Cita.
/// </summary>
public sealed class ReglasDeReserva
{
    // Para la reconstitución desde la base (T-6).
    private ReglasDeReserva()
    {
    }

    /// <summary>Días calendario desde hoy antes de los cuales no se reserva. Hoy 1: desde mañana.</summary>
    public int AnticipacionMinimaDias { get; private set; }

    /// <summary>Meses hacia adelante hasta los que se reserva. Hoy 2.</summary>
    public int VentanaMeses { get; private set; }

    /// <summary>
    /// Minutos antes del inicio desde los que una cancelación es tardía y hasta los que se puede reprogramar (RN-17).
    /// Hoy 120.
    /// </summary>
    public int UmbralCancelacionTardiaMin { get; private set; }

    /// <summary>Citas en línea por teléfono en una semana (RN-18). Hoy 3.</summary>
    public int TopeSemanalPorTelefono { get; private set; }

    /// <summary>Unas reglas nuevas: anticipación y umbral no negativos; ventana y tope mayores que cero.</summary>
    /// <returns>Nulo si se crearon; si no, por qué no.</returns>
    public static RechazoDeReglasDeReserva? Crear(
        int anticipacionMinimaDias,
        int ventanaMeses,
        int umbralCancelacionTardiaMin,
        int topeSemanalPorTelefono,
        out ReglasDeReserva? reglas)
    {
        reglas = null;
        var rechazo = Validar(anticipacionMinimaDias, ventanaMeses, umbralCancelacionTardiaMin, topeSemanalPorTelefono);
        if (rechazo is not null)
        {
            return rechazo;
        }

        reglas = new ReglasDeReserva
        {
            AnticipacionMinimaDias = anticipacionMinimaDias,
            VentanaMeses = ventanaMeses,
            UmbralCancelacionTardiaMin = umbralCancelacionTardiaMin,
            TopeSemanalPorTelefono = topeSemanalPorTelefono,
        };
        return null;
    }

    /// <summary>
    /// Cambia los cuatro valores. Anticipación y umbral pueden ser 0 (reservar el mismo día; ninguna cancelación es
    /// tardía), no negativos; ventana y tope son mayores que cero. Si alguno no vale, nada cambia.
    /// </summary>
    /// <returns>Nulo si se cambiaron; si no, por qué no.</returns>
    public RechazoDeReglasDeReserva? Cambiar(
        int anticipacionMinimaDias,
        int ventanaMeses,
        int umbralCancelacionTardiaMin,
        int topeSemanalPorTelefono)
    {
        var rechazo = Validar(anticipacionMinimaDias, ventanaMeses, umbralCancelacionTardiaMin, topeSemanalPorTelefono);
        if (rechazo is not null)
        {
            return rechazo;
        }

        AnticipacionMinimaDias = anticipacionMinimaDias;
        VentanaMeses = ventanaMeses;
        UmbralCancelacionTardiaMin = umbralCancelacionTardiaMin;
        TopeSemanalPorTelefono = topeSemanalPorTelefono;
        return null;
    }

    private static RechazoDeReglasDeReserva? Validar(
        int anticipacionMinimaDias,
        int ventanaMeses,
        int umbralCancelacionTardiaMin,
        int topeSemanalPorTelefono) =>
        anticipacionMinimaDias < 0 ? RechazoDeReglasDeReserva.AnticipacionMinimaNegativa
        : ventanaMeses <= 0 ? RechazoDeReglasDeReserva.VentanaNoPositiva
        : umbralCancelacionTardiaMin < 0 ? RechazoDeReglasDeReserva.UmbralNegativo
        : topeSemanalPorTelefono <= 0 ? RechazoDeReglasDeReserva.TopeNoPositivo
        : null;
}

/// <summary>Por qué <see cref="ReglasDeReserva"/> no aceptó unos valores.</summary>
public enum RechazoDeReglasDeReserva
{
    /// <summary>0 vale: se reserva el mismo día.</summary>
    AnticipacionMinimaNegativa = 1,
    VentanaNoPositiva = 2,

    /// <summary>0 vale: ninguna cancelación es tardía.</summary>
    UmbralNegativo = 3,
    TopeNoPositivo = 4,
}
