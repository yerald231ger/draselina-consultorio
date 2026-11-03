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
/// tramos vigentes de esa fecha, crea la Cita con su Consentimiento por <see cref="Cita.Agendar"/>, que decide INV-C5,
/// y la guarda bajo la restricción de exclusión (AD-8). Ya guardada, y en la misma transacción, deja su entrada en la
/// bitácora con el actor de <see cref="IActorActual"/> (RN-24, AD-22) y publica <see cref="CitaAgendada"/> por el
/// outbox (AD-3, T-30): Cita, bitácora y evento se confirman juntos o ninguno. Ni este handler ni sus puertos
/// registran nombre, teléfono, correo ni motivo (AD-23).
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
        var fecha = comando.Fecha!.Value;
        var hora = comando.Hora!.Value;
        var ahora = reloj.UtcNow;

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

        var consentimiento = Consentimiento.Crear(ahora, contenido.VersionDelAvisoDePrivacidad, CanalDeConsentimiento.EnLinea);

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // AD-26: la fila sobre la fecha local de la Cita va antes de leer los tramos vigentes; así una custom que se
        // guarda en el mismo instante no deja pasar una Cita con los tramos de antes.
        var fechaLocal = HoraLocal.DesdeUtc(inicio).Fecha;
        await candado.TomarAsync(fechaLocal, cancellationToken);

        var reserva = await reglasDeReserva.ObtenerAsync(cancellationToken);
        var agenda = await agendaAtencion.ObtenerAsync(HoraLocal.DesdeUtc(ahora).Fecha, cancellationToken);
        var reglas = ReglasDeAgenda.Crear(agenda.TramosVigentes(fechaLocal), reserva);

        var rechazo = Cita.Agendar(paciente.Valor, tipo, inicio, comando.Motivo!, consentimiento, reglas, ahora, out var cita);
        if (rechazo is not null)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(Codigo(rechazo.Value)).Como<CitaAgendadaEnLinea>();
        }

        citas.Agregar(cita!);
        if (await citas.GuardarAsync(cancellationToken) == GuardadoDeCita.HorarioOcupado)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAgenda.HorarioOcupado).Como<CitaAgendadaEnLinea>();
        }

        await DejarRastroAsync(cita!, actor, ahora, cancellationToken);

        await tx.ConfirmarAsync(cancellationToken);

        var consultorio = contenido.DatosDelConsultorio;
        return new CitaAgendadaEnLinea(
            cita!.Id,
            fecha,
            hora,
            tipo.Nombre,
            tipo.DuracionMin,
            consultorio.Lugar,
            $"{consultorio.Direccion}, {consultorio.Ciudad}");
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
