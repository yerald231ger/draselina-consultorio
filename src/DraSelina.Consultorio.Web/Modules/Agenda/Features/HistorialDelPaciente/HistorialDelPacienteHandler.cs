using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// El historial de un Paciente para el Administrador (entrada 5.7). Una consulta: no abre transacción ni cambia estado. El
/// contacto lo da Pacientes (AD-10, RN-29); si no lo conoce, no se leen Citas. Las Citas las lee Agenda sin rastrear, con
/// su estado como en la Agenda (<see cref="EstadoMostrado"/>), y el resumen sale de esas mismas Citas: Pacientes nunca
/// cuenta Citas (RN-29). Fechas y horas, locales (AD-17). Ni este handler ni sus puertos registran nombre, teléfono ni
/// correo (AD-23).
/// </summary>
public sealed class HistorialDelPacienteHandler(IPacientesApi pacientes, ICitasDelPaciente citas, IClock reloj)
    : IQueryHandler<HistorialDelPaciente, PacienteConHistorial>
{
    public async Task<Resultado<PacienteConHistorial>> HandleAsync(HistorialDelPaciente consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (await pacientes.ObtenerContactoConTelefonoAsync(consulta.PatientId, cancellationToken) is not { } contacto)
        {
            return Resultado.Fallo(CodigosDeAgenda.PacienteNoEncontrado).Como<PacienteConHistorial>();
        }

        var leidas = await citas.TodasAsync(consulta.PatientId, cancellationToken);
        var ahora = reloj.UtcNow;
        List<CitaDelHistorial> historial = [.. leidas.Select(c => Mostrada(c, ahora))];

        return Resultado.Exito(new PacienteConHistorial(
            consulta.PatientId,
            new ContactoDelPaciente(contacto.Nombre, contacto.Telefono, contacto.Correo),
            new ResumenDelHistorial(
                historial.Count,
                historial.Count(c => c.Estado == EstadoEnLaAgenda.NoSePresento),
                historial.Count(c => c.Cancelacion == CancelacionEnLaAgenda.Tardia)),
            historial));
    }

    private static CitaDelHistorial Mostrada(CitaLeidaDelPaciente cita, DateTimeOffset ahora)
    {
        var (fecha, hora) = HoraLocal.DesdeUtc(cita.Inicio);
        return new CitaDelHistorial(
            cita.Id,
            fecha,
            hora,
            cita.TipoDeVisita,
            EstadoMostrado.Origen(cita.Origen),
            EstadoMostrado.Estado(cita.Estado, cita.Inicio, ahora),
            EstadoMostrado.Cancelacion(cita.MotivoDeCancelacion));
    }
}
