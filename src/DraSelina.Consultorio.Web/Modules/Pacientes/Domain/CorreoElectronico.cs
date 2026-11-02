using System.Net.Mail;

namespace DraSelina.Consultorio.Pacientes.Domain;

/// <summary>Un correo electrónico válido (T-9). Es opcional en el Paciente; si existe, tiene forma de correo.</summary>
public sealed record CorreoElectronico
{
    /// <summary>El largo máximo de una dirección de correo.</summary>
    public const int LargoMaximo = 254;

    private CorreoElectronico(string valor) => Valor = valor;

    /// <summary>La dirección, sin espacios al principio ni al final.</summary>
    public string Valor { get; }

    /// <summary>El correo, si <paramref name="texto"/> es una sola dirección válida; si no, nulo.</summary>
    public static CorreoElectronico? Crear(string? texto)
    {
        var valor = texto?.Trim() ?? "";
        if (valor.Length is 0 or > LargoMaximo
            || !MailAddress.TryCreate(valor, out var direccion)
            || direccion.Address != valor
            || !direccion.Host.Contains('.'))
        {
            return null;
        }

        return new CorreoElectronico(valor);
    }

    /// <summary>Reconstitución desde la base: no valida (T-6).</summary>
    internal static CorreoElectronico Reconstituir(string valor) => new(valor);

    /// <summary>Dos correos son la misma dirección aunque difieran en mayúsculas.</summary>
    public bool EsLaMismaDireccionQue(CorreoElectronico otro) =>
        string.Equals(Valor, otro.Valor, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Valor;
}
