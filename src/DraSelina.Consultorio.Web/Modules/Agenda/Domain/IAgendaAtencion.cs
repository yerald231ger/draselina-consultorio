namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>El puerto de escritura de <see cref="AgendaAtencion"/> (T-16).</summary>
public interface IAgendaAtencion
{
    /// <summary>
    /// La agenda de atención, con toda la base y las agendas custom de <paramref name="customsDesde"/> en adelante.
    /// Quien la pide pasa hoy, en fecha local: las custom de fechas pasadas no participan en ninguna regla.
    /// </summary>
    Task<AgendaAtencion> ObtenerAsync(DateOnly customsDesde, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda la base y las agendas custom de la fecha desde la que se obtuvo en adelante: agrega, cambia y borra lo
    /// que haga falta para que la base quede igual al agregado. Nunca toca las custom anteriores a hoy (fecha local) ni
    /// a esa fecha; si el agregado trae una, lanza <see cref="InvalidOperationException"/> sin guardar nada.
    /// </summary>
    Task GuardarAsync(AgendaAtencion agenda, CancellationToken cancellationToken);
}
