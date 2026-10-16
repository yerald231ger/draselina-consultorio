using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// El comando del formulario de prueba. Sus mensajes salen de DataAnnotations, en español, por el decorador real
/// <c>ValidacionDeComandos</c>. Solo existe para probar cómo se muestran los errores.
/// </summary>
public sealed class ComandoDePrueba : ICommand<int>, IValidatableObject
{
    public const string MensajeNombreRequerido = "El nombre es obligatorio.";
    public const string MensajeCorreoRequerido = "El correo es obligatorio.";
    public const string MensajeCorreoMalFormado = "El correo no tiene la forma de un correo.";
    public const string MensajeCorreoLargo = "El correo va de 6 a 40 caracteres.";
    public const string MensajeSinEntrega = "No podemos enviar la confirmación a ese correo.";
    public const string MensajeRevisaElNombre = "Revisa el nombre.";

    /// <summary>Un correo con este dominio pasa los atributos pero no la regla del objeto.</summary>
    public const string DominioSinEntrega = "@sin-entrega.test";

    [Required(ErrorMessage = MensajeNombreRequerido)]
    public string Nombre { get; set; } = "";

    [Required(ErrorMessage = MensajeCorreoRequerido)]
    [EmailAddress(ErrorMessage = MensajeCorreoMalFormado)]
    [StringLength(40, MinimumLength = 6, ErrorMessage = MensajeCorreoLargo)]
    public string Correo { get; set; } = "";

    // Solo corre si los atributos pasaron. Da un error del objeto (propiedad "") y uno de campo, a la vez.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Correo.EndsWith(DominioSinEntrega, StringComparison.Ordinal))
        {
            yield return new ValidationResult(MensajeSinEntrega);
            yield return new ValidationResult(MensajeRevisaElNombre, [nameof(Nombre)]);
        }
    }
}

/// <summary>El handler del comando de prueba: si llega aquí, el comando pasó la validación.</summary>
public sealed class HandlerDePrueba : ICommandHandler<ComandoDePrueba, int>
{
    public Task<Resultado<int>> HandleAsync(ComandoDePrueba comando, CancellationToken cancellationToken) =>
        Task.FromResult<Resultado<int>>(1);
}
