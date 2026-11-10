using DraSelina.Consultorio.Kernel;
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

    // La misma igualdad de BuscarAsync, sin rastrear: solo el id.
    public async Task<PatientId?> IdAsync(Telefono telefono, NombreCompleto nombre, CancellationToken cancellationToken) =>
        await db.Pacientes
            .AsNoTracking()
            .Where(p => p.Telefono == telefono && p.Nombre.Normalizado == nombre.Normalizado)
            .Select(p => (PatientId?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

    // Por el índice ix_paciente_telefono (AD-13).
    public async Task<IReadOnlyList<PatientId>> ConTelefonoAsync(Telefono telefono, CancellationToken cancellationToken) =>
        await db.Pacientes
            .AsNoTracking()
            .Where(p => p.Telefono == telefono)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

    public Task<bool> TieneCorreoAsync(PatientId paciente, CancellationToken cancellationToken) =>
        db.Pacientes.AsNoTracking().AnyAsync(p => p.Id == paciente && p.Correo != null, cancellationToken);

    public Task<string?> NombreAsync(PatientId paciente, CancellationToken cancellationToken) =>
        db.Pacientes.AsNoTracking()
            .Where(p => p.Id == paciente)
            .Select(p => (string?)p.Nombre.Original)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<(string Nombre, string? Correo)?> ContactosAsync(PatientId paciente, CancellationToken cancellationToken)
    {
        var encontrado = await db.Pacientes.AsNoTracking()
            .Where(p => p.Id == paciente)
            .Select(p => new { p.Nombre.Original, p.Correo })
            .SingleOrDefaultAsync(cancellationToken);

        return encontrado is null ? null : (encontrado.Original, encontrado.Correo?.Valor);
    }

    // Una sola consulta, por la llave primaria: id = ANY(@ids).
    public async Task<IReadOnlyDictionary<PatientId, (string Nombre, string? Correo)>> ContactosAsync(
        IReadOnlyCollection<PatientId> ids, CancellationToken cancellationToken)
    {
        var distintos = ids.Distinct().ToList();
        var encontrados = await db.Pacientes.AsNoTracking()
            .Where(p => distintos.Contains(p.Id))
            .Select(p => new { p.Id, p.Nombre.Original, p.Correo })
            .ToListAsync(cancellationToken);

        return encontrados.ToDictionary(e => e.Id, e => (e.Original, e.Correo?.Valor));
    }

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
