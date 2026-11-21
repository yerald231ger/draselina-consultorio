using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Si el Paciente capturado ya existe, con su próxima y su anterior Cita. Una consulta: no abre transacción ni cambia
/// estado. Pacientes busca por teléfono y nombre sin crear (RN-25, RN-26); si no existe, no se leen Citas. El estado de
/// cada Cita es el de la Agenda (<see cref="EstadoMostrado"/>). Ni este handler ni sus puertos registran nombre ni
/// teléfono (AD-23).
/// </summary>
public sealed class PacienteReconocidoHandler(IPacientesApi pacientes, ICitasDelPaciente citas, IClock reloj)
    : IQueryHandler<PacienteReconocido, PacienteDeLaCita>
{
    public async Task<Resultado<PacienteDeLaCita>> HandleAsync(PacienteReconocido consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (await pacientes.BuscarAsync(consulta.Nombre, consulta.Telefono, cancellationToken) is not { } paciente)
        {
            return Resultado.Exito(PacienteDeLaCita.Desconocido);
        }

        var ahora = reloj.UtcNow;
        var (proxima, anterior) = await citas.ProximaYAnteriorAsync(paciente, ahora, cancellationToken);
        return Resultado.Exito(new PacienteDeLaCita(true, Mostrada(proxima, ahora), Mostrada(anterior, ahora)));
    }

    private static CitaDelPaciente? Mostrada(CitaLeidaDelPaciente? cita, DateTimeOffset ahora)
    {
        if (cita is null)
        {
            return null;
        }

        var (fecha, hora) = HoraLocal.DesdeUtc(cita.Inicio);
        return new CitaDelPaciente(
            fecha,
            hora,
            cita.TipoDeVisita,
            EstadoMostrado.Estado(cita.Estado, cita.Inicio, ahora),
            EstadoMostrado.Cancelacion(cita.MotivoDeCancelacion));
    }
}
