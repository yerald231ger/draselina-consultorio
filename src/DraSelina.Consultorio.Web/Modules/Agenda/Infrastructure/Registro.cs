using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Features;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// Lo que el host registra de Agenda: su contexto, su puerto de transacción, su outbox, su bitácora, su candado por fecha, sus puertos, sus handlers y su único endpoint HTTP.
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
            .AddScoped<ICitasDelCalendario, CitasDelCalendario>()
            .AddScoped<ICitasDelEnlace, CitasDelEnlace>()
            .AddScoped<AgendaApi>()
            .AddScoped<IAgendaApi>(proveedor => proveedor.GetRequiredService<AgendaApi>())
            .AddScoped<ICommandHandler<AgendarCitaEnLinea, CitaAgendadaEnLinea>, AgendarCitaEnLineaHandler>()
            .AddScoped<ICommandHandler<ReprogramarCitaPorEnlace, CitaReprogramadaPorEnlace>, ReprogramarCitaPorEnlaceHandler>()
            .AddScoped<IQueryHandler<HorariosLibres, IReadOnlyList<DiaConHorarios>>, HorariosLibresHandler>()
            .AddScoped<IQueryHandler<VentanaDeReservaPublica, VentanaDeReservaVigente>, VentanaDeReservaPublicaHandler>()
            .AddScoped<IQueryHandler<ArchivoDeCalendarioPorEnlace, ArchivoDeCalendario>, ArchivoDeCalendarioPorEnlaceHandler>()
            .AddScoped<IQueryHandler<CitaPorEnlace, CitaDelEnlace>, CitaPorEnlaceHandler>()
            .AddScoped<IQueryHandler<TiposDeVisitaReservables, IReadOnlyList<TipoDeVisitaReservable>>, TiposDeVisitaReservablesHandler>();

    private static IServiceCollection AddEnlaceDeGestion(this IServiceCollection servicios)
    {
        servicios.AddOptions<OpcionesDeEnlaceDeGestion>()
            .BindConfiguration(OpcionesDeEnlaceDeGestion.Seccion)
            .ValidateOnStart();

        return servicios
            .AddSingleton<IValidateOptions<OpcionesDeEnlaceDeGestion>, ValidacionDeOpcionesDeEnlaceDeGestion>()
            .AddSingleton(proveedor => proveedor.GetRequiredService<IOptions<OpcionesDeEnlaceDeGestion>>().Value.ClaveDeGestion());
    }

    /// <summary>
    /// Lo que Agenda sirve por HTTP, solo donde un formulario no alcanza (AD-10): el archivo de calendario de una Cita,
    /// en <see cref="RutaDelCalendario"/>. Se llega con el token del Enlace de gestión, nunca con el id (AD-19). Es una
    /// lectura sin actor, bitácora, transacción ni eventos (Gerardo, 2026-11-03). Lo que no sea una Cita Agendada futura
    /// responde 404 sin decir por qué (RN-22). La telemetría enmascara la ruta (AD-23, ServiceDefaults). Lleva el límite
    /// de frecuencia de Tu cita (AD-22): el host da el nombre de la política, que define la interfaz.
    /// </summary>
    /// <param name="politicaDeFrecuencia">La política de límite de frecuencia por IP que pide el endpoint.</param>
    public static IEndpointRouteBuilder MapAgenda(this IEndpointRouteBuilder rutas, string politicaDeFrecuencia)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(politicaDeFrecuencia);

        rutas.MapGet(RutaDelCalendario, async (
            string token,
            IQueryHandler<ArchivoDeCalendarioPorEnlace, ArchivoDeCalendario> consulta,
            HttpContext contexto,
            CancellationToken cancelacion) =>
        {
            // Lleva el Enlace de gestión: ni el navegador ni un intermediario lo guardan.
            contexto.Response.Headers.CacheControl = "no-store";

            var resultado = await consulta.HandleAsync(new ArchivoDeCalendarioPorEnlace(token), cancelacion);
            return resultado.EsExito
                ? Results.File(
                    Encoding.UTF8.GetBytes(resultado.Valor.Contenido),
                    ArchivoDeCalendario.TipoDeContenido,
                    ArchivoDeCalendario.NombreDeArchivo)
                : Results.NotFound();
        }).RequireRateLimiting(politicaDeFrecuencia);

        return rutas;
    }

    /// <summary>La ruta del archivo de calendario: bajo el Enlace de gestión.</summary>
    public const string RutaDelCalendario = "/c/{token}/calendario.ics";
}
