using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Cambia un Tipo de visita activo con <see cref="TipoDeVisita.Cambiar"/>: vale desde la siguiente reserva (T-6). Uno
/// inexistente o inactivo es <see cref="CodigosDeAgenda.TipoNoEncontrado"/>. Bitácora <c>TipoDeVisita/Cambiada</c> con
/// antes y después; sin cambios, sin bitácora.
/// </summary>
public sealed class CambiarTipoDeVisitaHandler(
    ITransaccionDeAgenda transaccion, ITiposDeVisita tipos, IBitacoraDeAgenda bitacora, IActorActual actorActual, IClock reloj)
    : ICommandHandler<CambiarTipoDeVisita, TipoDeVisitaGuardado>
{
    public async Task<Resultado<TipoDeVisitaGuardado>> HandleAsync(CambiarTipoDeVisita comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var id = comando.Id!.Value;
        var actor = await actorActual.ObtenerAsync();
        var actorId = await actorActual.IdDelAdministradorAsync();
        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        var tipo = await tipos.ObtenerParaCambiarAsync(id, cancellationToken);
        if (tipo is not { Activo: true })
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAgenda.TipoNoEncontrado).Como<TipoDeVisitaGuardado>();
        }

        var antes = DetalleDeTipo.De(tipo);
        tipo.Cambiar(comando.Nombre!, comando.Descripcion, comando.DuracionMin!.Value, comando.ReservableEnLinea, comando.Orden!.Value);
        var despues = DetalleDeTipo.De(tipo);
        if (antes == despues)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Exito(new TipoDeVisitaGuardado(id));
        }

        await tipos.GuardarAsync(cancellationToken);
        await bitacora.AnotarAsync(
            new EntradaDeBitacora(
                reloj.UtcNow, actor, actorId, EntidadDeBitacora.TipoDeVisita, id.Valor.ToString(), AccionDeBitacora.Cambiada,
                new { antes, despues }),
            cancellationToken);

        await tx.ConfirmarAsync(cancellationToken);
        return Resultado.Exito(new TipoDeVisitaGuardado(id));
    }
}
