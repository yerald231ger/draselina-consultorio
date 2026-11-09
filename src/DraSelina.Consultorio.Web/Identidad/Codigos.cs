namespace DraSelina.Consultorio.Web.Identidad;

/// <summary>Cómo se limpia lo que se escribe en un campo de código antes de pasarlo a Identity.</summary>
public static class Codigos
{
    /// <summary>El código de la app: sin espacios ni guiones ("123 456" es "123456").</summary>
    public static string Limpiar(string? codigo) =>
        (codigo ?? "").Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);

    /// <summary>Un código de recuperación: sin espacios; el guion es parte del código.</summary>
    public static string LimpiarRecuperacion(string? codigo) => (codigo ?? "").Replace(" ", "", StringComparison.Ordinal);
}
