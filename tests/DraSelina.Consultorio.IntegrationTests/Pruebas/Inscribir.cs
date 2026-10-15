using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>
/// Comando de prueba con forma declarada por DataAnnotations (AD-12): lo valida el decorador del host antes de su
/// handler. No guarda nada; su handler solo deja constancia de que corrió.
/// </summary>
public sealed class Inscribir : ICommand<Guid>, IValidatableObject
{
    public const string MensajeNombreRequerido = "El nombre es obligatorio.";
    public const string MensajeNombreLargo = "El nombre tiene a lo más 50 caracteres.";
    public const string MensajeCorreoRequerido = "El correo es obligatorio.";
    public const string MensajeCorreoMalFormado = "El correo no tiene la forma de un correo.";
    public const string MensajeCorreoLargo = "El correo tiene a lo más 40 caracteres.";
    public const string MensajeEdadFueraDeRango = "La edad va de 18 a 120.";
    public const string MensajeDelObjeto = "La inscripción no es válida en su conjunto.";

    public Guid Id { get; set; }

    [Required(ErrorMessage = MensajeNombreRequerido)]
    [StringLength(50, ErrorMessage = MensajeNombreLargo)]
    public string Nombre { get; set; } = "";

    [Required(ErrorMessage = MensajeCorreoRequerido)]
    [EmailAddress(ErrorMessage = MensajeCorreoMalFormado)]
    [StringLength(40, ErrorMessage = MensajeCorreoLargo)]
    public string Correo { get; set; } = "";

    [Range(18, 120, ErrorMessage = MensajeEdadFueraDeRango)]
    public int Edad { get; set; }

    /// <summary>Solo de la prueba: hace que la validación del objeto devuelva un error sin miembros.</summary>
    public bool ErrorDelObjeto { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ErrorDelObjeto)
        {
            yield return new ValidationResult(MensajeDelObjeto);
        }
    }
}

/// <summary>Cuántas veces corrió el handler de <see cref="Inscribir"/> en esta web. Uno por web (singleton).</summary>
public sealed class LlamadasDeInscribir
{
    private int _cuantas;

    public int Cuantas => Volatile.Read(ref _cuantas);

    public void Contar() => Interlocked.Increment(ref _cuantas);
}

/// <summary>El handler de <see cref="Inscribir"/>: cuenta la llamada y devuelve el id del comando.</summary>
public sealed class InscribirHandler(LlamadasDeInscribir llamadas) : ICommandHandler<Inscribir, Guid>
{
    public Task<Resultado<Guid>> HandleAsync(Inscribir comando, CancellationToken cancellationToken)
    {
        llamadas.Contar();
        return Task.FromResult<Resultado<Guid>>(comando.Id);
    }
}
