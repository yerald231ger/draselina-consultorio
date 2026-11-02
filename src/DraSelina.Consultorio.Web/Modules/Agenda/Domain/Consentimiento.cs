namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// La aceptación del aviso de privacidad al crear una Cita (AD-24, RN-23): cuándo, qué versión del aviso y por qué
/// canal. Los tres son obligatorios.
/// </summary>
public sealed record Consentimiento
{
    private Consentimiento(DateTimeOffset en, string versionDelAviso, CanalDeConsentimiento canal)
    {
        En = en;
        VersionDelAviso = versionDelAviso;
        Canal = canal;
    }

    /// <summary>Cuándo se aceptó, en UTC.</summary>
    public DateTimeOffset En { get; }

    public string VersionDelAviso { get; }

    public CanalDeConsentimiento Canal { get; }

    public static Consentimiento Crear(DateTimeOffset en, string versionDelAviso, CanalDeConsentimiento canal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionDelAviso);
        if (!Enum.IsDefined(canal))
        {
            throw new ArgumentOutOfRangeException(nameof(canal), canal, "Canal de consentimiento desconocido.");
        }

        return new Consentimiento(en.ToUniversalTime(), versionDelAviso, canal);
    }

    /// <summary>Reconstitución desde la base: no valida (T-6).</summary>
    internal static Consentimiento Reconstituir(DateTimeOffset en, string versionDelAviso, CanalDeConsentimiento canal) =>
        new(en, versionDelAviso, canal);
}

/// <summary>Por dónde llegó el Consentimiento.</summary>
public enum CanalDeConsentimiento
{
    /// <summary>El Paciente marcó la casilla al reservar en el sitio.</summary>
    EnLinea = 1,

    /// <summary>Lo obtuvo el consultorio por fuera del sitio, al registrar una Cita del consultorio.</summary>
    Externo = 2,
}
