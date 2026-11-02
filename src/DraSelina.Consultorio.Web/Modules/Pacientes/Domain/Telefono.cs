using System.Text.RegularExpressions;

namespace DraSelina.Consultorio.Pacientes.Domain;

/// <summary>
/// Un teléfono de México en E.164: <c>+52</c> y los 10 dígitos del número nacional, que no empieza en 0 ni en 1
/// (INV-P2, T-9). No existe uno con otra forma. No es la identidad del Paciente: puede cambiar o compartirse
/// (INV-P3).
/// </summary>
/// <remarks>
/// Decidir si un número de 10 dígitos existe en el plan de numeración de México lo hace libphonenumber, en
/// <c>Pacientes.Features</c>, antes de construir este valor: el dominio solo usa la biblioteca base (T-28).
/// </remarks>
public sealed partial record Telefono
{
    private Telefono(string valor) => Valor = valor;

    /// <summary>El número en E.164, como <c>+524441234567</c>.</summary>
    public string Valor { get; }

    /// <summary>El teléfono, si <paramref name="e164"/> es un número de México en E.164; si no, nulo.</summary>
    public static Telefono? DesdeE164(string? e164) =>
        e164 is not null && FormaDeMexico().IsMatch(e164) ? new Telefono(e164) : null;

    /// <summary>Reconstitución desde la base: no valida (T-6).</summary>
    internal static Telefono Reconstituir(string valor) => new(valor);

    public override string ToString() => Valor;

    [GeneratedRegex(@"^\+52[2-9][0-9]{9}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormaDeMexico();
}
