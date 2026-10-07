using Microsoft.EntityFrameworkCore.Storage;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>El puerto de transacción de los tipos de prueba, con la misma forma que el de un módulo.</summary>
public interface ITransaccionDePruebas
{
    Task<ITransaccionAbiertaDePruebas> AbrirAsync(CancellationToken cancellationToken);
}

/// <summary>Una transacción abierta de los tipos de prueba. Soltarla sin confirmar la deshace.</summary>
public interface ITransaccionAbiertaDePruebas : IAsyncDisposable
{
    Task ConfirmarAsync(CancellationToken cancellationToken);

    Task DeshacerAsync(CancellationToken cancellationToken);
}

/// <summary>El puerto sobre <see cref="PruebasDbContext"/>, igual que el de un módulo sobre su contexto.</summary>
public sealed class TransaccionDePruebas(PruebasDbContext db) : ITransaccionDePruebas
{
    public async Task<ITransaccionAbiertaDePruebas> AbrirAsync(CancellationToken cancellationToken) =>
        new Abierta(db, await db.Database.BeginTransactionAsync(cancellationToken));

    private sealed class Abierta(PruebasDbContext db, IDbContextTransaction transaccion) : ITransaccionAbiertaDePruebas
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
            db.ChangeTracker.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await transaccion.DisposeAsync();
            }
            finally
            {
                if (!_confirmada)
                {
                    db.ChangeTracker.Clear();
                }
            }
        }
    }
}
