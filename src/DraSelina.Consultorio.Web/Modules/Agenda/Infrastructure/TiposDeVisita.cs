using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ITiposDeVisita"/> sobre <see cref="AgendaDbContext"/>.</summary>
public sealed class TiposDeVisita(AgendaDbContext db) : ITiposDeVisita
{
    public Task<TipoDeVisita?> ObtenerAsync(TipoDeVisitaId id, CancellationToken cancellationToken) =>
        db.TiposDeVisita.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<TipoDeVisita?> ObtenerParaCambiarAsync(TipoDeVisitaId id, CancellationToken cancellationToken) =>
        db.TiposDeVisita.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<int> ContarActivosParaCambiarAsync(CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Contar los tipos activos para cambiarlos necesita una transacción abierta.");
        }

        var activos = await db.TiposDeVisita
            .FromSql($"select * from agenda.tipo_de_visita where activo for update")
            .AsNoTracking()
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        return activos.Count;
    }

    public void Agregar(TipoDeVisita tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        db.TiposDeVisita.Add(tipo);
    }

    public Task GuardarAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
