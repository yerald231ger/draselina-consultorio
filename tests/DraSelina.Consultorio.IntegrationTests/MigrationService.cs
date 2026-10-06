using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using DraSelina.Consultorio.Web;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Ejecuta el <c>MigrationService</c> real como proceso, igual que lo hace el AppHost: mismo <c>Program</c>,
/// misma variable de entorno para la cadena de conexión, mismo código de salida.
/// </summary>
public static class MigrationService
{
    private static readonly TimeSpan Limite = TimeSpan.FromMinutes(3);

    /// <summary>Migra la base de <paramref name="cadenaDeConexion"/>. Lanza si el proceso no sale con 0.</summary>
    public static async Task EjecutarAsync(string cadenaDeConexion, CancellationToken cancelacion = default)
    {
        var ensamblado = RutaDelEnsamblado();

        var inicio = new ProcessStartInfo(Dotnet())
        {
            WorkingDirectory = Path.GetDirectoryName(ensamblado)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        inicio.ArgumentList.Add(ensamblado);
        inicio.Environment[$"ConnectionStrings__{BaseDeDatos.Nombre}"] = cadenaDeConexion;

        var salida = new StringBuilder();
        using var proceso = new Process { StartInfo = inicio };
        proceso.OutputDataReceived += (_, linea) => Anotar(linea.Data);
        proceso.ErrorDataReceived += (_, linea) => Anotar(linea.Data);

        proceso.Start();
        proceso.BeginOutputReadLine();
        proceso.BeginErrorReadLine();

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
        limite.CancelAfter(Limite);
        try
        {
            await proceso.WaitForExitAsync(limite.Token);
        }
        catch (OperationCanceledException)
        {
            proceso.Kill(entireProcessTree: true);
            cancelacion.ThrowIfCancellationRequested();
            throw new MigracionFallidaException(null, Texto());
        }

        if (proceso.ExitCode != 0)
        {
            throw new MigracionFallidaException(proceso.ExitCode, Texto());
        }

        void Anotar(string? linea)
        {
            if (linea is null) return;
            lock (salida) salida.AppendLine(linea);
        }

        string Texto()
        {
            lock (salida) return salida.ToString();
        }
    }

    // La ruta la escribe la compilación (ver el .csproj): el .dll queda en el bin/ del propio MigrationService,
    // junto a sus dependencias.
    private static string RutaDelEnsamblado()
    {
        var ruta = typeof(MigrationService).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(atributo => atributo.Key == "RutaDelMigrationService").Value;

        return File.Exists(ruta)
            ? ruta
            : throw new FileNotFoundException($"No está compilado el MigrationService en '{ruta}'. Compila la solución: ./dotnet.sh build");
    }

    // El mismo `dotnet` que corre estas pruebas (el de .dotnet/ cuando se usa ./dotnet.sh).
    private static string Dotnet()
    {
        var nombre = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var junto = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", nombre));
        return File.Exists(junto) ? junto : nombre;
    }
}

/// <summary>El <c>MigrationService</c> no terminó con código 0. El mensaje lleva el código y la salida del proceso.</summary>
public sealed class MigracionFallidaException(int? codigoDeSalida, string salida) : Exception(
    (codigoDeSalida is { } codigo
        ? $"El MigrationService terminó con código de salida {codigo}."
        : "El MigrationService no terminó a tiempo y se detuvo.")
    + $" Salida del proceso:{Environment.NewLine}{salida}")
{
    public int? CodigoDeSalida { get; } = codigoDeSalida;

    public string Salida { get; } = salida;
}
