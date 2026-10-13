using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// Lo que el host registra de Agenda: su contexto, su puerto de transacción, su outbox y sus handlers. Cada
/// handler se registra aquí, uno por línea, por su interfaz (AD-10):
/// <c>.AddScoped&lt;ICommandHandler&lt;MiComando, T&gt;, MiComandoHandler&gt;()</c>, y el de un evento:
/// <c>.AddScoped&lt;IEventHandler&lt;MiEvento&gt;, MiHandler&gt;()</c>.
/// </summary>
public static class Registro
{
    public static IServiceCollection AddAgenda(this IServiceCollection servicios, string cadenaDeConexion) =>
        servicios
            .AddDbContext<AgendaDbContext>(opciones => AgendaDbContext.Configurar(opciones, cadenaDeConexion))
            .AddScoped<ITransaccionDeAgenda, TransaccionDeAgenda>()
            .AddScoped<IOutboxDeAgenda, OutboxDeAgenda>();
}
