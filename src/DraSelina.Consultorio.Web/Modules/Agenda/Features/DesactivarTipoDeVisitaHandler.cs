using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Desactiva un Tipo de visita con <see cref="TipoDeVisita.Desactivar"/> (Gerardo, 2026-11-17): no el último activo
/// (<see cref="CodigosDeAgenda.UltimoTipoActivo"/>), contado con las filas bloqueadas para que dos desactivaciones a la vez
/// no dejen ninguno. Uno inexistente o ya inactivo es <see cref="CodigosDeAgenda.TipoNoEncontrado"/>. Bitácora
/// <c>TipoDeVisita/Cambiada</c> con <c>activo</c> de verdadero a falso. Las Citas del tipo no cambian.
/// </summary>
public sealed class DesactivarTipoDeVisitaHandler(
    ITransaccionDeAgenda transaccion, ITiposDeVisita tipos, IBitacoraDeAgenda bitacora, IActorActual actorActual, IClock reloj)
    : ICommandHandler<DesactivarTipoDeVisita, TipoDeVisitaGuardado>
{
    public async Task<Resultado<TipoDeVisitaGuardado>> HandleAsync(DesactivarTipoDeVisita comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var id = comando.Id!.Value;
        var actor = await actorActual.ObtenerAsync();
        var actorId = await actorActual.IdDelAdministradorAsync();
        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // Primero bloquear los activos: otra desactivación espera aquí y después ya cuenta lo que esta dejó.
        var activos = await tipos.ContarActivosParaCambiarAsync(cancellationToken);
        var tipo = await tipos.ObtenerParaCambiarAsync(id, cancellationToken);
        if (tipo is not { Activo: true })
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAgenda.TipoNoEncontrado).Como<TipoDeVisitaGuardado>();
        }

        if (activos <= 1)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAgenda.UltimoTipoActivo).Como<TipoDeVisitaGuardado>();
        }

        var antes = DetalleDeTipo.De(tipo);
        tipo.Desactivar();
        await tipos.GuardarAsync(cancellationToken);
        await bitacora.AnotarAsync(
            new EntradaDeBitacora(
                reloj.UtcNow, actor, actorId, EntidadDeBitacora.TipoDeVisita, id.Valor.ToString(), AccionDeBitacora.Cambiada,
                new { antes, despues = DetalleDeTipo.De(tipo) }),
            cancellationToken);

        await tx.ConfirmarAsync(cancellationToken);
        return Resultado.Exito(new TipoDeVisitaGuardado(id));
    }
}
