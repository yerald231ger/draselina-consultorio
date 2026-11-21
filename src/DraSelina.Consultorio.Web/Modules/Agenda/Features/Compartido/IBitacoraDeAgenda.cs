using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// El puerto por el que el handler de un comando de Agenda deja la bitácora de su cambio (RN-24, CAP-17). La entrada
/// se guarda en <c>agenda.bitacora</c> dentro de la transacción abierta del handler: queda si la transacción se
/// confirma y desaparece si se deshace. Solo se inserta; nunca se actualiza ni se borra.
/// </summary>
public interface IBitacoraDeAgenda
{
    /// <summary>Guarda la entrada. Lanza si no hay una transacción abierta.</summary>
    Task AnotarAsync(EntradaDeBitacora entrada, CancellationToken cancellationToken);
}

/// <summary>Quién cambió qué y cuándo. Sin motivo de consulta ni datos de contacto (AD-23).</summary>
/// <param name="OcurridoEn">Cuándo, leído del reloj inyectado.</param>
/// <param name="Actor">Quién, de <see cref="IActorActual"/> (AD-22).</param>
/// <param name="ActorId">El id del usuario administrador; nulo para Paciente y Sistema.</param>
/// <param name="Entidad">Qué se cambió.</param>
/// <param name="EntidadId">El id de lo que se cambió: el de la Cita, la fecha de la custom, etc.</param>
/// <param name="Accion">Qué se le hizo.</param>
/// <param name="Detalle">Antes y después, o lo que hace falta para entender el cambio. Se guarda como JSON.</param>
public sealed record EntradaDeBitacora(
    DateTimeOffset OcurridoEn,
    Actor Actor,
    string? ActorId,
    EntidadDeBitacora Entidad,
    string EntidadId,
    AccionDeBitacora Accion,
    object? Detalle);

/// <summary>
/// Lo que registra la bitácora. Se guarda con su nombre. Base y custom se registran por separado aunque sean un solo
/// agregado, para saber qué parte cambió.
/// </summary>
public enum EntidadDeBitacora
{
    Cita = 1,
    AgendaBase = 2,
    AgendaCustom = 3,
    TipoDeVisita = 4,
    ReglasDeReserva = 5,
}

/// <summary>Lo que se le hizo a la entidad. Se guarda con su nombre.</summary>
public enum AccionDeBitacora
{
    Agendada = 1,
    Reprogramada = 2,
    Cancelada = 3,
    NoSePresento = 4,
    Registrada = 5,
    Cambiada = 6,
    Borrada = 7,
}
