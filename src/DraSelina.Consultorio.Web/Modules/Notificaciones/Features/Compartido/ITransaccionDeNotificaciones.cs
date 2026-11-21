namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// El puerto por el que el handler de un comando de Notificaciones abre la transacción de su módulo. Un comando, una
/// transacción: el handler la abre, guarda de forma explícita por sus puertos y la confirma.
/// </summary>
public interface ITransaccionDeNotificaciones
{
    /// <summary>Abre la transacción. Se suelta con <c>await using</c>: al soltarla sin confirmar, se deshace.</summary>
    Task<ITransaccionAbiertaDeNotificaciones> AbrirAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Una transacción abierta de Notificaciones. Lo que no se confirma no queda: soltarla sin haber confirmado la deshace,
/// también cuando el handler lanza. Una transacción abierta termina una sola vez: se llama a
/// <see cref="ConfirmarAsync"/> o a <see cref="DeshacerAsync"/>, nunca a los dos ni dos veces al mismo; una segunda
/// llamada lanza la excepción del proveedor de la base.
/// </summary>
public interface ITransaccionAbiertaDeNotificaciones : IAsyncDisposable
{
    /// <summary>Confirma todo lo guardado desde que se abrió. A lo más una vez, y nunca después de deshacer.</summary>
    Task ConfirmarAsync(CancellationToken cancellationToken);

    /// <summary>Deshace todo lo guardado desde que se abrió. Va antes de devolver un <c>Resultado</c> no exitoso.
    /// A lo más una vez, y nunca después de confirmar.</summary>
    Task DeshacerAsync(CancellationToken cancellationToken);
}
