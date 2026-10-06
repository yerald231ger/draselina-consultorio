using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Wolverine;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>
/// Comando de prueba: guarda una <see cref="Anotacion"/> y publica <see cref="AnotacionHecha"/>. Devuelve el id.
/// Como todo comando, es una clase con propiedades <c>set</c>.
/// </summary>
public sealed class Anotar : ICommand<Guid>
{
    public Guid Id { get; set; }

    public string Texto { get; set; } = "";

    /// <summary>
    /// Nulo: el evento se publica de inmediato. Con valor: se programa para dentro de ese tiempo, y mientras
    /// tanto se queda guardado en el esquema <c>wolverine</c>, donde una prueba lo puede ver sin carreras.
    /// </summary>
    public TimeSpan? AvisarEn { get; set; }

    public Desenlace Desenlace { get; set; } = Desenlace.Exito;
}

/// <summary>Cómo termina el handler de <see cref="Anotar"/>.</summary>
public enum Desenlace
{
    /// <summary>Guarda, publica y devuelve éxito.</summary>
    Exito,

    /// <summary>Guarda, publica y después devuelve un fallo esperado.</summary>
    FalloAlFinal,

    /// <summary>Guarda, publica y después lanza: el fallo que nadie espera.</summary>
    LanzarAlFinal,

    /// <summary>Agrega la anotación, no guarda y devuelve éxito.</summary>
    ExitoSinGuardar,
}

/// <summary>El conjunto cerrado de códigos de fallo esperado de las anotaciones de prueba.</summary>
public static class CodigosDeAnotacion
{
    public const string TextoRepetido = "anotacion.texto_repetido";
    public const string RechazadaAlFinal = "anotacion.rechazada_al_final";
    public const string NoExiste = "anotacion.no_existe";
}

/// <summary>Evento de prueba. Puede ser <c>record</c>; lleva solo el id.</summary>
public sealed record AnotacionHecha(Guid AnotacionId);

/// <summary>La excepción que lanza <see cref="Anotar"/> con <see cref="Desenlace.LanzarAlFinal"/>.</summary>
public sealed class FalloInesperadoDePrueba(Guid anotacionId) : Exception($"Fallo inesperado de prueba en la anotación {anotacionId}.");

/// <summary>
/// Un handler por comando. Llega a la base por su puerto y guarda de forma explícita. No abre ni confirma ninguna
/// transacción: eso lo hace el host.
/// </summary>
public static class AnotarHandler
{
    public static async Task<Resultado<Guid>> Handle(Anotar comando, IAnotaciones anotaciones, IMessageBus bus, CancellationToken cancelacion)
    {
        // Fallo esperado antes de cambiar nada, con un código del conjunto cerrado.
        if (await anotaciones.ExisteTextoAsync(comando.Texto, cancelacion))
        {
            return Resultado.Fallo(CodigosDeAnotacion.TextoRepetido).Como<Guid>();
        }

        anotaciones.Agregar(new Anotacion { Id = comando.Id, Texto = comando.Texto });
        if (comando.Desenlace == Desenlace.ExitoSinGuardar)
        {
            return comando.Id;
        }

        await anotaciones.GuardarAsync(cancelacion);

        var evento = new AnotacionHecha(comando.Id);
        if (comando.AvisarEn is { } espera)
        {
            await bus.ScheduleAsync(evento, espera);
        }
        else
        {
            await bus.PublishAsync(evento);
        }

        return comando.Desenlace switch
        {
            Desenlace.FalloAlFinal => Resultado.Fallo(CodigosDeAnotacion.RechazadaAlFinal).Como<Guid>(),
            Desenlace.LanzarAlFinal => throw new FalloInesperadoDePrueba(comando.Id),
            _ => comando.Id,
        };
    }
}

/// <summary>
/// El handler del evento: deja un rastro y anota si la anotación ya estaba confirmada. Corre en su propia
/// transacción, que el host confirma salvo excepción.
/// </summary>
public static class AnotacionHechaHandler
{
    public static async Task Handle(AnotacionHecha evento, IAnotaciones anotaciones, CancellationToken cancelacion)
    {
        // Un mensaje puede entregarse más de una vez: el rastro se deja una sola.
        if (await anotaciones.ExisteRastroAsync(evento.AnotacionId, cancelacion))
        {
            return;
        }

        anotaciones.Agregar(new RastroDeAnotacion
        {
            Id = evento.AnotacionId,
            AnotacionConfirmada = await anotaciones.ExisteAsync(evento.AnotacionId, cancelacion),
        });
        await anotaciones.GuardarAsync(cancelacion);
    }
}

/// <summary>Un comando al que nadie le escribió handler.</summary>
public sealed class ComandoSinHandler : ICommand<int>;
