using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Agenda una Cita en línea. Orquesta y no decide (T-13): pide el Paciente a Pacientes, que confirma en su propia
/// transacción antes de la de Agenda (AD-28); abre la transacción de Agenda, toma el candado de la fecha local
/// (AD-26, RN-20) y solo después lee las reglas y la agenda de atención; arma <see cref="ReglasDeAgenda"/> con los
/// tramos vigentes de esa fecha, cuenta las Citas Agendadas de la semana local de la Cita de los Pacientes que
/// comparten el teléfono (RN-18, AD-13), crea la Cita con su Consentimiento por <see cref="Cita.Agendar"/>, que decide
/// INV-C5 y el tope semanal, y la guarda bajo la restricción de exclusión (AD-8). Ya guardada, y en la misma transacción, deja su entrada en la
/// bitácora con el actor de <see cref="IActorActual"/> (RN-24, AD-22) y publica <see cref="CitaAgendada"/> por el
/// outbox (AD-3, T-30): Cita, bitácora y evento se confirman juntos o ninguno. Ni este handler ni sus puertos
/// registran nombre, teléfono, correo ni motivo (AD-23). Un comando cuya solicitud ya tiene Cita devuelve esa Cita, sin
/// crear otra ni dejar otra bitácora u otro evento (AD-27, RN-21): se busca antes de todo, otra vez después del
/// candado (un envío repetido de la misma fecha hace fila detrás del primero) y, si dos envíos de la misma solicitud
/// chocan al guardar, después de deshacer.
/// </summary>
public sealed class AgendarCitaEnLineaHandler(
    ITiposDeVisita tiposDeVisita,
    IPacientesApi pacientes,
    IContenidoApi contenido,
    ITransaccionDeAgenda transaccion,
    ICandadoDeFecha candado,
    IReglasDeReserva reglasDeReserva,
    IAgendaAtencion agendaAtencion,
    ICitas citas,
    ICitasActivas citasActivas,
    IBitacoraDeAgenda bitacora,
    IOutboxDeAgenda outbox,
    IActorActual actorActual,
    IClock reloj) : ICommandHandler<AgendarCitaEnLinea, CitaAgendadaEnLinea>
{
    public async Task<Resultado<CitaAgendadaEnLinea>> HandleAsync(
        AgendarCitaEnLinea comando,
        CancellationToken cancellationToken)
    {
        // La validación de forma (AD-12) ya garantizó que estos no son nulos.
        var solicitud = comando.SolicitudId!.Value;
        var fecha = comando.Fecha!.Value;
        var hora = comando.Hora!.Value;
        var ahora = reloj.UtcNow;

        // AD-27: el mismo formulario confirmado otra vez. Antes de todo: ni Pacientes ni las reglas cuentan ya.
        if (await citas.ObtenerPorSolicitudAsync(solicitud, cancellationToken) is { } yaAgendada)
        {
            return await ConfirmacionAsync(yaAgendada, cancellationToken);
        }

        // Sin sesión, una reserva en línea es del Paciente (AD-22).
        var actor = await actorActual.ObtenerAsync();

        var tipo = await tiposDeVisita.ObtenerAsync(comando.TipoDeVisitaId!.Value, cancellationToken);
        if (tipo is null)
        {
            return Resultado.Fallo(CodigosDeAgenda.TipoNoReservable).Como<CitaAgendadaEnLinea>();
        }

        // Fecha y hora locales del consultorio; la Cita guarda instantes UTC (AD-17).
        var inicio = HoraLocal.AUtc(fecha, hora);

        var paciente = await pacientes.BuscarOCrearAsync(comando.Nombre!, comando.Telefono!, comando.Correo, cancellationToken);
        if (!paciente.EsExito)
        {
            return DatoDelPacienteInvalido(paciente);
        }

        // RN-18: quiénes comparten el teléfono lo contesta Pacientes (RN-29); las Citas las cuenta Agenda. Pacientes ya
        // confirmó al Paciente de esta reserva, así que viene en la lista.
        var mismoTelefono = await pacientes.ConTelefonoAsync(comando.Telefono!, cancellationToken);

        var consentimiento = Consentimiento.Crear(ahora, contenido.VersionDelAvisoDePrivacidad, CanalDeConsentimiento.EnLinea);

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // AD-26: la fila sobre la fecha local de la Cita va antes de leer los tramos vigentes; así una custom que se
        // guarda en el mismo instante no deja pasar una Cita con los tramos de antes.
        var fechaLocal = HoraLocal.DesdeUtc(inicio).Fecha;
        await candado.TomarAsync(fechaLocal, cancellationToken);

        // Un envío repetido de la misma fecha esperó el candado detrás del primero, que ya confirmó su Cita: es esa.
        // Sin esta lectura, contaría la Cita del primero para el tope o chocaría con ella por horario (AD-27).
        if (await citas.ObtenerPorSolicitudAsync(solicitud, cancellationToken) is { } agendadaEnLaEspera)
        {
            await tx.DeshacerAsync(cancellationToken);
            return await ConfirmacionAsync(agendadaEnLaEspera, cancellationToken);
        }

        var reserva = await reglasDeReserva.ObtenerAsync(cancellationToken);
        var agenda = await agendaAtencion.ObtenerAsync(HoraLocal.DesdeUtc(ahora).Fecha, cancellationToken);
        var reglas = ReglasDeAgenda.Crear(agenda.TramosVigentes(fechaLocal), reserva);

        // La semana de calendario local de la Cita, de lunes a domingo (AD-17). Cuenta después del candado: es un
        // límite blando (AD-13); dos reservas simultáneas de días distintos pueden pasar las dos.
        var (lunes, lunesSiguiente) = HoraLocal.SemanaDe(fechaLocal);
        var citasDeLaSemana = await citasActivas.AgendadasDeAsync(mismoTelefono, lunes, lunesSiguiente, cancellationToken);

        var rechazo = Cita.Agendar(
            solicitud,
            paciente.Valor, tipo, inicio, comando.Motivo!, consentimiento, reglas, citasDeLaSemana, ahora, out var cita);
        if (rechazo is not null)
        {
            await tx.DeshacerAsync(cancellationToken);

            // Un envío repetido de otra fecha de la misma semana puede ver la Cita ya confirmada del primero en el
            // conteo (tope semanal) o en otra regla: si la solicitud ya tiene Cita, es esa (AD-27).
            return await citas.ObtenerPorSolicitudAsync(solicitud, cancellationToken) is { } guardadaPorOtroEnvio
                ? await ConfirmacionAsync(guardadaPorOtroEnvio, cancellationToken)
                : Resultado.Fallo(Codigo(rechazo.Value)).Como<CitaAgendadaEnLinea>();
        }

        citas.Agregar(cita!);
        if (await citas.GuardarAsync(cancellationToken) is not GuardadoDeCita.Guardada)
        {
            await tx.DeshacerAsync(cancellationToken);

            // Otro envío de la misma solicitud, con otra fecha (otro candado), guardó primero: choca por el único o por
            // horario. La base esperó a que confirmara, así que ya se lee. Si no es de esta solicitud, es de otro
            // Paciente: horario ocupado.
            return await citas.ObtenerPorSolicitudAsync(solicitud, cancellationToken) is { } guardadaPorOtroEnvio
                ? await ConfirmacionAsync(guardadaPorOtroEnvio, cancellationToken)
                : Resultado.Fallo(CodigosDeAgenda.HorarioOcupado).Como<CitaAgendadaEnLinea>();
        }

        await DejarRastroAsync(cita!, actor, ahora, cancellationToken);

        await tx.ConfirmarAsync(cancellationToken);

        return Confirmacion(cita!, tipo);
    }

    // Lo que pinta Cita confirmada, igual para la Cita recién creada y para la que ya tenía la solicitud. Fecha y hora
    // locales salen del inicio guardado (AD-17); la duración, del rango, que no se recalcula (INV-C3).
    private CitaAgendadaEnLinea Confirmacion(Cita cita, TipoDeVisita tipo)
    {
        var (fecha, hora) = HoraLocal.DesdeUtc(cita.Rango.Inicio);
        var consultorio = contenido.DatosDelConsultorio;
        return new CitaAgendadaEnLinea(
            cita.Id,
            fecha,
            hora,
            tipo.Nombre,
            (int)(cita.Rango.Fin - cita.Rango.Inicio).TotalMinutes,
            consultorio.Lugar,
            $"{consultorio.Direccion}, {consultorio.Ciudad}");
    }

    private async Task<Resultado<CitaAgendadaEnLinea>> ConfirmacionAsync(Cita cita, CancellationToken cancellationToken)
    {
        // El tipo existe: la llave foránea lo protege (Restrict). Su nombre puede haber cambiado; se muestra el de hoy.
        var tipo = await tiposDeVisita.ObtenerAsync(cita.TipoDeVisitaId, cancellationToken)
                   ?? throw new InvalidOperationException($"La Cita {cita.Id} apunta a un tipo de visita que no existe.");
        return Confirmacion(cita, tipo);
    }

    // RN-24 y AD-3, dentro de la transacción y después de guardar la Cita sin traslape: si la Cita no se confirma,
    // tampoco quedan. El id del Administrador llega con la épica 5; una reserva del Paciente lo deja nulo.
    private async Task DejarRastroAsync(Cita cita, Actor actor, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        var origen = Origen(cita.Origen);

        await bitacora.AnotarAsync(
            new EntradaDeBitacora(
                ahora,
                actor,
                ActorId: null,
                EntidadDeBitacora.Cita,
                cita.Id.ToString(),
                AccionDeBitacora.Agendada,
                new
                {
                    inicio = cita.Rango.Inicio,
                    fin = cita.Rango.Fin,
                    tipoDeVisitaId = cita.TipoDeVisitaId.Valor,
                    origen,
                }),
            cancellationToken);

        var eventoId = Guid.CreateVersion7();
        await outbox.PublicarAsync(
            new CitaAgendada(eventoId, ahora, cita.Id, cita.PacienteId, actor, cita.Rango.Inicio, cita.Rango.Fin, origen),
            eventoId,
            claveDeOrden: cita.Id.Valor,
            ahora,
            cancellationToken);
    }

    private static string Origen(OrigenDeCita origen) => origen switch
    {
        OrigenDeCita.EnLinea => CitaAgendada.OrigenEnLinea,
        OrigenDeCita.Consultorio => CitaAgendada.OrigenConsultorio,
        _ => throw new ArgumentOutOfRangeException(nameof(origen), origen, "Origen sin texto."),
    };

    private static string Codigo(RechazoAlAgendar rechazo) => rechazo switch
    {
        RechazoAlAgendar.TipoNoReservable => CodigosDeAgenda.TipoNoReservable,
        RechazoAlAgendar.EnElPasado => CodigosDeAgenda.EnElPasado,
        RechazoAlAgendar.FueraDeHorario => CodigosDeAgenda.FueraDeHorario,
        RechazoAlAgendar.FueraDeVentana => CodigosDeAgenda.FueraDeVentana,
        RechazoAlAgendar.TopeSemanal => CodigosDeAgenda.TopeSemanal,
        _ => throw new ArgumentOutOfRangeException(nameof(rechazo), rechazo, "Rechazo sin código."),
    };

    // Lo que Pacientes rechaza de los datos es un error de forma del comando: va bajo su propiedad (AD-12).
    private static Resultado<CitaAgendadaEnLinea> DatoDelPacienteInvalido(Resultado paciente) =>
        paciente.Codigo switch
        {
            CodigosDePacientes.TelefonoInvalido => Resultado.Invalido(
                nameof(AgendarCitaEnLinea.Telefono), AgendarCitaEnLinea.MensajeDeTelefonoDeMexico).Como<CitaAgendadaEnLinea>(),
            CodigosDePacientes.NombreInvalido => Resultado.Invalido(
                nameof(AgendarCitaEnLinea.Nombre), AgendarCitaEnLinea.MensajeDeNombre).Como<CitaAgendadaEnLinea>(),
            CodigosDePacientes.CorreoInvalido => Resultado.Invalido(
                nameof(AgendarCitaEnLinea.Correo), AgendarCitaEnLinea.MensajeDeCorreo).Como<CitaAgendadaEnLinea>(),
            _ => paciente.Como<CitaAgendadaEnLinea>(),
        };
}
