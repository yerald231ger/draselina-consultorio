using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Crea un Tipo de visita activo (CAP-16; entrada 6.7) con <see cref="TipoDeVisita.Crear"/>, y en la misma transacción
/// deja la bitácora <c>TipoDeVisita/Registrada</c> a nombre del Administrador con su id (RN-24).
/// </summary>
public sealed class CrearTipoDeVisitaHandler(
    ITransaccionDeAgenda transaccion, ITiposDeVisita tipos, IBitacoraDeAgenda bitacora, IActorActual actorActual, IClock reloj)
    : ICommandHandler<CrearTipoDeVisita, TipoDeVisitaGuardado>
{
    public async Task<Resultado<TipoDeVisitaGuardado>> HandleAsync(CrearTipoDeVisita comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var actor = await actorActual.ObtenerAsync();
        var actorId = await actorActual.IdDelAdministradorAsync();
        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // La validación de forma (AD-12) ya garantizó nombre, duración y orden.
        var tipo = TipoDeVisita.Crear(
            comando.Nombre!, comando.DuracionMin!.Value, comando.ReservableEnLinea, comando.Orden!.Value, comando.Descripcion);
        tipos.Agregar(tipo);
        await tipos.GuardarAsync(cancellationToken);

        await bitacora.AnotarAsync(
            new EntradaDeBitacora(
                reloj.UtcNow, actor, actorId, EntidadDeBitacora.TipoDeVisita, tipo.Id.Valor.ToString(), AccionDeBitacora.Registrada,
                new { antes = (object?)null, despues = DetalleDeTipo.De(tipo) }),
            cancellationToken);

        await tx.ConfirmarAsync(cancellationToken);
        return Resultado.Exito(new TipoDeVisitaGuardado(tipo.Id));
    }
}
