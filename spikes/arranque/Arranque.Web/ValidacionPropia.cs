using System.ComponentModel.DataAnnotations;
using Arranque.Contracts;
using Wolverine;

namespace Arranque.Web;

/// <summary>
/// Middleware propio mínimo (fila "Sin soporte nativo" de la matriz). Corre antes del handler;
/// si el comando no pasa DataAnnotations, detiene la cadena y entrega un <see cref="Resultado"/>
/// inválido como respuesta de <c>InvokeAsync&lt;Resultado&gt;</c>, sin lanzar excepción.
/// </summary>
public static class ValidacionPropia
{
    public static (HandlerContinuation, OutgoingMessages) Before(IComando comando)
    {
        var fallos = new List<ValidationResult>();
        if (Validator.TryValidateObject(comando, new ValidationContext(comando), fallos, validateAllProperties: true))
        {
            return (HandlerContinuation.Continue, []);
        }

        var errores = fallos.SelectMany(f =>
            f.MemberNames.DefaultIfEmpty("").Select(campo => new ErrorDeCampo(campo, f.ErrorMessage ?? "")));

        return (HandlerContinuation.Stop, [Resultado.Invalido(errores)]);
    }
}
