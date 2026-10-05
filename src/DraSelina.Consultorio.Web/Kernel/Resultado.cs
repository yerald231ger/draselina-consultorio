namespace DraSelina.Consultorio.Kernel;

/// <summary>Las cuatro formas de un <see cref="Resultado"/>.</summary>
public enum FormaDeResultado
{
    Exito = 1,
    Invalido = 2,
    NoAutorizado = 3,
    Fallo = 4,
}

/// <summary>
/// Lo que devuelve todo comando y toda consulta. Tiene cuatro formas: éxito, inválido (errores de forma
/// por nombre de propiedad), no autorizado y fallo esperado con código. Los fallos esperados nunca se
/// lanzan como excepción. El Kernel no conoce códigos de negocio: cada módulo define su conjunto cerrado
/// en sus <c>Contracts</c>.
/// </summary>
public class Resultado
{
    private protected static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> SinErrores =
        System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>.Empty;

    private protected Resultado(
        FormaDeResultado forma,
        IReadOnlyDictionary<string, IReadOnlyList<string>> errores,
        string? codigo)
    {
        Forma = forma;
        Errores = errores;
        Codigo = codigo;
    }

    public FormaDeResultado Forma { get; }

    public bool EsExito => Forma == FormaDeResultado.Exito;

    /// <summary>Mensajes de error por nombre de propiedad. Vacío salvo en la forma inválida.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errores { get; }

    /// <summary>Código del fallo esperado. Nulo salvo en la forma de fallo.</summary>
    public string? Codigo { get; }

    public static Resultado Exito() => new(FormaDeResultado.Exito, SinErrores, null);

    public static Resultado<T> Exito<T>(T valor) => new(valor);

    /// <summary>Forma inválida: cada error queda bajo el nombre de su propiedad, en el orden recibido.</summary>
    public static Resultado Invalido(IEnumerable<(string Propiedad, string Mensaje)> errores)
    {
        ArgumentNullException.ThrowIfNull(errores);

        var porPropiedad = new Dictionary<string, List<string>>();
        foreach (var (propiedad, mensaje) in errores)
        {
            ArgumentNullException.ThrowIfNull(propiedad);
            ArgumentNullException.ThrowIfNull(mensaje);
            if (!porPropiedad.TryGetValue(propiedad, out var mensajes))
            {
                porPropiedad[propiedad] = mensajes = [];
            }

            mensajes.Add(mensaje);
        }

        if (porPropiedad.Count == 0)
        {
            throw new ArgumentException("Un resultado inválido lleva al menos un error.", nameof(errores));
        }

        return new Resultado(
            FormaDeResultado.Invalido,
            porPropiedad.ToDictionary(par => par.Key, par => (IReadOnlyList<string>)par.Value.AsReadOnly()).AsReadOnly(),
            null);
    }

    public static Resultado Invalido(string propiedad, string mensaje) => Invalido([(propiedad, mensaje)]);

    public static Resultado NoAutorizado() => new(FormaDeResultado.NoAutorizado, SinErrores, null);

    /// <summary>Fallo esperado, con un código del conjunto cerrado del módulo que lo devuelve.</summary>
    public static Resultado Fallo(string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        return new Resultado(FormaDeResultado.Fallo, SinErrores, codigo);
    }

    /// <summary>Lleva un resultado no exitoso al tipo que el llamador debe devolver, sin perder forma, errores ni código.</summary>
    public Resultado<T> Como<T>() =>
        EsExito
            ? throw new InvalidOperationException("Un resultado exitoso no se convierte: su valor se da con Resultado.Exito(valor).")
            : new Resultado<T>(Forma, Errores, Codigo);
}

/// <summary>Un <see cref="Resultado"/> que en el éxito trae un valor.</summary>
public sealed class Resultado<T> : Resultado
{
    private readonly T? _valor;

    internal Resultado(T valor)
        : base(FormaDeResultado.Exito, SinErrores, null)
    {
        _valor = valor;
    }

    internal Resultado(
        FormaDeResultado forma,
        IReadOnlyDictionary<string, IReadOnlyList<string>> errores,
        string? codigo)
        : base(forma, errores, codigo)
    {
    }

    /// <summary>El valor del éxito. Pedirlo en cualquier otra forma es un error de programación.</summary>
    public T Valor =>
        EsExito
            ? _valor!
            : throw new InvalidOperationException($"El resultado no es exitoso ({Forma}); no tiene valor.");

    public static implicit operator Resultado<T>(T valor) => new(valor);
}
