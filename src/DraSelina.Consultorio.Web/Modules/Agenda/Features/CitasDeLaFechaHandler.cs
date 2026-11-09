using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Las Citas de una fecha para la Agenda del Administrador. Una consulta: no abre transacción ni cambia estado. Los
/// nombres los pide a Pacientes en lote, una sola vez (AD-10, RN-29): la página no compone dos módulos. Una Cita cuyo
/// Paciente no existe se lista sin nombre. Atendida se deriva aquí: Agendada con el inicio ya llegado (RN-19, RN-31).
/// </summary>
public sealed class CitasDeLaFechaHandler(ICitasDeLaFecha citas, IPacientesApi pacientes, IClock reloj)
    : IQueryHandler<CitasDeLaFecha, IReadOnlyList<CitaDeLaFecha>>
{
    public async Task<Resultado<IReadOnlyList<CitaDeLaFecha>>> HandleAsync(
        CitasDeLaFecha consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var leidas = await citas.EntreAsync(
            HoraLocal.AUtc(consulta.Fecha, TimeOnly.MinValue),
            HoraLocal.AUtc(consulta.Fecha.AddDays(1), TimeOnly.MinValue),
            cancellationToken);

        var contactos = await pacientes.ObtenerContactosAsync([.. leidas.Select(c => c.PacienteId)], cancellationToken);
        var ahora = reloj.UtcNow;

        IReadOnlyList<CitaDeLaFecha> lista =
        [
            .. leidas.Select(c => new CitaDeLaFecha(
                c.Id,
                HoraLocal.DesdeUtc(c.Inicio).Hora,
                HoraLocal.DesdeUtc(c.Fin).Hora,
                contactos.TryGetValue(c.PacienteId, out var contacto) ? contacto.Nombre : null,
                c.MotivoDeConsulta,
                Estado(c, ahora),
                Cancelacion(c.MotivoDeCancelacion))),
        ];

        return Resultado.Exito(lista);
    }

    // RN-31: "ya pasó" se mide desde el inicio; en el inicio mismo ya cuenta como Atendida, el mismo corte de
    // Cita.Cancelar.
    private static EstadoEnLaAgenda Estado(CitaLeidaDeLaFecha cita, DateTimeOffset ahora) => cita.Estado switch
    {
        EstadoCita.Agendada when cita.Inicio <= ahora => EstadoEnLaAgenda.Atendida,
        EstadoCita.Agendada => EstadoEnLaAgenda.Agendada,
        EstadoCita.NoSePresento => EstadoEnLaAgenda.NoSePresento,
        EstadoCita.Cancelada => EstadoEnLaAgenda.Cancelada,
        _ => throw new InvalidOperationException($"Estado de Cita desconocido: {cita.Estado}."),
    };

    private static CancelacionEnLaAgenda? Cancelacion(MotivoDeCancelacion? motivo) => motivo switch
    {
        null => null,
        MotivoDeCancelacion.Paciente => CancelacionEnLaAgenda.PorElPaciente,
        MotivoDeCancelacion.Tarde => CancelacionEnLaAgenda.Tardia,
        MotivoDeCancelacion.Consultorio => CancelacionEnLaAgenda.PorElConsultorio,
        _ => throw new InvalidOperationException($"Motivo de cancelación desconocido: {motivo}."),
    };
}
