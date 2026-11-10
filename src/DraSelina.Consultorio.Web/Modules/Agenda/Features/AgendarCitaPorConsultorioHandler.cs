using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Agenda una Cita del consultorio (CAP-13, FR-15). Orquesta y no decide (T-13): pide el Paciente a Pacientes, que
/// confirma en su propia transacción antes de la de Agenda (AD-28); abre la transacción de Agenda, toma el candado de
/// la fecha local (AD-26) y solo después lee los Tramos vigentes de esa fecha; crea la Cita con su Consentimiento
/// externo por <see cref="Cita.AgendarPorConsultorio"/>, que decide el pasado y la nota obligatoria fuera de horario
/// (INV-C6), y la guarda bajo la restricción de exclusión (AD-8). Ya guardada, y en la misma transacción, deja su
/// entrada en la bitácora a nombre del Administrador, con su id (RN-24, AD-22), y publica <see cref="CitaAgendada"/>
/// con origen consultorio por el outbox (AD-3, T-30): Cita, bitácora y evento se confirman juntos o ninguno. La
/// confirmación y el Recordatorio los arma Notificaciones al recibir el evento. Ni este handler ni sus puertos
/// registran nombre, teléfono, correo, motivo ni nota (AD-23).
/// </summary>
public sealed class AgendarCitaPorConsultorioHandler(
    ITiposDeVisita tiposDeVisita,
    IPacientesApi pacientes,
    IContenidoApi contenido,
    ITransaccionDeAgenda transaccion,
    ICandadoDeFecha candado,
    ITramosDeLaFecha tramosDeLaFecha,
    ICitas citas,
    IBitacoraDeAgenda bitacora,
    IOutboxDeAgenda outbox,
    IActorActual actorActual,
    IClock reloj,
    ClaveDeGestion claveDeGestion) : ICommandHandler<AgendarCitaPorConsultorio, CitaAgendadaPorConsultorio>
{
    public async Task<Resultado<CitaAgendadaPorConsultorio>> HandleAsync(
        AgendarCitaPorConsultorio comando,
        CancellationToken cancellationToken)
    {
        // La validación de forma (AD-12) ya garantizó que estos no son nulos y que el Consentimiento se confirmó.
        var fecha = comando.Fecha!.Value;
        var hora = comando.Hora!.Value;
        var ahora = reloj.UtcNow;

        // El comando es solo del Administrador (AD-22): su id va a la bitácora (RN-24).
        var actor = await actorActual.ObtenerAsync();
        var actorId = await actorActual.IdDelAdministradorAsync();

        // Inexistente no se agenda; si está inactivo lo decide la Cita (FR-15).
        var tipo = await tiposDeVisita.ObtenerAsync(comando.TipoDeVisitaId!.Value, cancellationToken);
        if (tipo is null)
        {
            return Resultado.Fallo(CodigosDeAgenda.TipoNoReservable).Como<CitaAgendadaPorConsultorio>();
        }

        // Fecha y hora locales del consultorio; la Cita guarda instantes UTC (AD-17).
        var inicio = HoraLocal.AUtc(fecha, hora);

        var paciente = await pacientes.BuscarOCrearAsync(comando.Nombre!, comando.Telefono!, comando.Correo, cancellationToken);
        if (!paciente.EsExito)
        {
            return DatoDelPacienteInvalido(paciente);
        }

        var consentimiento = Consentimiento.Crear(ahora, contenido.VersionDelAvisoDePrivacidad, CanalDeConsentimiento.Externo);

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // AD-26: la fila sobre la fecha local de la Cita va antes de leer los Tramos vigentes, como en la reserva en
        // línea; así una custom que se guarda en el mismo instante no deja pasar una Cita sin nota con los Tramos de antes.
        var fechaLocal = HoraLocal.DesdeUtc(inicio).Fecha;
        await candado.TomarAsync(fechaLocal, cancellationToken);

        var vigentes = await tramosDeLaFecha.VigentesAsync(fechaLocal, cancellationToken);

        var rechazo = Cita.AgendarPorConsultorio(
            paciente.Valor, tipo, inicio, comando.Nota, comando.Motivo, consentimiento, vigentes, ahora, claveDeGestion, out var cita);
        if (rechazo is not null)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(Codigo(rechazo.Value)).Como<CitaAgendadaPorConsultorio>();
        }

        citas.Agregar(cita!);
        if (await citas.GuardarAsync(cancellationToken) is not GuardadoDeCita.Guardada)
        {
            // Sin solicitud no hay choque por el único: es otra Cita en ese horario (RN-1).
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAgenda.HorarioOcupado).Como<CitaAgendadaPorConsultorio>();
        }

        var fueraDeHorario = cita!.QuedaFueraDeHorario(vigentes);

        await DejarRastroAsync(cita, actor, actorId, fueraDeHorario, ahora, cancellationToken);

        await tx.ConfirmarAsync(cancellationToken);

        var (fechaDeLaCita, horaDeLaCita) = HoraLocal.DesdeUtc(cita.Rango.Inicio);
        return Resultado.Exito(new CitaAgendadaPorConsultorio(cita.Id, fechaDeLaCita, horaDeLaCita, fueraDeHorario));
    }

    // RN-24 y AD-3, dentro de la transacción y después de guardar la Cita sin traslape: si la Cita no se confirma,
    // tampoco quedan. Nunca motivo, nota ni contacto (AD-23).
    private async Task DejarRastroAsync(
        Cita cita, Actor actor, string? actorId, bool fueraDeHorario, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        await bitacora.AnotarAsync(
            new EntradaDeBitacora(
                ahora,
                actor,
                actorId,
                EntidadDeBitacora.Cita,
                cita.Id.ToString(),
                AccionDeBitacora.Agendada,
                new
                {
                    inicio = cita.Rango.Inicio,
                    fin = cita.Rango.Fin,
                    tipoDeVisitaId = cita.TipoDeVisitaId.Valor,
                    origen = CitaAgendada.OrigenConsultorio,
                    fueraDeHorario,
                }),
            cancellationToken);

        var eventoId = Guid.CreateVersion7();
        await outbox.PublicarAsync(
            new CitaAgendada(
                eventoId, ahora, cita.Id, cita.PacienteId, actor, cita.Rango.Inicio, cita.Rango.Fin, CitaAgendada.OrigenConsultorio),
            eventoId,
            claveDeOrden: cita.Id.Valor,
            ahora,
            cancellationToken);
    }

    private static string Codigo(RechazoAlAgendar rechazo) => rechazo switch
    {
        RechazoAlAgendar.TipoNoReservable => CodigosDeAgenda.TipoNoReservable,
        RechazoAlAgendar.EnElPasado => CodigosDeAgenda.EnElPasado,
        RechazoAlAgendar.NotaObligatoria => CodigosDeAgenda.NotaObligatoria,
        _ => throw new ArgumentOutOfRangeException(nameof(rechazo), rechazo, "Rechazo sin código para una Cita del consultorio."),
    };

    // Lo que Pacientes rechaza de los datos es un error de forma del comando: va bajo su propiedad (AD-12).
    private static Resultado<CitaAgendadaPorConsultorio> DatoDelPacienteInvalido(Resultado paciente) =>
        paciente.Codigo switch
        {
            CodigosDePacientes.TelefonoInvalido => Resultado.Invalido(
                nameof(AgendarCitaPorConsultorio.Telefono), AgendarCitaPorConsultorio.MensajeDeTelefonoDeMexico)
                .Como<CitaAgendadaPorConsultorio>(),
            CodigosDePacientes.NombreInvalido => Resultado.Invalido(
                nameof(AgendarCitaPorConsultorio.Nombre), AgendarCitaPorConsultorio.MensajeDeNombre).Como<CitaAgendadaPorConsultorio>(),
            CodigosDePacientes.CorreoInvalido => Resultado.Invalido(
                nameof(AgendarCitaPorConsultorio.Correo), AgendarCitaPorConsultorio.MensajeDeCorreo).Como<CitaAgendadaPorConsultorio>(),
            _ => paciente.Como<CitaAgendadaPorConsultorio>(),
        };
}
