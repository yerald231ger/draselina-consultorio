using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>
/// Comando de prueba: guarda una <see cref="Anotacion"/>, publica <see cref="AnotacionHecha"/> y devuelve el id de
/// la anotación. Como todo comando, es una clase con propiedades <c>set</c>.
/// </summary>
public sealed class Anotar : ICommand<Guid>
{
    public Guid Id { get; set; }

    public string Texto { get; set; } = "";

    public Desenlace Desenlace { get; set; } = Desenlace.Exito;
}

/// <summary>Cómo termina el handler de <see cref="Anotar"/>.</summary>
public enum Desenlace
{
    /// <summary>Guarda, confirma y devuelve éxito.</summary>
    Exito,

    /// <summary>Guarda, publica y después descubre un fallo esperado: deshace y lo devuelve.</summary>
    FalloAlFinal,

    /// <summary>Agrega la anotación, descubre un fallo esperado antes de guardar: deshace y lo devuelve.</summary>
    FalloSinGuardar,

    /// <summary>Guarda, publica y después lanza: el fallo que nadie espera.</summary>
    LanzarAlFinal,

    /// <summary>Guarda, publica, no confirma y devuelve éxito: un handler mal escrito.</summary>
    ExitoSinConfirmar,
}

/// <summary>El conjunto cerrado de códigos de fallo esperado de las anotaciones de prueba.</summary>
public static class CodigosDeAnotacion
{
    public const string TextoRepetido = "anotacion.texto_repetido";
    public const string RechazadaAlFinal = "anotacion.rechazada_al_final";
    public const string NoExiste = "anotacion.no_existe";
}

/// <summary>La excepción que lanza <see cref="Anotar"/> con <see cref="Desenlace.LanzarAlFinal"/>.</summary>
public sealed class FalloInesperadoDePrueba(Guid anotacionId) : Exception($"Fallo inesperado de prueba en la anotación {anotacionId}.");

/// <summary>
/// Un handler por comando. Abre la transacción por su puerto, llega a la base por otro puerto, guarda de forma
/// explícita, publica su evento por el outbox y confirma: la anotación y el evento quedan juntos, o ninguno. Toda
/// salida con un <c>Resultado</c> no exitoso deshace antes de devolver.
/// </summary>
public sealed class AnotarHandler(
    ITransaccionDePruebas transaccion,
    IAnotaciones anotaciones,
    IOutboxDePruebas outbox,
    IClock reloj) : ICommandHandler<Anotar, Guid>
{
    public async Task<Resultado<Guid>> HandleAsync(Anotar comando, CancellationToken cancellationToken)
    {
        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // Fallo esperado antes de cambiar nada, con un código del conjunto cerrado.
        if (await anotaciones.ExisteTextoAsync(comando.Texto, cancellationToken))
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAnotacion.TextoRepetido).Como<Guid>();
        }

        anotaciones.Agregar(new Anotacion { Id = comando.Id, Texto = comando.Texto });
        if (comando.Desenlace == Desenlace.FalloSinGuardar)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAnotacion.RechazadaAlFinal).Como<Guid>();
        }

        await anotaciones.GuardarAsync(cancellationToken);

        // El evento se guarda en la misma transacción que la anotación.
        var eventoId = Guid.CreateVersion7();
        var ocurridoEn = reloj.UtcNow;
        await outbox.PublicarAsync(
            new AnotacionHecha(eventoId, ocurridoEn, comando.Id), eventoId, claveDeOrden: comando.Id, ocurridoEn, cancellationToken);

        switch (comando.Desenlace)
        {
            case Desenlace.FalloAlFinal:
                await tx.DeshacerAsync(cancellationToken);
                return Resultado.Fallo(CodigosDeAnotacion.RechazadaAlFinal).Como<Guid>();

            case Desenlace.LanzarAlFinal:
                throw new FalloInesperadoDePrueba(comando.Id);

            case Desenlace.ExitoSinConfirmar:
                return comando.Id;
        }

        await tx.ConfirmarAsync(cancellationToken);
        return comando.Id;
    }
}

/// <summary>Un comando al que nadie le escribió handler.</summary>
public sealed class ComandoSinHandler : ICommand<int>;
