using System.ComponentModel.DataAnnotations;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// La forma de un celular de México: 10 dígitos, con o sin <c>+52</c>, admitiendo espacios, guiones, puntos y
/// paréntesis entre ellos. Solo con la biblioteca base, sin ASP.NET (AD-12). Si el número existe en el plan de
/// numeración de México lo decide Pacientes, con libphonenumber, al reconocer o registrar al Paciente. Un valor
/// nulo pasa: que falte lo dice <c>[Required]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class TelefonoDeDiezDigitosAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string texto)
        {
            return false;
        }

        var digitos = new List<char>(texto.Length);
        var conMas = false;
        foreach (var caracter in texto.Trim())
        {
            if (char.IsAsciiDigit(caracter))
            {
                digitos.Add(caracter);
            }
            else if (caracter == '+' && digitos.Count == 0 && !conMas)
            {
                conMas = true;
            }
            else if (caracter is not (' ' or '-' or '.' or '(' or ')'))
            {
                return false;
            }
        }

        return digitos.Count == 10 && !conMas
               || digitos.Count == 12 && digitos[0] == '5' && digitos[1] == '2';
    }
}
