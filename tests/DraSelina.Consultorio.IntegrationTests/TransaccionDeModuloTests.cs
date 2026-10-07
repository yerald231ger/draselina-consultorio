using DraSelina.Consultorio.Agenda.Features;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.Notificaciones.Features;
using DraSelina.Consultorio.Notificaciones.Infrastructure;
using DraSelina.Consultorio.Pacientes.Features;
using DraSelina.Consultorio.Pacientes.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Cada módulo entrega su puerto de transacción, registrado por su <c>Registro</c> y montado sobre su contexto.
/// Se pide al contenedor de la web en memoria y se usa contra la base migrada: abrir y confirmar, abrir y deshacer,
/// y abrir y soltar sin confirmar.
/// </summary>
public sealed class TransaccionDeModuloTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task La_transaccion_de_Agenda_abre_confirma_y_deshace()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        var puerto = ambito.ServiceProvider.GetRequiredService<ITransaccionDeAgenda>();
        var db = ambito.ServiceProvider.GetRequiredService<AgendaDbContext>();

        await using (var tx = await puerto.AbrirAsync(Cancelacion))
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            await tx.ConfirmarAsync(Cancelacion);
        }

        Assert.Null(db.Database.CurrentTransaction);

        await using (var tx = await puerto.AbrirAsync(Cancelacion))
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            await tx.DeshacerAsync(Cancelacion);
        }

        Assert.Null(db.Database.CurrentTransaction);

        await using (await puerto.AbrirAsync(Cancelacion))
        {
            Assert.NotNull(db.Database.CurrentTransaction);
        }

        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task La_transaccion_de_Pacientes_abre_confirma_y_deshace()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        var puerto = ambito.ServiceProvider.GetRequiredService<ITransaccionDePacientes>();
        var db = ambito.ServiceProvider.GetRequiredService<PacientesDbContext>();

        await using (var tx = await puerto.AbrirAsync(Cancelacion))
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            await tx.ConfirmarAsync(Cancelacion);
        }

        Assert.Null(db.Database.CurrentTransaction);

        await using (var tx = await puerto.AbrirAsync(Cancelacion))
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            await tx.DeshacerAsync(Cancelacion);
        }

        Assert.Null(db.Database.CurrentTransaction);

        await using (await puerto.AbrirAsync(Cancelacion))
        {
            Assert.NotNull(db.Database.CurrentTransaction);
        }

        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task La_transaccion_de_Notificaciones_abre_confirma_y_deshace()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        var puerto = ambito.ServiceProvider.GetRequiredService<ITransaccionDeNotificaciones>();
        var db = ambito.ServiceProvider.GetRequiredService<NotificacionesDbContext>();

        await using (var tx = await puerto.AbrirAsync(Cancelacion))
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            await tx.ConfirmarAsync(Cancelacion);
        }

        Assert.Null(db.Database.CurrentTransaction);

        await using (var tx = await puerto.AbrirAsync(Cancelacion))
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            await tx.DeshacerAsync(Cancelacion);
        }

        Assert.Null(db.Database.CurrentTransaction);

        await using (await puerto.AbrirAsync(Cancelacion))
        {
            Assert.NotNull(db.Database.CurrentTransaction);
        }

        Assert.Null(db.Database.CurrentTransaction);
    }
}
