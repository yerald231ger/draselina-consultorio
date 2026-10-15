using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// Valida la forma de todo comando antes de su handler (AD-12). Aplica las DataAnnotations del comando, y su
/// <see cref="IValidatableObject"/> si lo implementa. Si no pasa, devuelve <see cref="Resultado"/> inválido con
/// cada error bajo el nombre de su propiedad y el handler no se ejecuta; si pasa, delega. Los mensajes salen tal
/// cual del atributo (<c>ErrorMessage</c>): aquí no se traducen. Las consultas no se validan.
/// </summary>
public sealed class ValidacionDeComandos<TComando, T>(ICommandHandler<TComando, T> siguiente)
    : ICommandHandler<TComando, T>
    where TComando : ICommand<T>
{
    /// <summary>La propiedad de un error que no es de ningún campo: la misma convención que Blazor para el modelo.</summary>
    public const string ErrorDelObjeto = "";

    /// <summary>El mensaje de un error que llega sin mensaje.</summary>
    public const string MensajePorDefecto = "El valor no es válido.";

    /// <summary>El handler que envuelve.</summary>
    public ICommandHandler<TComando, T> Siguiente => siguiente;

    public Task<Resultado<T>> HandleAsync(TComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var errores = new List<ValidationResult>();
        if (Validator.TryValidateObject(comando, new ValidationContext(comando), errores, validateAllProperties: true))
        {
            return siguiente.HandleAsync(comando, cancellationToken);
        }

        return Task.FromResult(Resultado.Invalido(PorPropiedad(errores)).Como<T>());
    }

    // Un error con varios miembros va bajo cada uno; uno sin miembros, bajo la propiedad del objeto.
    private static IEnumerable<(string Propiedad, string Mensaje)> PorPropiedad(IEnumerable<ValidationResult> errores)
    {
        foreach (var error in errores)
        {
            var mensaje = string.IsNullOrEmpty(error.ErrorMessage) ? MensajePorDefecto : error.ErrorMessage;
            var miembros = error.MemberNames.ToList();
            if (miembros.Count == 0)
            {
                yield return (ErrorDelObjeto, mensaje);
                continue;
            }

            foreach (var miembro in miembros)
            {
                yield return (miembro ?? ErrorDelObjeto, mensaje);
            }
        }
    }
}
