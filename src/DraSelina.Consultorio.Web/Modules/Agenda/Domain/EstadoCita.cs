namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>Los únicos tres estados que guarda una Cita (INV-C7, AD-9). Los dos últimos son finales.</summary>
public enum EstadoCita
{
    Agendada = 1,
    NoSePresento = 2,
    Cancelada = 3,
}

/// <summary>Por qué se canceló una Cita. Existe si y solo si está <see cref="EstadoCita.Cancelada"/> (INV-C8).</summary>
public enum MotivoDeCancelacion
{
    Paciente = 1,
    Tarde = 2,
    Consultorio = 3,
}

/// <summary>Quién creó la Cita. No cambia después de creada (INV-C10).</summary>
public enum OrigenDeCita
{
    /// <summary>La creó el Paciente desde el sitio.</summary>
    EnLinea = 1,

    /// <summary>La registró el Administrador a mano.</summary>
    Consultorio = 2,
}
