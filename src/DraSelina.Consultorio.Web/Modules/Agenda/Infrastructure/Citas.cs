using DraSelina.Consultorio.Agenda.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ICitas"/> sobre <see cref="AgendaDbContext"/>.</summary>
public sealed class Citas(AgendaDbContext db) : ICitas
{
    public void Agregar(Cita cita) => db.Citas.Add(cita);

    public async Task<GuardadoDeCita> GuardarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return GuardadoDeCita.Guardada;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
                                              {
                                                  SqlState: PostgresErrorCodes.ExclusionViolation,
                                                  ConstraintName: AgendaDbContext.RestriccionSinTraslape,
                                              })
        {
            // 23P01: el traslape vuelve como resultado, nunca como excepción (AD-8, T-22).
            return GuardadoDeCita.HorarioOcupado;
        }
    }
}
