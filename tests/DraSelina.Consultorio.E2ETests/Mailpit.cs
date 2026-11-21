using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DraSelina.Consultorio.E2ETests;

/// <summary>
/// La API HTTP de Mailpit. Los correos salen por el outbox, con retraso: las búsquedas se repiten hasta un tiempo
/// límite en lugar de esperar un tiempo fijo.
/// </summary>
public sealed partial class Mailpit(Uri direccion) : IDisposable
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Pausa = TimeSpan.FromMilliseconds(500);

    // El principio del asunto de la confirmación (PlantillaDeConfirmacion.Titulo).
    private const string AsuntoDeConfirmacion = "Tu cita está confirmada";

    private readonly HttpClient _http = new() { BaseAddress = direccion };

    /// <summary>
    /// El texto del correo de confirmación de la Cita que llegue a <paramref name="destinatario"/>; falla si no llega a
    /// tiempo. Se busca por asunto: el Recordatorio ("Mañana tienes cita") lleva el mismo Enlace de gestión y puede
    /// salir en la misma corrida.
    /// </summary>
    public async Task<string> ConfirmacionParaAsync(string destinatario)
    {
        var hasta = DateTimeOffset.UtcNow + Limite;
        while (DateTimeOffset.UtcNow < hasta)
        {
            var ids = await BuscarAsync($"to:\"{destinatario}\" subject:\"{AsuntoDeConfirmacion}\"");
            if (ids.Count > 0)
            {
                using var mensaje = await _http.GetFromJsonAsync<JsonDocument>($"api/v1/message/{ids[0]}");
                return mensaje!.RootElement.GetProperty("Text").GetString() ?? "";
            }

            await Task.Delay(Pausa);
        }

        throw new TimeoutException(
            $"No llegó la confirmación a {destinatario} en {Limite.TotalSeconds} segundos.");
    }

    /// <summary>Cuántos correos mencionan <paramref name="texto"/> en cualquier parte.</summary>
    public async Task<int> CorreosConAsync(string texto) => (await BuscarAsync($"\"{texto}\"")).Count;

    /// <summary>El Enlace de gestión (la dirección completa hasta el token) dentro del texto de un correo.</summary>
    public static string EnlaceDeGestionEn(string texto)
    {
        var enlace = EnlaceDeGestion().Match(texto);
        return enlace.Success
            ? enlace.Value
            : throw new InvalidOperationException($"El correo no trae un Enlace de gestión:\n{texto}");
    }

    private async Task<List<string>> BuscarAsync(string consulta)
    {
        using var resultado = await _http.GetFromJsonAsync<JsonDocument>(
            $"api/v1/search?query={Uri.EscapeDataString(consulta)}");
        return [.. resultado!.RootElement.GetProperty("messages").EnumerateArray()
            .Select(m => m.GetProperty("ID").GetString()!)];
    }

    // El token va en Base64 URL: letras, dígitos, guion y guion bajo.
    [GeneratedRegex(@"https?://[^\s""'<>]+/c/[A-Za-z0-9_-]+")]
    private static partial Regex EnlaceDeGestion();

    public void Dispose() => _http.Dispose();
}
