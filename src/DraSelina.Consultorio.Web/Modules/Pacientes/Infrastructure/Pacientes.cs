using DraSelina.Consultorio.Pacientes.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DraSelina.Consultorio.Pacientes.Infrastructure;

/// <summary><see cref="IPacientes"/> sobre <see cref="PacientesDbContext"/>.</summary>
public sealed class Pacientes(PacientesDbContext db) : IPacientes
{
    // La igualdad la decide la collation de la columna nombre_normalizado, no el código (RN-26).
    public Task<Paciente?> BuscarAsync(Telefono telefono, NombreCompleto nombre, CancellationToken cancellationToken) =>
        db.Pacientes.FirstOrDefaultAsync(
            p => p.Telefono == telefono && p.Nombre.Normalizado == nombre.Normalizado,
            cancellationToken);

    public void Agregar(Paciente paciente) => db.Pacientes.Add(paciente);

    public async Task<GuardadoDePaciente> GuardarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return GuardadoDePaciente.Guardado;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
                                              {
                                                  SqlState: PostgresErrorCodes.UniqueViolation,
                                                  ConstraintName: PacientesDbContext.IndiceDeIdentidad,
                                              })
        {
            // El detalle de la violación (Detail) llevaría teléfono y nombre. Quedan fuera de los registros, incluido el
            // SaveChangesFailed que EF Core escribe antes de este catch, porque Npgsql oculta Detail mientras la cadena
            // de conexión no active Include Error Detail. Debe seguir desactivado (AD-23).
            return GuardadoDePaciente.IdentidadRepetida;
        }
    }
}
