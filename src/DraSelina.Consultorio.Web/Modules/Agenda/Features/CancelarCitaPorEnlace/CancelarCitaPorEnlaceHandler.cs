using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Cancela una Cita por su Enlace de gestión (CAP-8, FR-10). Orquesta y no decide (T-13): busca solo por el hash del
/// token (AD-19); abre la transacción y lee la Cita con su fila bloqueada, así un cambio a la misma Cita (reprogramar u
/// otra cancelación) espera al primero y parte de lo que dejó. No toma candado de fecha: AD-26 solo aplica a crear o
/// mover. Arma <see cref="ReglasDeAgenda"/> con los tramos vigentes de la fecha de la Cita y la cancela por
/// <see cref="Cita.Cancelar"/>, que decide si es tardía (RN-16). Ya guardada, y en la misma transacción, deja su bitácora
/// con el actor de <see cref="IActorActual"/> (RN-24, AD-22) y publica <see cref="CitaCancelada"/> por el outbox (AD-3,
/// T-30). Un token sin Cita, o de una Cita cuyo inicio ya pasó, es <see cref="CodigosDeAgenda.CitaNoEncontrada"/>
/// (RN-22). Una Cita ya cancelada es éxito con su motivo guardado, sin bitácora ni evento (Gerardo, 2026-11-06). Ni el
/// token ni datos del Paciente van a la bitácora, al evento ni a los registros (AD-23).
/// </summary>
public sealed class CancelarCitaPorEnlaceHandler(
    ITiposDeVisita tiposDeVisita,
    ITransaccionDeAgenda transaccion,
    IReglasDeReserva reglasDeReserva,
    IAgendaAtencion agendaAtencion,
    ICitas citas,
    IBitacoraDeAgenda bitacora,
    IOutboxDeAgenda outbox,
    IActorActual actorActual,
    IClock reloj) : ICommandHandler<CancelarCitaPorEnlace, CitaCanceladaPorEnlace>
{
    public async Task<Resultado<CitaCanceladaPorEnlace>> HandleAsync(
        CancelarCitaPorEnlace comando,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        // Un token mal formado no es de ninguna Cita: ni se consulta la base.
        if (EnlaceDeGestion.HashDelToken(comando.Token) is not { } hash)
        {
            return NoEncontrada();
        }

        var ahora = reloj.UtcNow;

        // Sin sesión, quien llega con el Enlace de gestión es el Paciente (AD-22).
        var actor = await actorActual.ObtenerAsync();

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // Con su fila bloqueada: otro cambio a la misma Cita espera aquí a que este termine.
        var cita = await citas.ObtenerPorTokenHashParaCambiarAsync(hash, cancellationToken);
        if (cita is null || cita.Rango.Inicio <= ahora)
        {
            await tx.DeshacerAsync(cancellationToken);
            return NoEncontrada();
        }

        // El tipo existe: la llave foránea lo protege (Restrict). Su nombre de hoy va en la respuesta.
        var tipo = await tiposDeVisita.ObtenerAsync(cita.TipoDeVisitaId, cancellationToken)
                   ?? throw new InvalidOperationException($"La Cita {cita.Id} apunta a un tipo de visita que no existe.");

        // Ya cancelada: un segundo envío. Lo guardado, sin otra bitácora ni otro evento (Gerardo, 2026-11-06).
        if (cita.Estado == EstadoCita.Cancelada)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Cancelada(cita, tipo);
        }

        var reserva = await reglasDeReserva.ObtenerAsync(cancellationToken);
        var agenda = await agendaAtencion.ObtenerAsync(HoraLocal.DesdeUtc(ahora).Fecha, cancellationToken);
        var reglas = ReglasDeAgenda.Crear(agenda.TramosVigentes(HoraLocal.DesdeUtc(cita.Rango.Inicio).Fecha), reserva);

        var rechazo = cita.Cancelar(ahora, reglas);
        if (rechazo is not null)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(Codigo(rechazo.Value)).Como<CitaCanceladaPorEnlace>();
        }

        // Cancelar no mueve el rango: la restricción de exclusión no puede rechazarlo. Si aun así no se guarda, es un
        // error, no un resultado.
        if (await citas.GuardarAsync(cancellationToken) is not GuardadoDeCita.Guardada)
        {
            throw new InvalidOperationException($"La Cita {cita.Id} no se pudo guardar al cancelarla.");
        }

        await DejarRastroAsync(cita, actor, ahora, cancellationToken);

        await tx.ConfirmarAsync(cancellationToken);

        return Cancelada(cita, tipo);
    }

    private static Resultado<CitaCanceladaPorEnlace> NoEncontrada() =>
        Resultado.Fallo(CodigosDeAgenda.CitaNoEncontrada).Como<CitaCanceladaPorEnlace>();

    // Fecha y hora locales del inicio guardado (AD-17); tardía o por el Consultorio, según el motivo que guardó la Cita.
    private static Resultado<CitaCanceladaPorEnlace> Cancelada(Cita cita, TipoDeVisita tipo)
    {
        var (fecha, hora) = HoraLocal.DesdeUtc(cita.Rango.Inicio);
        return Resultado.Exito(
            new CitaCanceladaPorEnlace(
                fecha,
                hora,
                tipo.Nombre,
                Tardia: cita.MotivoDeCancelacion == MotivoDeCancelacion.Tarde,
                PorElConsultorio: cita.MotivoDeCancelacion == MotivoDeCancelacion.Consultorio));
    }

    // RN-24 y AD-3, dentro de la transacción y después de guardar: si el cambio no se confirma, tampoco quedan. El id
    // del Administrador llega con la épica 5; un cambio del Paciente lo deja nulo.
    private async Task DejarRastroAsync(Cita cita, Actor actor, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        var motivo = Motivo(cita.MotivoDeCancelacion!.Value);

        await bitacora.AnotarAsync(
            new EntradaDeBitacora(
                ahora,
                actor,
                ActorId: null,
                EntidadDeBitacora.Cita,
                cita.Id.ToString(),
                AccionDeBitacora.Cancelada,
                new
                {
                    inicio = cita.Rango.Inicio,
                    fin = cita.Rango.Fin,
                    motivo,
                }),
            cancellationToken);

        var eventoId = Guid.CreateVersion7();
        await outbox.PublicarAsync(
            new CitaCancelada(eventoId, ahora, cita.Id, cita.PacienteId, actor, cita.Rango.Inicio, motivo),
            eventoId,
            claveDeOrden: cita.Id.Valor,
            ahora,
            cancellationToken);
    }

    // El mismo texto que guarda la columna motivo_de_cancelacion (E-16); una prueba lo ata a TextoDeEnumeraciones.Motivo.
    private static string Motivo(MotivoDeCancelacion motivo) => motivo switch
    {
        MotivoDeCancelacion.Paciente => CitaCancelada.MotivoPaciente,
        MotivoDeCancelacion.Tarde => CitaCancelada.MotivoTarde,
        MotivoDeCancelacion.Consultorio => CitaCancelada.MotivoConsultorio,
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, "Motivo sin texto."),
    };

    private static string Codigo(RechazoAlCancelar rechazo) => rechazo switch
    {
        RechazoAlCancelar.CitaNoAgendada => CodigosDeAgenda.CitaNoAgendada,

        // Ya leída como futura arriba; si el reloj la alcanzó, se ve igual que una que ya pasó (RN-22).
        RechazoAlCancelar.YaEmpezo => CodigosDeAgenda.CitaNoEncontrada,
        _ => throw new ArgumentOutOfRangeException(nameof(rechazo), rechazo, "Rechazo sin código."),
    };
}
