namespace DraSelina.Consultorio.BuildingBlocks.Contracts;

/// <summary>
/// El handler de un evento de integración (AD-3). Un evento puede tener varios, o ninguno: quien publica no sabe
/// quién escucha. El servicio de entrega del host llama a cada uno cuando el evento ya está confirmado en el outbox
/// del módulo que lo publicó, y lo vuelve a entregar si alguno lanza: un handler debe tolerar recibir el mismo
/// evento más de una vez.
/// </summary>
public interface IEventHandler<in TEvento>
{
    Task HandleAsync(TEvento evento, CancellationToken cancellationToken);
}
