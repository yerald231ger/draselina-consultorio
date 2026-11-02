using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Pacientes.Domain;

/// <summary>
/// Una persona que tiene o tuvo una Cita. Se reconoce por teléfono y nombre (RN-25); esa igualdad la decide el índice
/// único de la base, no este agregado (RN-26). No conoce citas (RN-29).
/// </summary>
public sealed class Paciente
{
    // Para la reconstitución desde la base (T-6).
    private Paciente()
    {
        Nombre = null!;
        Telefono = null!;
    }

    public PatientId Id { get; private set; }

    public NombreCompleto Nombre { get; private set; }

    public Telefono Telefono { get; private set; }

    /// <summary>Opcional (INV-P1).</summary>
    public CorreoElectronico? Correo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    /// <summary>Registra un Paciente nuevo, con su id Guid v7 (T-10, T-15). INV-P1 e INV-P2.</summary>
    public static Paciente Registrar(Telefono telefono, NombreCompleto nombre, CorreoElectronico? correo, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(telefono);
        ArgumentNullException.ThrowIfNull(nombre);

        return new Paciente
        {
            Id = PatientId.Nuevo(),
            Nombre = nombre,
            Telefono = telefono,
            Correo = correo,
            CreadoEn = now,
            ActualizadoEn = now,
        };
    }

    /// <summary>
    /// El correo más reciente manda (RN-30): si es otra dirección, reemplaza a la guardada. Devuelve verdadero si
    /// cambió.
    /// </summary>
    public bool ActualizarCorreo(CorreoElectronico correo, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(correo);

        if (Correo is not null && Correo.EsLaMismaDireccionQue(correo))
        {
            return false;
        }

        Correo = correo;
        ActualizadoEn = now;
        return true;
    }
}
