using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// Una fila de <c>agenda.agenda_base_tramo</c>: un tramo de la semana tipo. <see cref="AgendaAtencion"/> no tiene id
/// propio y vive en tres tablas; <see cref="AgendaAtencionPersistida"/> lo arma a mano desde estas filas.
/// </summary>
public sealed class FilaDeTramoBase
{
    public const string Tabla = "agenda_base_tramo";

    public Guid Id { get; set; }

    /// <summary>ISO: 1 = lunes … 7 = domingo.</summary>
    public short DiaDeLaSemana { get; set; }

    /// <summary>Hora local de Ciudad de México.</summary>
    public TimeOnly Inicio { get; set; }

    /// <summary>Hora local de Ciudad de México.</summary>
    public TimeOnly Fin { get; set; }

    /// <summary>El día ISO (1 = lunes … 7 = domingo) de un <see cref="DayOfWeek"/>.</summary>
    public static short DiaIso(DayOfWeek dia) => dia == DayOfWeek.Sunday ? (short)7 : (short)dia;

    /// <summary>El <see cref="DayOfWeek"/> de un día ISO.</summary>
    public static DayOfWeek Dia(short diaIso) => diaIso == 7 ? DayOfWeek.Sunday : (DayOfWeek)diaIso;
}

/// <summary>Una fila de <c>agenda.agenda_custom</c>: el horario propio de una fecha. Sin tramos, ese día no se atiende.</summary>
public sealed class FilaDeAgendaCustom
{
    public const string Tabla = "agenda_custom";

    /// <summary>Fecha local; es la llave (INV-A1).</summary>
    public DateOnly Fecha { get; set; }

    public string Motivo { get; set; } = "";

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset ActualizadaEn { get; set; }

    public List<FilaDeTramoCustom> Tramos { get; set; } = [];
}

/// <summary>Una fila de <c>agenda.agenda_custom_tramo</c>: un tramo de una agenda custom.</summary>
public sealed class FilaDeTramoCustom
{
    public const string Tabla = "agenda_custom_tramo";

    public Guid Id { get; set; }

    public DateOnly Fecha { get; set; }

    public TimeOnly Inicio { get; set; }

    public TimeOnly Fin { get; set; }
}
