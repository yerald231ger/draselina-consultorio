using DraSelina.Consultorio.BuildingBlocks.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// Un outbox dado de alta en el host: el contexto del módulo que publica. El host lee y actualiza la tabla
/// <c>outbox</c> del esquema de ese contexto, por la conexión y la transacción de ese contexto (AD-2).
/// </summary>
public sealed record OutboxRegistrado(Type Contexto);

/// <summary>
/// Cuánto puede tardar un handler de evento. Al vencerse se cancela su <see cref="CancellationToken"/> y la entrega
/// cuenta como fallida: el evento se reintenta según el calendario y la vuelta sigue con los demás.
/// </summary>
public sealed record OpcionesDeEntrega(TimeSpan LimitePorHandler)
{
    public static readonly OpcionesDeEntrega Predeterminadas = new(TimeSpan.FromSeconds(30));
}

/// <summary>El alta de la entrega de eventos y de cada outbox. La llama <c>Program.cs</c>.</summary>
public static class Outbox
{
    /// <summary>La tabla de todo outbox. Cada módulo que publica la tiene en su esquema, con las mismas columnas.</summary>
    public const string Tabla = "outbox";

    public const string Pendiente = "Pendiente";
    public const string Entregado = "Entregado";
    public const string Fallido = "Fallido";

    /// <summary>
    /// Registra la entrega de eventos y el servicio en segundo plano que la repite. Los tipos de evento se toman
    /// de los <see cref="IEventHandler{TEvento}"/> que los módulos registraron, antes o después de esta llamada.
    /// </summary>
    public static IServiceCollection AddEntregaDeEventos(this IServiceCollection servicios) =>
        servicios
            .AddSingleton(_ => new CatalogoDeEventos(servicios))
            .AddSingleton(OpcionesDeEntrega.Predeterminadas)
            .AddSingleton<EntregaDeEventos>()
            .AddHostedService<ServicioDeEntregaDeEventos>();

    /// <summary>Da de alta el outbox del módulo dueño de <typeparamref name="TContexto"/>.</summary>
    public static IServiceCollection AddOutbox<TContexto>(this IServiceCollection servicios)
        where TContexto : DbContext =>
        servicios.AddSingleton(new OutboxRegistrado(typeof(TContexto)));
}

/// <summary>
/// Los tipos de evento que alguien escucha, por su nombre (la columna <c>tipo</c>), y cuántos handlers tiene cada
/// uno. Sale de los <see cref="IEventHandler{TEvento}"/> registrados en el contenedor.
/// </summary>
public sealed class CatalogoDeEventos
{
    private readonly Dictionary<string, (Type Tipo, int Handlers)> _porNombre = new(StringComparer.Ordinal);

    public CatalogoDeEventos(IServiceCollection servicios)
    {
        var tipos = servicios
            .Where(s => s.ServiceType.IsConstructedGenericType && s.ServiceType.GetGenericTypeDefinition() == typeof(IEventHandler<>))
            .GroupBy(s => s.ServiceType.GenericTypeArguments[0]);

        foreach (var grupo in tipos)
        {
            var tipo = grupo.Key;
            if (_porNombre.TryGetValue(tipo.Name, out var otro))
            {
                throw new InvalidOperationException(
                    $"Dos tipos de evento se llaman '{tipo.Name}': '{otro.Tipo.FullName}' y '{tipo.FullName}'. "
                    + "El outbox guarda el nombre del tipo: debe ser único en la aplicación.");
            }

            _porNombre[tipo.Name] = (tipo, grupo.Count());
        }
    }

    /// <summary>El tipo de evento con ese nombre y cuántos handlers tiene; falso si nadie lo escucha.</summary>
    public bool TryBuscar(string nombre, out Type tipo, out int handlers)
    {
        var existe = _porNombre.TryGetValue(nombre, out var encontrado);
        (tipo, handlers) = existe ? encontrado : (typeof(object), 0);
        return existe;
    }
}
