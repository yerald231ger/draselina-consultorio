using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DraSelina.Consultorio.Pacientes.Infrastructure;

/// <summary>
/// Solo para <c>dotnet ef migrations add</c>: crea el contexto sin arrancar la aplicación.
/// Generar una migración no abre conexión, así que la cadena no lleva contraseña ni apunta a una base real.
/// </summary>
public sealed class PacientesDbContextFactory : IDesignTimeDbContextFactory<PacientesDbContext>
{
    public PacientesDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<PacientesDbContext>();
        PacientesDbContext.Configurar(opciones, "Host=localhost;Database=consultorio");
        return new PacientesDbContext(opciones.Options);
    }
}
