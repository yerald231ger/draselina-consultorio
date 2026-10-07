using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.Pacientes.Infrastructure;

/// <summary>Lo que el host registra de la infraestructura de Pacientes.</summary>
public static class Registro
{
    public static IServiceCollection AddPacientes(this IServiceCollection servicios, string cadenaDeConexion) =>
        servicios.AddDbContext<PacientesDbContext>(opciones => PacientesDbContext.Configurar(opciones, cadenaDeConexion));
}
