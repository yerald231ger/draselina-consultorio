using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Marca que el Paciente no se presentó (CAP-14, FR-16). Orquesta y no decide (T-13): abre la transacción y lee la Cita
/// por su id con la fila bloqueada, así otro cambio a la misma Cita espera al primero y parte de lo que dejó. No toma
/// candado de fecha: no crea ni mueve rangos (AD-26). La marca <see cref="Cita.MarcarNoSePresento"/>, que decide si su
/// inicio ya llegó (RN-31). Ya guardada, y en la misma transacción, deja su bitácora a nombre del Administrador, con su
/// id (RN-24, AD-22). No publica evento (T-23). Una Cita que ya No se presentó es éxito con lo guardado, sin otra
/// bitácora (Gerardo, 2026-11-06). Ni motivo de consulta, ni nota, ni contacto van a la bitácora (AD-23).
/// </summary>
public sealed class MarcarNoSePresentoHandler(
    ITransaccionDeAgenda transaccion,
    ICitas citas,
    IBitacoraDeAgenda bitacora,
    IActorActual actorActual,
    IClock reloj) : ICommandHandler<MarcarNoSePresento, CitaMarcadaNoSePresento>
{
    public async Task<Resultado<CitaMarcadaNoSePresento>> HandleAsync(
        MarcarNoSePresento comando,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        // La validación de forma (AD-12) ya garantizó que no es nulo.
        var id = comando.CitaId!.Value;
        var ahora = reloj.UtcNow;

        // El comando es solo del Administrador (AD-22): su id va a la bitácora (RN-24).
        var actor = await actorActual.ObtenerAsync();
        var actorId = await actorActual.IdDelAdministradorAsync();

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // Con su fila bloqueada: otro cambio a la misma Cita espera aquí a que este termine.
        var cita = await citas.ObtenerPorIdParaCambiarAsync(id, cancellationToken);
        if (cita is null)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAgenda.CitaNoEncontrada).Como<CitaMarcadaNoSePresento>();
        }

        // Ya marcada: un segundo envío. Lo guardado, sin otra bitácora (Gerardo, 2026-11-06).
        if (cita.Estado == EstadoCita.NoSePresento)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Marcada(cita, ahora);
        }

        var anterior = cita.Estado;
        var rechazo = cita.MarcarNoSePresento(ahora);
        if (rechazo is not null)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(Codigo(rechazo.Value)).Como<CitaMarcadaNoSePresento>();
        }

        // Marcar no mueve el rango: la restricción de exclusión no puede rechazarlo. Si aun así no se guarda, es un
        // error, no un resultado.
        if (await citas.GuardarAsync(cancellationToken) is not GuardadoDeCita.Guardada)
        {
            throw new InvalidOperationException($"La Cita {cita.Id} no se pudo guardar al marcarla.");
        }

        // RN-24, dentro de la transacción y después de guardar: si el cambio no se confirma, tampoco queda.
        await bitacora.AnotarAsync(
            new EntradaDeBitacora(
                ahora,
                actor,
                actorId,
                EntidadDeBitacora.Cita,
                cita.Id.ToString(),
                AccionDeBitacora.NoSePresento,
                new
                {
                    inicio = cita.Rango.Inicio,
                    fin = cita.Rango.Fin,
                    estadoAnterior = anterior.ToString(),
                    estadoNuevo = cita.Estado.ToString(),
                }),
            cancellationToken);

        await tx.ConfirmarAsync(cancellationToken);

        return Marcada(cita, ahora);
    }

    private static Resultado<CitaMarcadaNoSePresento> Marcada(Cita cita, DateTimeOffset ahora) =>
        Resultado.Exito(new CitaMarcadaNoSePresento(cita.Id, EstadoMostrado.Estado(cita.Estado, cita.Rango.Inicio, ahora)));

    private static string Codigo(RechazoAlMarcarNoSePresento rechazo) => rechazo switch
    {
        RechazoAlMarcarNoSePresento.CitaNoAgendada => CodigosDeAgenda.CitaNoAgendada,
        RechazoAlMarcarNoSePresento.AunNoEmpieza => CodigosDeAgenda.AunNoEmpieza,
        _ => throw new ArgumentOutOfRangeException(nameof(rechazo), rechazo, "Rechazo sin código."),
    };
}
