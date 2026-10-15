using System.ComponentModel.DataAnnotations;
using System.Reflection;
using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Todo atributo de validación de un comando lleva su <c>ErrorMessage</c> (Gerardo, 2026-10-15): el decorador
/// entrega el mensaje tal cual, y sin <c>ErrorMessage</c> el que llega es el de .NET, en inglés ("The Nombre field
/// is required."). Recorre los comandos de Web y los de prueba, así que un comando nuevo queda cubierto sin tocarla.
/// </summary>
public sealed class MensajesDeValidacionTests
{
    [Fact]
    public void Todo_atributo_de_validacion_de_un_comando_lleva_su_mensaje()
    {
        var comandos = CasosDeUso.DeLaWebYDePrueba()
            .Where(t => t.GetInterfaces().Any(i => i.IsConstructedGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)))
            .ToList();
        Assert.NotEmpty(comandos);

        var faltantes = comandos.SelectMany(SinMensaje).ToList();

        Assert.True(faltantes.Count == 0, string.Join(Environment.NewLine, faltantes));
    }

    [Fact]
    public void El_verificador_nombra_cada_atributo_sin_mensaje()
    {
        var faltantes = SinMensaje(typeof(ComandoConAtributosSinMensaje));

        Assert.Equal(2, faltantes.Count);
        Assert.Contains(faltantes, f => f.Contains($"{nameof(ComandoConAtributosSinMensaje.Nombre)}: Required"));
        Assert.Contains(faltantes, f => f.Contains($"{nameof(ComandoConAtributosSinMensaje.Correo)}: EmailAddress"));
    }

    /// <summary>
    /// Un mensaje por atributo de validación sin <c>ErrorMessage</c> escrito, en el tipo o en sus propiedades. Se lee
    /// cómo se escribió el atributo y no su propiedad <c>ErrorMessage</c>: algunos (<c>[EmailAddress]</c>) la traen
    /// llena con el mensaje de .NET en inglés aunque nadie la haya escrito.
    /// </summary>
    private static List<string> SinMensaje(Type comando)
    {
        var lugares = new List<(string Nombre, MemberInfo Miembro)> { (comando.Name, comando) };
        lugares.AddRange(comando.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => (p.Name, (MemberInfo)p)));

        return
        [
            .. from lugar in lugares
               from atributo in lugar.Miembro.GetCustomAttributesData()
               where typeof(ValidationAttribute).IsAssignableFrom(atributo.AttributeType)
                     && !atributo.NamedArguments.Any(a => a.MemberName is nameof(ValidationAttribute.ErrorMessage) or nameof(ValidationAttribute.ErrorMessageResourceName))
               select $"{comando.FullName}.{lugar.Nombre}: {atributo.AttributeType.Name.Replace("Attribute", "")} no lleva ErrorMessage.",
        ];
    }

    // No es un comando a propósito: el recorrido de la otra prueba no debe encontrarlo.
    private sealed class ComandoConAtributosSinMensaje
    {
        [Required]
        public string Nombre { get; set; } = "";

        [EmailAddress]
        [StringLength(40, ErrorMessage = "El correo tiene a lo más 40 caracteres.")]
        public string Correo { get; set; } = "";
    }
}
