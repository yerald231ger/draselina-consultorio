using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.Pacientes.Infrastructure;

/// <summary>Lo que el host registra de la infraestructura de Pacientes.</summary>
public static class Registro
{
    public static IServiceCollection AddPacientes(this IServiceCollection servicios, string cadenaDeConexion) =>
        // Las opciones son las mismas para toda la aplicación: se registran una sola vez (singleton). Así el host
        // puede dar el contexto a un handler sin pedirlo al contenedor en cada mensaje. El contexto sigue siendo
        // uno por ámbito.
        servicios.AddDbContext<PacientesDbContext>(
            opciones => PacientesDbContext.Configurar(opciones, cadenaDeConexion),
            optionsLifetime: ServiceLifetime.Singleton);
}
