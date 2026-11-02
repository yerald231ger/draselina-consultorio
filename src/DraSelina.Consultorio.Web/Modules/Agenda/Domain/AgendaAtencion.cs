namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// Cuándo atiende Selina: la agenda base (la semana tipo, por día de la semana) más las agendas custom (por fecha).
/// Existe una sola en el sistema y no conoce citas (T-3, T-4). Es la única que contesta qué tramos valen en una
/// fecha (INV-V1, RN-4). Se carga con las agendas custom de hoy en adelante; las de fechas pasadas no participan en
/// ninguna regla.
/// </summary>
public sealed class AgendaAtencion
{
    private readonly Dictionary<DayOfWeek, IReadOnlyList<Tramo>> _base = [];
    private readonly SortedDictionary<DateOnly, AgendaCustom> _customs = [];

    private AgendaAtencion(DateOnly customsDesde)
    {
        CustomsDesde = customsDesde;
    }

    /// <summary>
    /// Desde qué fecha se cargaron las agendas custom. Las anteriores no están en el agregado y no participan en
    /// ninguna regla; quien guarda solo sincroniza de esa fecha en adelante.
    /// </summary>
    internal DateOnly CustomsDesde { get; }

    /// <summary>Las agendas custom cargadas, por fecha.</summary>
    public IReadOnlyCollection<AgendaCustom> Customs => _customs.Values;

    /// <summary>
    /// Una agenda de atención sin tramos: ningún día se atiende (INV-B3) y no hay agendas custom. Es el punto de
    /// partida antes de registrar la base con <see cref="CambiarBase"/>.
    /// </summary>
    public static AgendaAtencion Crear() => new(DateOnly.MinValue);

    /// <summary>Reconstitución desde la base: no valida (T-6).</summary>
    internal static AgendaAtencion Reconstituir(
        IEnumerable<(DayOfWeek Dia, Tramo Tramo)> tramosBase,
        IEnumerable<(DateOnly Fecha, string Motivo, IEnumerable<Tramo> Tramos)> customs,
        DateOnly customsDesde)
    {
        var agenda = new AgendaAtencion(customsDesde);
        foreach (var dia in tramosBase.GroupBy(t => t.Dia))
        {
            agenda._base[dia.Key] = Ordenados(dia.Select(t => t.Tramo));
        }

        foreach (var (fecha, motivo, tramos) in customs)
        {
            agenda._customs[fecha] = new AgendaCustom(fecha, Ordenados(tramos), motivo);
        }

        return agenda;
    }

    /// <summary>Los tramos de la agenda base para ese día de la semana; vacío si ese día no se atiende.</summary>
    public IReadOnlyList<Tramo> TramosBase(DayOfWeek dia) => _base.GetValueOrDefault(dia, []);

    /// <summary>
    /// Reemplaza los tramos de un día de la agenda base. Cero tramos: ese día no se atiende (INV-B3). No toca agendas
    /// custom (RN-7) ni citas (RN-6). INV-B1 lo garantiza <see cref="Tramo"/>.
    /// </summary>
    /// <param name="dia">El día de la semana.</param>
    /// <param name="tramos">Los tramos nuevos de ese día.</param>
    /// <param name="duracionMinima">La duración del tipo de visita más corto; la decide el llamador (T-1, INV-B5).</param>
    /// <returns>Nulo si se cambió; si no, por qué no.</returns>
    public RechazoDeAgendaAtencion? CambiarBase(DayOfWeek dia, IEnumerable<Tramo> tramos, TimeSpan duracionMinima)
    {
        if (!Enum.IsDefined(dia))
        {
            throw new ArgumentOutOfRangeException(nameof(dia), dia, "Día de la semana desconocido.");
        }

        var lista = Validar(tramos, duracionMinima, out var rechazo);
        if (rechazo is not null)
        {
            return rechazo;
        }

        _base[dia] = lista;
        return null;
    }

    /// <summary>
    /// Registra la agenda custom de una fecha: reemplaza por completo a la base ese día. Cero tramos: ese día no se
    /// atiende (INV-A3). Falla si la fecha ya tiene una (INV-A1).
    /// </summary>
    public RechazoDeAgendaAtencion? RegistrarCustom(DateOnly fecha, IEnumerable<Tramo> tramos, string motivo, TimeSpan duracionMinima)
    {
        if (_customs.ContainsKey(fecha))
        {
            return RechazoDeAgendaAtencion.FechaConCustom;
        }

        return GuardarCustom(fecha, tramos, motivo, duracionMinima);
    }

    /// <summary>Cambia los tramos y el motivo de la agenda custom de una fecha (INV-A2 a A4, A6).</summary>
    public RechazoDeAgendaAtencion? CambiarCustom(DateOnly fecha, IEnumerable<Tramo> tramos, string motivo, TimeSpan duracionMinima)
    {
        if (!_customs.ContainsKey(fecha))
        {
            return RechazoDeAgendaAtencion.FechaSinCustom;
        }

        return GuardarCustom(fecha, tramos, motivo, duracionMinima);
    }

    /// <summary>Borra la agenda custom de una fecha: la fecha vuelve a la base (RN-12).</summary>
    public RechazoDeAgendaAtencion? BorrarCustom(DateOnly fecha) =>
        _customs.Remove(fecha) ? null : RechazoDeAgendaAtencion.FechaSinCustom;

    /// <summary>
    /// Los tramos que valen para una fecha: los de su agenda custom si existe; si no, los de la base para ese día de
    /// la semana (INV-V1, RN-4).
    /// </summary>
    public TramosVigentes TramosVigentes(DateOnly fecha) =>
        _customs.TryGetValue(fecha, out var custom)
            ? new TramosVigentes(fecha, custom.Tramos, OrigenDeTramos.Custom)
            : new TramosVigentes(fecha, TramosBase(fecha.DayOfWeek), OrigenDeTramos.Base);

    /// <summary>Las fechas con agenda custom, en orden. Son las que no cambian al cambiar la base (RN-7).</summary>
    public IReadOnlyList<DateOnly> FechasConCustom() => [.. _customs.Keys];

    private RechazoDeAgendaAtencion? GuardarCustom(DateOnly fecha, IEnumerable<Tramo> tramos, string motivo, TimeSpan duracionMinima)
    {
        // INV-A4: siempre tiene motivo.
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return RechazoDeAgendaAtencion.SinMotivo;
        }

        var lista = Validar(tramos, duracionMinima, out var rechazo);
        if (rechazo is not null)
        {
            return rechazo;
        }

        _customs[fecha] = new AgendaCustom(fecha, lista, motivo.Trim());
        return null;
    }

    // INV-B2 e INV-A2: los tramos de un día no se enciman. INV-B5 e INV-A6: en cada uno cabe una cita del tipo más corto.
    private static IReadOnlyList<Tramo> Validar(IEnumerable<Tramo> tramos, TimeSpan duracionMinima, out RechazoDeAgendaAtencion? rechazo)
    {
        ArgumentNullException.ThrowIfNull(tramos);
        if (duracionMinima <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duracionMinima), duracionMinima, "La duración mínima es mayor que cero.");
        }

        var lista = Ordenados(tramos);
        rechazo = null;

        for (var i = 1; i < lista.Count; i++)
        {
            if (lista[i - 1].SeEncimaCon(lista[i]))
            {
                rechazo = RechazoDeAgendaAtencion.TramosEncimados;
                return lista;
            }
        }

        if (lista.Any(t => t.Duracion < duracionMinima))
        {
            rechazo = RechazoDeAgendaAtencion.TramoMasCortoQueLaDuracionMinima;
        }

        return lista;
    }

    // Una copia de solo lectura: quien la recibe no puede cambiar los tramos por fuera de las invariantes.
    private static IReadOnlyList<Tramo> Ordenados(IEnumerable<Tramo> tramos)
    {
        var lista = tramos.ToArray();
        if (lista.Any(t => t is null))
        {
            throw new ArgumentException("Un tramo no puede ser nulo.", nameof(tramos));
        }

        return Array.AsReadOnly(lista.OrderBy(t => t.Inicio).ToArray());
    }
}

/// <summary>
/// El horario propio de una fecha concreta: reemplaza por completo a la base ese día (RN-4). Entidad interna de
/// <see cref="AgendaAtencion"/>, identificada por su fecha (INV-A1); fuera del agregado nadie la crea ni la cambia.
/// </summary>
public sealed class AgendaCustom
{
    internal AgendaCustom(DateOnly fecha, IReadOnlyList<Tramo> tramos, string motivo)
    {
        Fecha = fecha;
        Tramos = tramos;
        Motivo = motivo;
    }

    public DateOnly Fecha { get; }

    /// <summary>En orden de inicio. Cero tramos: ese día no se atiende (INV-A3).</summary>
    public IReadOnlyList<Tramo> Tramos { get; }

    /// <summary>"cirugía", "vacaciones", "sábado abierto" (INV-A4).</summary>
    public string Motivo { get; }
}

/// <summary>Los tramos que valen para una fecha y de dónde salieron (INV-V1). Los produce <see cref="AgendaAtencion"/>.</summary>
/// <param name="Fecha">La fecha local.</param>
/// <param name="Tramos">En orden de inicio; vacío si ese día no se atiende.</param>
/// <param name="Origen">Si salieron de la agenda base o de la custom de esa fecha.</param>
public sealed record TramosVigentes(DateOnly Fecha, IReadOnlyList<Tramo> Tramos, OrigenDeTramos Origen)
{
    /// <summary>Si ese día no se atiende.</summary>
    public bool SinTramos => Tramos.Count == 0;
}

/// <summary>De dónde salieron los <see cref="TramosVigentes"/> de una fecha.</summary>
public enum OrigenDeTramos
{
    /// <summary>De la agenda base, por el día de la semana.</summary>
    Base = 1,

    /// <summary>De la agenda custom de esa fecha.</summary>
    Custom = 2,
}

/// <summary>Por qué <see cref="AgendaAtencion"/> no aceptó un cambio.</summary>
public enum RechazoDeAgendaAtencion
{
    /// <summary>Dos tramos del mismo día se enciman (INV-B2, INV-A2).</summary>
    TramosEncimados = 1,

    /// <summary>En algún tramo no cabe una cita del tipo de visita más corto (INV-B5, INV-A6).</summary>
    TramoMasCortoQueLaDuracionMinima = 2,

    /// <summary>La fecha ya tiene agenda custom (INV-A1).</summary>
    FechaConCustom = 3,

    /// <summary>La fecha no tiene agenda custom que cambiar o borrar.</summary>
    FechaSinCustom = 4,

    /// <summary>La agenda custom no trae motivo (INV-A4).</summary>
    SinMotivo = 5,
}
