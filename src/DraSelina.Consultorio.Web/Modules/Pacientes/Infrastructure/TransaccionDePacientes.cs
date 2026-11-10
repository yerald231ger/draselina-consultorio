using DraSelina.Consultorio.Pacientes.Features;
using Microsoft.EntityFrameworkCore.Storage;

namespace DraSelina.Consultorio.Pacientes.Infrastructure;

/// <summary>El puerto de transacción de Pacientes sobre su contexto: la transacción es la de <see cref="PacientesDbContext"/>.</summary>
public sealed class TransaccionDePacientes(PacientesDbContext db) : ITransaccionDePacientes
{
    // En administración el contexto dura lo que el circuito: lo rastreado por un comando ya confirmado puede estar
    // viejo (otro ámbito lo cambió después), y una lectura rastreada lo devolvería tal cual aunque la fila se acabe de
    // bloquear. Cada comando empieza sin nada rastreado: lee lo guardado.
    public async Task<ITransaccionAbiertaDePacientes> AbrirAsync(CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        return new Abierta(db, await db.Database.BeginTransactionAsync(cancellationToken));
    }

    private sealed class Abierta(PacientesDbContext db, IDbContextTransaction transaccion) : ITransaccionAbiertaDePacientes
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
