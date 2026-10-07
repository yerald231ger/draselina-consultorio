using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore.Storage;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>El puerto de transacción de Agenda sobre su contexto: la transacción es la de <see cref="AgendaDbContext"/>.</summary>
public sealed class TransaccionDeAgenda(AgendaDbContext db) : ITransaccionDeAgenda
{
    public async Task<ITransaccionAbiertaDeAgenda> AbrirAsync(CancellationToken cancellationToken) =>
        new Abierta(db, await db.Database.BeginTransactionAsync(cancellationToken));

    private sealed class Abierta(AgendaDbContext db, IDbContextTransaction transaccion) : ITransaccionAbiertaDeAgenda
    {
        private bool _confirmada;

        public async Task ConfirmarAsync(CancellationToken cancellationToken)
        {
            await transaccion.CommitAsync(cancellationToken);
            _confirmada = true;
        }

        public async Task DeshacerAsync(CancellationToken cancellationToken)
        {
            await transaccion.RollbackAsync(cancellationToken);
            Olvidar();
        }

        // Soltar una transacción sin confirmar la deshace: así un handler que lanza no la deja abierta, aunque
        // el ámbito del contenedor siga vivo (en administración dura lo que el circuito).
        public async ValueTask DisposeAsync()
        {
            try
            {
                await transaccion.DisposeAsync();
            }
            finally
            {
                // También si soltarla lanza (por ejemplo, con la conexión rota).
                if (!_confirmada)
                {
                    Olvidar();
                }
            }
        }

        // Lo deshecho tampoco queda en el contexto: sin esto, el siguiente comando del mismo ámbito guardaría o
        // leería los cambios de uno que no se confirmó.
        private void Olvidar() => db.ChangeTracker.Clear();
    }
}
