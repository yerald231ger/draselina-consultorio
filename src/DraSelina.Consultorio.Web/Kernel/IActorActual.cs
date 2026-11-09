namespace DraSelina.Consultorio.Kernel;

/// <summary>
/// Quién actúa en el ámbito actual: <see cref="Actor.Administrador"/>, <see cref="Actor.Paciente"/> o
/// <see cref="Actor.Sistema"/>. Lo entrega el host por un único servicio (AD-22); ningún handler lo averigua a su
/// manera. Es asíncrono porque el estado de autenticación del host lo es.
/// </summary>
public interface IActorActual
{
    /// <summary>El actor del ámbito actual.</summary>
    Task<Actor> ObtenerAsync();

    /// <summary>
    /// El id de la cuenta del Administrador que actúa, para la bitácora (<c>actor_id</c>, RN-24); nulo si el actor no es
    /// <see cref="Actor.Administrador"/>.
    /// </summary>
    Task<string?> IdDelAdministradorAsync();
}
