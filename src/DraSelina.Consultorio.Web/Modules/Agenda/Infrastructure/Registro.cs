using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Features;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// Lo que el host registra de Agenda: su contexto, su puerto de transacción, su outbox, su bitácora, su candado por fecha, sus puertos y sus handlers.
/// Cada handler se registra aquí, uno por línea, por su interfaz (AD-10):
/// <c>.AddScoped&lt;ICommandHandler&lt;MiComando, T&gt;, MiComandoHandler&gt;()</c>, y el de un evento:
/// <c>.AddScoped&lt;IEventHandler&lt;MiEvento&gt;, MiHandler&gt;()</c>.
/// La clave y la URL base del Enlace de gestión se leen de la sección <c>EnlaceDeGestion</c> de la configuración y se
/// validan al arrancar el host (AD-19): sin ellas la web no arranca.
/// </summary>
public static class Registro
{
    public static IServiceCollection AddAgenda(this IServiceCollection servicios, string cadenaDeConexion) =>
        servicios
            .AddDbContext<AgendaDbContext>(opciones => AgendaDbContext.Configurar(opciones, cadenaDeConexion))
            .AddEnlaceDeGestion()
            .AddScoped<ITransaccionDeAgenda, TransaccionDeAgenda>()
            .AddScoped<IOutboxDeAgenda, OutboxDeAgenda>()
            .AddScoped<IBitacoraDeAgenda, BitacoraDeAgenda>()
            .AddScoped<ICandadoDeFecha, CandadoDeFecha>()
            .AddScoped<ICitas, Citas>()
            .AddScoped<ITiposDeVisita, TiposDeVisita>()
            .AddScoped<IAgendaAtencion, AgendaAtencionPersistida>()
            .AddScoped<IReglasDeReserva, ReglasDeReservaPersistidas>()
            .AddScoped<ICitasActivas, CitasActivas>()
            .AddScoped<ITiposDeVisitaReservables, TiposDeVisitaReservablesPersistidos>()
            .AddScoped<ICommandHandler<AgendarCitaEnLinea, CitaAgendadaEnLinea>, AgendarCitaEnLineaHandler>()
            .AddScoped<IQueryHandler<HorariosLibres, IReadOnlyList<DiaConHorarios>>, HorariosLibresHandler>()
            .AddScoped<IQueryHandler<TiposDeVisitaReservables, IReadOnlyList<TipoDeVisitaReservable>>, TiposDeVisitaReservablesHandler>();

    private static IServiceCollection AddEnlaceDeGestion(this IServiceCollection servicios)
    {
        servicios.AddOptions<OpcionesDeEnlaceDeGestion>()
            .BindConfiguration(OpcionesDeEnlaceDeGestion.Seccion)
            .ValidateOnStart();

        return servicios
            .AddSingleton<IValidateOptions<OpcionesDeEnlaceDeGestion>, ValidacionDeOpcionesDeEnlaceDeGestion>()
            .AddSingleton(proveedor => proveedor.GetRequiredService<IOptions<OpcionesDeEnlaceDeGestion>>().Value.ClaveDeGestion())
            .AddSingleton<IAgendaApi, AgendaApi>();
    }
}
