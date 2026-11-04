using System.Security.Cryptography;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// La clave del host con la que se derivan los Enlaces de gestión (AD-19, RN-22). Vive fuera de la base: la da la
/// configuración del host. Nunca se escribe en registros: <see cref="ToString"/> no la muestra (AD-23).
/// </summary>
public sealed class ClaveDeGestion
{
    /// <summary>Lo mínimo para HMAC-SHA256: tantos bytes como su salida.</summary>
    public const int BytesMinimos = 32;

    private readonly byte[] _bytes;

    private ClaveDeGestion(byte[] bytes) => _bytes = bytes;

    /// <summary>Una clave con al menos <see cref="BytesMinimos"/> bytes; se copia.</summary>
    /// <exception cref="ArgumentException">Si tiene menos.</exception>
    public static ClaveDeGestion Crear(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < BytesMinimos)
        {
            throw new ArgumentException($"La clave del Enlace de gestión lleva al menos {BytesMinimos} bytes.", nameof(bytes));
        }

        return new ClaveDeGestion(bytes.ToArray());
    }

    internal ReadOnlySpan<byte> Bytes => _bytes;

    public override string ToString() => "ClaveDeGestion(oculta)";
}

/// <summary>
/// El token del Enlace de gestión de una Cita y su hash (AD-19, RN-22). El token se deriva, no se genera al azar:
/// <c>HMAC-SHA256(clave del host, CitaId)</c>, con el <see cref="Guid"/> en orden big-endian, y se escribe en base64url
/// sin relleno. La Cita guarda solo <see cref="Hash"/>: SHA-256 de los 32 bytes del token. Nunca va a registros,
/// bitácora, eventos ni outbox (AD-23): <see cref="ToString"/> no lo muestra.
/// </summary>
public sealed class EnlaceDeGestion
{
    private readonly byte[] _hash;

    private EnlaceDeGestion(string token, byte[] hash)
    {
        Token = token;
        _hash = hash;
    }

    /// <summary>Base64url sin relleno: 43 caracteres. Es lo que va después de <c>/c/</c> en el enlace.</summary>
    public string Token { get; }

    /// <summary>SHA-256 del token, 32 bytes: lo único que guarda la Cita. Una copia.</summary>
    public byte[] Hash => (byte[])_hash.Clone();

    /// <summary>El de <paramref name="cita"/> con <paramref name="clave"/>: el mismo cada vez.</summary>
    public static EnlaceDeGestion Derivar(ClaveDeGestion clave, CitaId cita)
    {
        ArgumentNullException.ThrowIfNull(clave);

        var token = HMACSHA256.HashData(clave.Bytes, cita.Valor.ToByteArray(bigEndian: true));
        return new EnlaceDeGestion(Base64Url(token), SHA256.HashData(token));
    }

    /// <summary>
    /// El hash con el que se busca la Cita de un token que llega en una dirección (AD-19): SHA-256 de sus 32 bytes.
    /// Nulo si el token no es base64url sin relleno de 32 bytes: ese token no es de ninguna Cita.
    /// </summary>
    public static byte[]? HashDelToken(string? token)
    {
        if (token is not { Length: 43 })
        {
            return null;
        }

        Span<byte> bytes = stackalloc byte[33];
        var base64 = token.Replace('-', '+').Replace('_', '/') + "=";
        if (!Convert.TryFromBase64String(base64, bytes, out var escritos) || escritos != 32
            || Base64Url(bytes[..32].ToArray()) != token)
        {
            return null;
        }

        return SHA256.HashData(bytes[..32]);
    }

    public override string ToString() => "EnlaceDeGestion(oculto)";

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
