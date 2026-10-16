using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>
/// Comando de prueba de administración (AD-22): marcado con <see cref="IRequiereAdministrador"/>, así que sin
/// Administrador vuelve no autorizado y su handler no corre. Lleva un atributo de validación para probar que el
/// rechazo va antes que la validación. Su handler devuelve el actor que le dio <see cref="IActorActual"/>.
/// </summary>
public sealed class Administrar : ICommand<Actor>, IRequiereAdministrador
{
    public const string MensajeTextoRequerido = "El texto es obligatorio.";

    [Required(ErrorMessage = MensajeTextoRequerido)]
    public string Texto { get; set; } = "algo que administrar";
}

/// <summary>Consulta de prueba de administración: marcada, como <see cref="Administrar"/>.</summary>
public sealed class LeerAdministracion : IQuery<Actor>, IRequiereAdministrador;

/// <summary>Comando de prueba sin marca: cualquiera lo usa. Su handler devuelve el actor que recibió.</summary>
public sealed class PreguntarActor : ICommand<Actor>;

/// <summary>Cuántas veces corrieron los handlers de administración en esta web. Uno por web (singleton).</summary>
public sealed class LlamadasDeAdministracion
{
    private int _comando;
    private int _consulta;

    public int DelComando => Volatile.Read(ref _comando);

    public int DeLaConsulta => Volatile.Read(ref _consulta);

    public void ContarComando() => Interlocked.Increment(ref _comando);

    public void ContarConsulta() => Interlocked.Increment(ref _consulta);
}

/// <summary>El handler de <see cref="Administrar"/>: cuenta la llamada y devuelve el actor.</summary>
public sealed class AdministrarHandler(IActorActual actor, LlamadasDeAdministracion llamadas) : ICommandHandler<Administrar, Actor>
{
    public async Task<Resultado<Actor>> HandleAsync(Administrar comando, CancellationToken cancellationToken)
    {
        llamadas.ContarComando();
        return await actor.ObtenerAsync();
    }
}

/// <summary>El handler de <see cref="LeerAdministracion"/>: cuenta la llamada y devuelve el actor.</summary>
public sealed class LeerAdministracionHandler(IActorActual actor, LlamadasDeAdministracion llamadas) : IQueryHandler<LeerAdministracion, Actor>
{
    public async Task<Resultado<Actor>> HandleAsync(LeerAdministracion consulta, CancellationToken cancellationToken)
    {
        llamadas.ContarConsulta();
        return await actor.ObtenerAsync();
    }
}

/// <summary>El handler de <see cref="PreguntarActor"/>: devuelve el actor.</summary>
public sealed class PreguntarActorHandler(IActorActual actor) : ICommandHandler<PreguntarActor, Actor>
{
    public async Task<Resultado<Actor>> HandleAsync(PreguntarActor comando, CancellationToken cancellationToken) =>
        await actor.ObtenerAsync();
}
