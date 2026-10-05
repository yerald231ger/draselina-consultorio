using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// Solo para <c>dotnet ef migrations add</c>: crea el contexto sin arrancar la aplicación.
/// Generar una migración no abre conexión, así que la cadena no lleva contraseña ni apunta a una base real.
/// </summary>
public sealed class AgendaDbContextFactory : IDesignTimeDbContextFactory<AgendaDbContext>
{
    public AgendaDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<AgendaDbContext>();
        AgendaDbContext.Configurar(opciones, "Host=localhost;Database=consultorio");
        return new AgendaDbContext(opciones.Options);
    }
}
