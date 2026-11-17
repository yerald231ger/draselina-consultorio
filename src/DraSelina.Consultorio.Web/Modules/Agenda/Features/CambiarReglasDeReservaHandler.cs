using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Cambia las reglas de reserva (CAP-16, FR-19). Orquesta y no decide (T-13): abre la transacción, lee la única fila y
/// la cambia con <see cref="ReglasDeReserva.Cambiar"/>, que decide qué valores sirven; su rechazo va bajo la propiedad
/// del comando. Ya guardadas, y en la misma transacción, deja la bitácora <c>ReglasDeReserva/Cambiada</c> a nombre del
/// Administrador con su id y los valores antes y después (RN-24, AD-16). Los mismos valores son éxito sin bitácora. La
/// siguiente consulta que las lea ya usa los nuevos: nadie las guarda en memoria.
/// </summary>
public sealed class CambiarReglasDeReservaHandler(
    ITransaccionDeAgenda transaccion,
    IReglasDeReserva reglasDeReserva,
    IBitacoraDeAgenda bitacora,
    IActorActual actorActual,
    IClock reloj) : ICommandHandler<CambiarReglasDeReserva, ReglasDeReservaValores>
{
    public async Task<Resultado<ReglasDeReservaValores>> HandleAsync(CambiarReglasDeReserva comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        // La validación de forma (AD-12) ya garantizó los cuatro valores.
        var nuevos = new ReglasDeReservaValores(
            comando.AnticipacionMinimaDias!.Value, comando.VentanaMeses!.Value,
            comando.UmbralCancelacionTardiaMin!.Value, comando.TopeSemanalPorTelefono!.Value);
        var ahora = reloj.UtcNow;

        // El comando es solo del Administrador (AD-22): su id va a la bitácora (RN-24).
        var actor = await actorActual.ObtenerAsync();
        var actorId = await actorActual.IdDelAdministradorAsync();

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        var reglas = await reglasDeReserva.ObtenerAsync(cancellationToken);
        var antes = ReglasDeReservaVigentesHandler.Valores(reglas);
        if (antes == nuevos)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Exito(antes);
        }

        if (reglas.Cambiar(nuevos.AnticipacionMinimaDias, nuevos.VentanaMeses, nuevos.UmbralCancelacionTardiaMin, nuevos.TopeSemanalPorTelefono)
            is { } rechazo)
        {
            await tx.DeshacerAsync(cancellationToken);
            var (propiedad, mensaje) = Error(rechazo);
            return Resultado.Invalido(propiedad, mensaje).Como<ReglasDeReservaValores>();
        }

        await reglasDeReserva.GuardarAsync(reglas, cancellationToken);
        await bitacora.AnotarAsync(
            new EntradaDeBitacora(
                ahora, actor, actorId, EntidadDeBitacora.ReglasDeReserva, "1", AccionDeBitacora.Cambiada,
                new { antes = Detalle(antes), despues = Detalle(nuevos) }),
            cancellationToken);

        await tx.ConfirmarAsync(cancellationToken);

        return Resultado.Exito(nuevos);
    }

    // En camelCase, como el resto de la bitácora.
    private static object Detalle(ReglasDeReservaValores valores) => new
    {
        anticipacionMinimaDias = valores.AnticipacionMinimaDias,
        ventanaMeses = valores.VentanaMeses,
        umbralCancelacionTardiaMin = valores.UmbralCancelacionTardiaMin,
        topeSemanalPorTelefono = valores.TopeSemanalPorTelefono,
    };

    private static (string Propiedad, string Mensaje) Error(RechazoDeReglasDeReserva rechazo) => rechazo switch
    {
        RechazoDeReglasDeReserva.AnticipacionMinimaNegativa =>
            (nameof(CambiarReglasDeReserva.AnticipacionMinimaDias), CambiarReglasDeReserva.MensajeDeAnticipacion),
        RechazoDeReglasDeReserva.VentanaNoPositiva => (nameof(CambiarReglasDeReserva.VentanaMeses), CambiarReglasDeReserva.MensajeDeVentana),
        RechazoDeReglasDeReserva.UmbralNegativo =>
            (nameof(CambiarReglasDeReserva.UmbralCancelacionTardiaMin), CambiarReglasDeReserva.MensajeDeUmbral),
        RechazoDeReglasDeReserva.TopeNoPositivo => (nameof(CambiarReglasDeReserva.TopeSemanalPorTelefono), CambiarReglasDeReserva.MensajeDeTope),
        _ => throw new ArgumentOutOfRangeException(nameof(rechazo), rechazo, "Rechazo de reglas sin mensaje."),
    };
}
