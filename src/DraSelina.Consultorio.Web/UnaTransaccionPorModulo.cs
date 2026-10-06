using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using JasperFx.CodeGeneration.Model;
using JasperFx.Core.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.Runtime;
using Wolverine.Runtime.Handlers;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// Un comando, una transacción, un módulo. La política del host, fijada una sola vez:
/// <list type="bullet">
/// <item>El handler de un comando (<see cref="ICommand{T}"/>) corre en la transacción del contexto de su módulo. El
/// host la abre antes del handler y la cierra después: confirma si el <see cref="Resultado"/> es exitoso, deshace
/// si no lo es o si el handler lanza.</item>
/// <item>El handler de un evento corre igual, y confirma salvo excepción.</item>
/// <item>El handler de una consulta (<see cref="IQuery{T}"/>) corre sin transacción.</item>
/// </list>
/// El dueño lo decide la tabla de <see cref="ModuloConTransaccion"/> por el namespace del handler. Ni el handler ni
/// el comando declaran nada, y el host nunca llama a <c>SaveChanges</c>: el handler guarda.
/// </summary>
internal sealed class UnaTransaccionPorModulo : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var modulos = container.Services.GetServices<ModuloConTransaccion>().ToList();

        foreach (var cadena in chains)
        {
            Aplicar(cadena, modulos);
        }
    }

    private static void Aplicar(HandlerChain cadena, IReadOnlyList<ModuloConTransaccion> modulos)
    {
        var mensaje = cadena.MessageType;

        // Los handlers internos de Wolverine no son casos de uso.
        var handlers = cadena.Handlers.Where(h => h.HandlerType.Assembly != typeof(WolverineOptions).Assembly).ToList();
        if (handlers.Count == 0 || Cierra(mensaje, typeof(IQuery<>)))
        {
            return;
        }

        // Todo handler de comando o de evento vive en un módulo dado de alta: nadie corre en silencio sin transacción.
        foreach (var fuera in handlers.Where(h => ContextoDe(h, modulos) is null))
        {
            throw new InvalidOperationException(
                $"El handler {fuera.HandlerType.FullNameInCode()} de {mensaje.FullNameInCode()} vive en el namespace "
                + $"'{fuera.HandlerType.Namespace}', que no es de ningún módulo dado de alta en la política de transacción "
                + $"({nameof(RegistroDeTransacciones.AddTransaccionDeModulo)}). No se sabe en qué transacción corre. "
                + $"Módulos dados de alta: {string.Join(", ", modulos.Select(m => m.EspacioDeHandlers))}.");
        }

        var contextos = handlers.Select(h => ContextoDe(h, modulos)!).Distinct().ToList();

        if (Cierra(mensaje, typeof(ICommand<>)))
        {
            if (handlers.Count != 1)
            {
                throw new InvalidOperationException(
                    $"El comando {mensaje.FullNameInCode()} tiene {handlers.Count} handlers ({Nombres(handlers)}). Un comando tiene exactamente un handler.");
            }

            // El mismo Resultado<T> que ISender entrega: con otro tipo el cambio se confirmaría y quien despachó
            // no recibiría su resultado.
            var handler = handlers[0];
            var esperados = mensaje.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>))
                .Select(i => typeof(Resultado<>).MakeGenericType(i.GetGenericArguments()[0]))
                .ToList();
            if (handler.ReturnVariable is not { } resultado || esperados is not [var esperado] || resultado.VariableType != esperado)
            {
                throw new InvalidOperationException(
                    $"El handler {handler.HandlerType.FullNameInCode()} del comando {mensaje.FullNameInCode()} debe devolver "
                    + $"{string.Join(" o ", esperados.Select(e => e.FullNameInCode()))} y devuelve "
                    + $"{handler.ReturnVariable?.VariableType.FullNameInCode() ?? "nada"}: sin ese resultado no se sabe si confirmar o "
                    + "deshacer la transacción.");
            }

            Envolver(cadena, contextos[0], resultado);
            return;
        }

        // Un evento.
        if (contextos is not [var unico])
        {
            throw new InvalidOperationException(
                $"El evento {mensaje.FullNameInCode()} tiene handlers de más de un módulo en la misma cadena ({Nombres(handlers)}). "
                + "Una transacción es de un solo módulo.");
        }

        Envolver(cadena, unico, resultado: null);
    }

    private static Type? ContextoDe(MethodCall handler, IReadOnlyList<ModuloConTransaccion> modulos) =>
        modulos.FirstOrDefault(m => m.EspacioDeHandlers == handler.HandlerType.Namespace)?.Contexto;

    private static void Envolver(HandlerChain cadena, Type contexto, Variable? resultado)
    {
        var abrir = new AbrirTransaccionDeModulo(contexto);
        cadena.Middleware.Add(abrir);
        cadena.Postprocessors.Add(new CerrarTransaccionDeModulo(abrir.Transaccion, resultado));
    }

    private static bool Cierra(Type tipo, Type interfazAbierta) =>
        tipo.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfazAbierta);

    private static string Nombres(IEnumerable<MethodCall> handlers) =>
        string.Join(", ", handlers.Select(h => h.HandlerType.FullNameInCode()));

    /// <summary>Antes del handler: abre la transacción. Si el handler lanza, al salir se deshace.</summary>
    private sealed class AbrirTransaccionDeModulo : AsyncFrame
    {
        private readonly Type _tipoDeContexto;
        private Variable? _contexto;
        private Variable? _mensajes;
        private Variable? _cancelacion;

        public AbrirTransaccionDeModulo(Type tipoDeContexto)
        {
            _tipoDeContexto = tipoDeContexto;
            Transaccion = new Variable(typeof(TransaccionDeModulo), "transaccionDeModulo", this);
        }

        public Variable Transaccion { get; }

        public override IEnumerable<Variable> FindVariables(IMethodVariables chain)
        {
            // El mismo contexto que reciben el handler y sus puertos: uno por mensaje.
            _contexto = chain.FindVariable(_tipoDeContexto);
            yield return _contexto;
            _mensajes = chain.FindVariable(typeof(MessageContext));
            yield return _mensajes;
            _cancelacion = chain.FindVariable(typeof(CancellationToken));
            yield return _cancelacion;
        }

        public override void GenerateCode(GeneratedMethod method, ISourceWriter writer)
        {
            writer.WriteComment("La transacción del módulo del handler: la abre y la cierra el host");
            writer.WriteLine(
                $"await using var {Transaccion.Usage} = await {typeof(TransaccionDeModulo).FullNameInCode()}.{nameof(TransaccionDeModulo.AbrirAsync)}"
                + $"({_contexto!.Usage}, {_mensajes!.Usage}, {_cancelacion!.Usage}).ConfigureAwait(false);");
            Next?.GenerateCode(method, writer);
        }
    }

    /// <summary>Después del handler, si no lanzó: cierra según el resultado (comando) o confirma (evento).</summary>
    private sealed class CerrarTransaccionDeModulo(Variable transaccion, Variable? resultado) : AsyncFrame
    {
        private Variable? _cancelacion;

        public override IEnumerable<Variable> FindVariables(IMethodVariables chain)
        {
            yield return transaccion;
            if (resultado is not null)
            {
                yield return resultado;
            }

            _cancelacion = chain.FindVariable(typeof(CancellationToken));
            yield return _cancelacion;
        }

        public override void GenerateCode(GeneratedMethod method, ISourceWriter writer)
        {
            writer.WriteLine(resultado is null
                ? $"await {transaccion.Usage}.{nameof(TransaccionDeModulo.ConfirmarAsync)}({_cancelacion!.Usage}).ConfigureAwait(false);"
                : $"await {transaccion.Usage}.{nameof(TransaccionDeModulo.CerrarAsync)}({resultado.Usage}, {_cancelacion!.Usage}).ConfigureAwait(false);");
            Next?.GenerateCode(method, writer);
        }
    }
}
