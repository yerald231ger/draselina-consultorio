using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Pacientes.Contracts;
using DraSelina.Consultorio.Pacientes.Domain;
using DraSelina.Consultorio.Pacientes.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.Pacientes.Infrastructure;

/// <summary>
/// Lo que el host registra de Pacientes: su contexto, su puerto de transacción, sus puertos, su API y sus handlers. Cada handler se
/// registra aquí, uno por línea, por su interfaz (AD-10):
/// <c>.AddScoped&lt;ICommandHandler&lt;MiComando, T&gt;, MiComandoHandler&gt;()</c>.
/// </summary>
public static class Registro
{
    public static IServiceCollection AddPacientes(this IServiceCollection servicios, string cadenaDeConexion) =>
        servicios
            .AddDbContext<PacientesDbContext>(opciones => PacientesDbContext.Configurar(opciones, cadenaDeConexion))
            .AddScoped<ITransaccionDePacientes, TransaccionDePacientes>()
            .AddScoped<IPacientes, Pacientes>()
            .AddScoped<IBusquedaDePacientes, BusquedaDePacientes>()
            .AddScoped<IPacientesApi, PacientesApi>()
            .AddScoped<IQueryHandler<BuscarPacientes, PacientesEncontrados>, BuscarPacientesHandler>();
}
