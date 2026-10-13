using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>Un registro (aviso, error...) que escribió la web en memoria.</summary>
public sealed record RegistroEscrito(LogLevel Nivel, string Categoria, string Mensaje);

/// <summary>Guarda lo que la web en memoria registra, para que una prueba afirme que algo se registró.</summary>
public sealed class RegistrosDePrueba : ILoggerProvider
{
    private readonly ConcurrentQueue<RegistroEscrito> _escritos = new();

    public IReadOnlyCollection<RegistroEscrito> Escritos => [.. _escritos];

    public ILogger CreateLogger(string categoryName) => new Registrador(categoryName, _escritos);

    public void Dispose()
    {
    }

    private sealed class Registrador(string categoria, ConcurrentQueue<RegistroEscrito> escritos) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            escritos.Enqueue(new RegistroEscrito(logLevel, categoria, formatter(state, exception)));
    }
}
