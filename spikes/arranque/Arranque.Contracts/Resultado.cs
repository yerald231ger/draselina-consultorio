namespace Arranque.Contracts;

/// <summary>Marcador de comando. Hace aquí el papel de <c>ICommand&lt;T&gt;</c> de BuildingBlocks.Contracts.</summary>
public interface IComando;

/// <summary>Un error de forma, ligado al nombre de la propiedad del comando.</summary>
public sealed record ErrorDeCampo(string Campo, string Mensaje);

/// <summary>Resultado mínimo del prototipo: válido, o inválido con errores por campo.</summary>
public sealed record Resultado(bool EsValido, IReadOnlyList<ErrorDeCampo> Errores)
{
    public static Resultado Ok() => new(true, []);

    public static Resultado Invalido(IEnumerable<ErrorDeCampo> errores) => new(false, errores.ToList());
}
