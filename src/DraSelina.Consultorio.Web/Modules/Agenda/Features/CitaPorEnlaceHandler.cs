using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Tu cita por Enlace de gestión. Una consulta: no abre transacción ni cambia estado. Busca por el hash del token; una
/// Cita cuyo inicio ya pasó no se muestra, cualquiera que sea su estado (RN-22, RN-31). El nombre lo da Pacientes
/// (RN-29) y el lugar, Contenido (AD-15).
/// </summary>
public sealed class CitaPorEnlaceHandler(
    ICitasDelEnlace citas,
    IPacientesApi pacientes,
    IContenidoApi contenido,
    IClock reloj) : IQueryHandler<CitaPorEnlace, CitaDelEnlace>
{
    public async Task<Resultado<CitaDelEnlace>> HandleAsync(CitaPorEnlace consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        // Un token mal formado no es de ninguna Cita: ni se consulta la base.
        var cita = EnlaceDeGestion.HashDelToken(consulta.Token) is { } hash
            ? await citas.PorTokenHashAsync(hash, cancellationToken)
            : null;

        if (cita is null || cita.Inicio <= reloj.UtcNow || Estado(cita) is not { } estado)
        {
            return Resultado.Fallo(CodigosDeAgenda.CitaNoEncontrada).Como<CitaDelEnlace>();
        }

        // Toda Cita tiene su Paciente: si Pacientes no lo conoce, es un error de programación.
        var nombre = await pacientes.NombreAsync(cita.PacienteId, cancellationToken)
                     ?? throw new InvalidOperationException("La Cita apunta a un Paciente que Pacientes no conoce.");

        var (fecha, hora) = HoraLocal.DesdeUtc(cita.Inicio);
        return Resultado.Exito(new CitaDelEnlace(
            fecha,
            hora,
            cita.TipoDeVisita,
            (int)(cita.Fin - cita.Inicio).TotalMinutes,
            nombre,
            contenido.DatosDelConsultorio.Lugar,
            estado));
    }

    // Una Cita marcada No se presentó tiene su inicio en el pasado; si no, tampoco se muestra.
    private static EstadoDelEnlace? Estado(CitaLeidaPorEnlace cita) => (cita.Estado, cita.MotivoDeCancelacion) switch
    {
        (EstadoCita.Agendada, _) => EstadoDelEnlace.Agendada,
        (EstadoCita.Cancelada, MotivoDeCancelacion.Paciente or MotivoDeCancelacion.Tarde) => EstadoDelEnlace.CanceladaPorElPaciente,
        (EstadoCita.Cancelada, MotivoDeCancelacion.Consultorio) => EstadoDelEnlace.CanceladaPorElConsultorio,
        _ => null,
    };
}
