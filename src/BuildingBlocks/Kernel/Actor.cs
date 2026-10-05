namespace DraSelina.Consultorio.Kernel;

/// <summary>
/// Quién hace un cambio en el dominio: exactamente uno de estos tres (E-3).
/// Empieza en 1 para que el valor por omisión (0) no sea un actor válido.
/// </summary>
public enum Actor
{
    Paciente = 1,
    Administrador = 2,
    Sistema = 3,
}
