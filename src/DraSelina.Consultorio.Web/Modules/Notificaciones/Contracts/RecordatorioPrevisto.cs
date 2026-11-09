using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Contracts;

/// <summary>
/// Si el Recordatorio de una Cita que empieza en <see cref="Fecha"/> y <see cref="Hora"/> saldrá, y a qué hora (FR-22):
/// lo que dice Cita confirmada con correo (EXPERIENCE.md, "Confirmada, con correo" y "Recordatorio omitido"). Es la
/// misma regla con la que Notificaciones programa el Recordatorio; la página no la repite (AD-14). Solo lee.
/// </summary>
/// <param name="Fecha">Fecha local del consultorio de la Cita.</param>
/// <param name="Hora">Hora local de inicio.</param>
public sealed record RecordatorioPrevisto(DateOnly Fecha, TimeOnly Hora) : IQuery<Recordatorio>;

/// <summary>El Recordatorio de esa Cita.</summary>
/// <param name="SeEnviara">Falso si su hora ya pasó: se omite.</param>
/// <param name="Hora">La hora local a la que sale, el día anterior a la Cita.</param>
public sealed record Recordatorio(bool SeEnviara, TimeOnly Hora);
