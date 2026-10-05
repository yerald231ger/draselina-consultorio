using System.Collections.Concurrent;

namespace Arranque.Web;

/// <summary>Contadores que <c>verificar.sh</c> lee en <c>/diag</c>: ejecuciones del handler y errores registrados.</summary>
public sealed class Diagnostico : ILoggerProvider
{
    private int _handler;
    private int _errores;
    private readonly ConcurrentQueue<string> _ultimos = new();

    public int EjecucionesDelHandler => _handler;
    public int ErroresRegistrados => _errores;
    public string[] UltimosErrores => _ultimos.ToArray();

    public void HandlerEjecutado() => Interlocked.Increment(ref _handler);

    public ILogger CreateLogger(string categoryName) => new Contador(this, categoryName);

    public void Dispose() { }

    private sealed class Contador(Diagnostico d, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // LogLevel.None (6) es mayor que Error: Wolverine lo usa para silenciar sus mensajes internos.
        public bool IsEnabled(LogLevel logLevel) => logLevel is LogLevel.Error or LogLevel.Critical;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            Interlocked.Increment(ref d._errores);
            d._ultimos.Enqueue($"{categoria}: {formatter(state, exception)} | {exception?.GetType().FullName}");
            while (d._ultimos.Count > 10) d._ultimos.TryDequeue(out _);
        }
    }
}
