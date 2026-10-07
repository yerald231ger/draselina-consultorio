using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>Lo que el host registra de la infraestructura de Agenda.</summary>
public static class Registro
{
    public static IServiceCollection AddAgenda(this IServiceCollection servicios, string cadenaDeConexion) =>
        servicios.AddDbContext<AgendaDbContext>(opciones => AgendaDbContext.Configurar(opciones, cadenaDeConexion));
}
