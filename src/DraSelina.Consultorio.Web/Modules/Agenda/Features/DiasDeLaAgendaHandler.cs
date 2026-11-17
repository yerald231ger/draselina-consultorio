using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Los días de la Agenda del Administrador. Una consulta: no abre transacción ni cambia estado. Reúne en una vuelta las
/// Citas del rango, la agenda de atención del rango (base y customs, una lectura cada una) y los contactos en lote de
/// Pacientes (AD-10, RN-29): la página no compone módulos ni decide marcas. Atendida se deriva aquí, con
/// <see cref="EstadoMostrado"/> (RN-19, RN-31);
/// "fuera de horario" sale de <see cref="AgendaAtencion.TramosVigentes"/> y <see cref="Domain.Cita.QuedaFueraDe"/> (INV-C6).
/// </summary>
public sealed class DiasDeLaAgendaHandler(
    ICitasDeLaAgenda citas, IAgendaDelRango agenda, IPacientesApi pacientes, IClock reloj)
    : IQueryHandler<DiasDeLaAgenda, IReadOnlyList<DiaDeLaAgenda>>
{
    public async Task<Resultado<IReadOnlyList<DiaDeLaAgenda>>> HandleAsync(
        DiasDeLaAgenda consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var dias = consulta.Hasta.DayNumber - consulta.Desde.DayNumber + 1;
        if (dias < 1)
        {
            return Resultado.Invalido(nameof(DiasDeLaAgenda.Hasta), "El último día no puede ser anterior al primero.")
                .Como<IReadOnlyList<DiaDeLaAgenda>>();
        }

        if (dias > DiasDeLaAgenda.MaximoDeDias)
        {
            return Resultado.Invalido(nameof(DiasDeLaAgenda.Hasta), $"A lo más {DiasDeLaAgenda.MaximoDeDias} días de una vez.")
                .Como<IReadOnlyList<DiaDeLaAgenda>>();
        }

        var leidas = await citas.EntreAsync(
            HoraLocal.AUtc(consulta.Desde, TimeOnly.MinValue),
            HoraLocal.AUtc(consulta.Hasta.AddDays(1), TimeOnly.MinValue),
            cancellationToken);
        var atencion = await agenda.LeerAsync(consulta.Desde, consulta.Hasta, cancellationToken);
        var contactos = await pacientes.ObtenerContactosAsync([.. leidas.Select(c => c.PacienteId)], cancellationToken);
        var ahora = reloj.UtcNow;

        var porFecha = leidas.ToLookup(c => HoraLocal.DesdeUtc(c.Inicio).Fecha);
        var customs = atencion.Customs.ToDictionary(c => c.Fecha);

        IReadOnlyList<DiaDeLaAgenda> lista =
        [
            .. Enumerable.Range(0, dias).Select(i =>
            {
                var fecha = consulta.Desde.AddDays(i);
                var vigentes = atencion.TramosVigentes(fecha);
                var custom = customs.GetValueOrDefault(fecha);

                return new DiaDeLaAgenda(
                    fecha,
                    ClasificacionDelDia.De(vigentes, atencion.TramosBase(fecha.DayOfWeek).Count > 0),
                    custom?.Motivo,
                    [.. vigentes.Tramos.Select(t => new TramoDeLaAgenda(t.Inicio, t.Fin))],
                    [.. porFecha[fecha].Select(c => Cita(c, vigentes, contactos, ahora))]);
            }),
        ];

        return Resultado.Exito(lista);
    }

    private static CitaDeLaAgenda Cita(
        CitaLeidaDeLaAgenda cita, TramosVigentes vigentes, IReadOnlyDictionary<PatientId, Contactos> contactos, DateTimeOffset ahora)
    {
        var inicio = HoraLocal.DesdeUtc(cita.Inicio).Hora;
        var fin = HoraLocal.DesdeUtc(cita.Fin).Hora;
        var contacto = contactos.GetValueOrDefault(cita.PacienteId);

        return new CitaDeLaAgenda(
            cita.Id,
            inicio,
            fin,
            contacto?.Nombre,
            cita.MotivoDeConsulta,
            EstadoMostrado.Estado(cita.Estado, cita.Inicio, ahora),
            EstadoMostrado.Cancelacion(cita.MotivoDeCancelacion),
            SinCorreo: contacto?.Correo is null,
            // INV-C6: solo una Cita del Consultorio puede quedar fuera de los Tramos; una en línea nunca lleva la marca. La
            // regla vive en la Cita (T-13), la misma con la que pide la nota al agendarse; el rango leído de la base se
            // reconstituye sin validar (T-6).
            FueraDeHorario: cita.Origen == OrigenDeCita.Consultorio
                && Domain.Cita.QuedaFueraDe(RangoDeTiempo.Reconstituir(cita.Inicio, cita.Fin), vigentes),
            cita.Nota);
    }
}
