using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Reprograma una Cita por su Enlace de gestión (CAP-7, FR-9). Orquesta y no decide (T-13): busca solo por el hash del
/// token (AD-19); abre la transacción, toma el candado de la fecha local nueva (AD-26, RN-20) y solo después lee la Cita
/// con su fila bloqueada, las reglas y la agenda de atención. Fecha y luego fila, siempre en ese orden: un segundo cambio
/// a la misma Cita espera al primero y parte de lo que dejó. Arma <see cref="ReglasDeAgenda"/> con los tramos vigentes
/// de la fecha nueva, mueve la Cita por <see cref="Cita.Reprogramar"/>, que decide el umbral e INV-C4 y C5 sin tope
/// semanal (RN-15, RN-17), y la guarda bajo la restricción de exclusión (AD-8). Ya guardada, y en la misma transacción,
/// deja su bitácora con el actor de <see cref="IActorActual"/> (RN-24, AD-22) y publica <see cref="CitaReprogramada"/>
/// por el outbox (AD-3, T-30). Un token sin Cita, o de una Cita cuyo inicio ya pasó, es
/// <see cref="CodigosDeAgenda.CitaNoEncontrada"/> (RN-22). Pedir el inicio que la Cita Agendada ya tiene es éxito sin
/// cambio, sin bitácora ni evento (Gerardo, 2026-11-06). Ni el token ni datos del Paciente van a la bitácora, al evento
/// ni a los registros (AD-23).
/// </summary>
public sealed class ReprogramarCitaPorEnlaceHandler(
    ITiposDeVisita tiposDeVisita,
    ITransaccionDeAgenda transaccion,
    ICandadoDeFecha candado,
    IReglasDeReserva reglasDeReserva,
    IAgendaAtencion agendaAtencion,
    ICitas citas,
    IBitacoraDeAgenda bitacora,
    IOutboxDeAgenda outbox,
    IActorActual actorActual,
    IClock reloj) : ICommandHandler<ReprogramarCitaPorEnlace, CitaReprogramadaPorEnlace>
{
    public async Task<Resultado<CitaReprogramadaPorEnlace>> HandleAsync(
        ReprogramarCitaPorEnlace comando,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        // Un token mal formado no es de ninguna Cita: ni se consulta la base.
        if (EnlaceDeGestion.HashDelToken(comando.Token) is not { } hash)
        {
            return NoEncontrada();
        }

        // La validación de forma (AD-12) ya garantizó que estos no son nulos.
        var inicio = HoraLocal.AUtc(comando.Fecha!.Value, comando.Hora!.Value);
        var ahora = reloj.UtcNow;

        // Sin sesión, quien llega con el Enlace de gestión es el Paciente (AD-22).
        var actor = await actorActual.ObtenerAsync();

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // AD-26: el candado de la fecha nueva va antes de leer los tramos vigentes y la Cita.
        var fechaLocal = HoraLocal.DesdeUtc(inicio).Fecha;
        await candado.TomarAsync(fechaLocal, cancellationToken);

        // Con su fila bloqueada: otro cambio a la misma Cita espera aquí a que este termine.
        var cita = await citas.ObtenerPorTokenHashParaCambiarAsync(hash, cancellationToken);
        if (cita is null || cita.Rango.Inicio <= ahora)
        {
            await tx.DeshacerAsync(cancellationToken);
            return NoEncontrada();
        }

        // Ya está ahí: un segundo envío idéntico, o la misma hora otra vez. Antes del umbral (Gerardo, 2026-11-06).
        if (cita.Estado == EstadoCita.Agendada && cita.Rango.Inicio == inicio)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Reprogramada(cita);
        }

        // El tipo existe: la llave foránea lo protege (Restrict). El fin nuevo sale de su duración de hoy (INV-C3).
        var tipo = await tiposDeVisita.ObtenerAsync(cita.TipoDeVisitaId, cancellationToken)
                   ?? throw new InvalidOperationException($"La Cita {cita.Id} apunta a un tipo de visita que no existe.");
        var reserva = await reglasDeReserva.ObtenerAsync(cancellationToken);
        var agenda = await agendaAtencion.ObtenerAsync(HoraLocal.DesdeUtc(ahora).Fecha, cancellationToken);
        var reglas = ReglasDeAgenda.Crear(agenda.TramosVigentes(fechaLocal), reserva);

        var anterior = cita.Rango;
        var rechazo = cita.Reprogramar(RangoDeTiempo.Crear(inicio, inicio + tipo.Duracion), reglas, ahora);
        if (rechazo is not null)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(Codigo(rechazo.Value)).Como<CitaReprogramadaPorEnlace>();
        }

        // Un UPDATE de inicio y fin: la restricción de exclusión lo revisa igual que un INSERT (AD-8, T-22).
        if (await citas.GuardarAsync(cancellationToken) is not GuardadoDeCita.Guardada)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAgenda.HorarioOcupado).Como<CitaReprogramadaPorEnlace>();
        }

        await DejarRastroAsync(cita, anterior, actor, ahora, cancellationToken);

        await tx.ConfirmarAsync(cancellationToken);

        return Reprogramada(cita);
    }

    private static Resultado<CitaReprogramadaPorEnlace> NoEncontrada() =>
        Resultado.Fallo(CodigosDeAgenda.CitaNoEncontrada).Como<CitaReprogramadaPorEnlace>();

    // Fecha y hora locales del inicio guardado (AD-17).
    private static Resultado<CitaReprogramadaPorEnlace> Reprogramada(Cita cita)
    {
        var (fecha, hora) = HoraLocal.DesdeUtc(cita.Rango.Inicio);
        return Resultado.Exito(new CitaReprogramadaPorEnlace(fecha, hora));
    }

    // RN-24 y AD-3, dentro de la transacción y después de guardar sin traslape: si el cambio no se confirma, tampoco
    // quedan. El id del Administrador llega con la épica 5; un cambio del Paciente lo deja nulo.
    private async Task DejarRastroAsync(
        Cita cita,
        RangoDeTiempo anterior,
        Actor actor,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        await bitacora.AnotarAsync(
            new EntradaDeBitacora(
                ahora,
                actor,
                ActorId: null,
                EntidadDeBitacora.Cita,
                cita.Id.ToString(),
                AccionDeBitacora.Reprogramada,
                new
                {
                    inicioAnterior = anterior.Inicio,
                    finAnterior = anterior.Fin,
                    inicioNuevo = cita.Rango.Inicio,
                    finNuevo = cita.Rango.Fin,
                }),
            cancellationToken);

        var eventoId = Guid.CreateVersion7();
        await outbox.PublicarAsync(
            new CitaReprogramada(
                eventoId, ahora, cita.Id, cita.PacienteId, actor, anterior.Inicio, cita.Rango.Inicio, cita.Rango.Fin),
            eventoId,
            claveDeOrden: cita.Id.Valor,
            ahora,
            cancellationToken);
    }

    private static string Codigo(RechazoAlReprogramar rechazo) => rechazo switch
    {
        RechazoAlReprogramar.CitaNoAgendada => CodigosDeAgenda.CitaNoAgendada,
        RechazoAlReprogramar.ReprogramacionCerrada => CodigosDeAgenda.ReprogramacionCerrada,
        RechazoAlReprogramar.EnElPasado => CodigosDeAgenda.EnElPasado,
        RechazoAlReprogramar.FueraDeHorario => CodigosDeAgenda.FueraDeHorario,
        RechazoAlReprogramar.FueraDeVentana => CodigosDeAgenda.FueraDeVentana,
        _ => throw new ArgumentOutOfRangeException(nameof(rechazo), rechazo, "Rechazo sin código."),
    };
}
