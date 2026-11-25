namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// La aceptación del aviso de privacidad al crear una Cita (AD-24, RN-23): cuándo, qué versión del aviso y por qué
/// canal, los tres obligatorios, y si incluye el consentimiento expreso para el Motivo de consulta, que es un dato de
/// salud (art. 8 LFPDPPP; Gerardo, 2026-11-24).
/// </summary>
public sealed record Consentimiento
{
    private Consentimiento(DateTimeOffset en, string versionDelAviso, CanalDeConsentimiento canal, bool incluyeMotivoDeConsulta)
    {
        En = en;
        VersionDelAviso = versionDelAviso;
        Canal = canal;
        IncluyeMotivoDeConsulta = incluyeMotivoDeConsulta;
    }

    /// <summary>Cuándo se aceptó, en UTC.</summary>
    public DateTimeOffset En { get; }

    public string VersionDelAviso { get; }

    public CanalDeConsentimiento Canal { get; }

    /// <summary>
    /// Se dio, aparte de la aceptación del aviso, el consentimiento expreso para usar el Motivo de consulta: en línea es
    /// la segunda casilla de Tus datos; por fuera del sitio, la casilla única de Agendar cita, que ya nombra los datos de
    /// salud (Gerardo, 2026-11-24).
    /// </summary>
    public bool IncluyeMotivoDeConsulta { get; }

    public static Consentimiento Crear(
        DateTimeOffset en, string versionDelAviso, CanalDeConsentimiento canal, bool incluyeMotivoDeConsulta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionDelAviso);
        if (!Enum.IsDefined(canal))
        {
            throw new ArgumentOutOfRangeException(nameof(canal), canal, "Canal de consentimiento desconocido.");
        }

        return new Consentimiento(en.ToUniversalTime(), versionDelAviso, canal, incluyeMotivoDeConsulta);
    }

    /// <summary>Reconstitución desde la base: no valida (T-6).</summary>
    internal static Consentimiento Reconstituir(
        DateTimeOffset en, string versionDelAviso, CanalDeConsentimiento canal, bool incluyeMotivoDeConsulta) =>
        new(en, versionDelAviso, canal, incluyeMotivoDeConsulta);
}

/// <summary>Por dónde llegó el Consentimiento.</summary>
public enum CanalDeConsentimiento
{
    /// <summary>El Paciente marcó la casilla al reservar en el sitio.</summary>
    EnLinea = 1,

    /// <summary>Lo obtuvo el consultorio por fuera del sitio, al registrar una Cita del consultorio.</summary>
    Externo = 2,
}
