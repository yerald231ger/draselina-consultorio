using DraSelina.Consultorio.BuildingBlocks.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>
/// Da de alta los tipos de prueba como se da de alta un módulo: su contexto, su puerto de transacción, su outbox,
/// sus puertos y sus handlers, cada handler por su interfaz. <see cref="ComandoSinHandler"/> no tiene handler a
/// propósito, y a <see cref="EventoQueNadieEscucha"/> no lo escucha nadie.
/// </summary>
public static class Registro
{
    public static IServiceCollection AddPruebas(this IServiceCollection servicios, string cadenaDeConexion) =>
        servicios
            .AddDbContext<PruebasDbContext>(opciones => opciones.UseNpgsql(cadenaDeConexion))
            .AddScoped<ITransaccionDePruebas, TransaccionDePruebas>()
            .AddScoped<IOutboxDePruebas, OutboxDePruebas>()
            .AddScoped<IAnotaciones, Anotaciones>()
            .AddScoped<IEntregasRecibidas, EntregasRecibidas>()
            .AddSingleton<LlamadasDeInscribir>()
            .AddScoped<ICommandHandler<Anotar, Guid>, AnotarHandler>()
            .AddScoped<ICommandHandler<Inscribir, Guid>, InscribirHandler>()
            .AddScoped<IQueryHandler<LeerAnotacion, AnotacionLeida>, LeerAnotacionHandler>()
            .AddScoped<IEventHandler<AnotacionHecha>, AlHacerseUnaAnotacion>()
            .AddScoped<IEventHandler<EventoDePrueba>, EscuchaDePrueba>()
            .AddScoped<IEventHandler<EventoConDosHandlers>, PrimeraEscucha>()
            .AddScoped<IEventHandler<EventoConDosHandlers>, SegundaEscucha>();
}
