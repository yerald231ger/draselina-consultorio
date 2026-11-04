using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>Lo que Agenda ofrece a otros módulos y a la interfaz (AD-3, AD-20).</summary>
public interface IAgendaApi
{
    /// <summary>
    /// El Enlace de gestión de la Cita: <c>&lt;url base&gt;/c/&lt;token&gt;</c>, con el token derivado de la clave del
    /// host y del id (AD-19, RN-22). Agenda es la única que lo arma; el mismo cada vez para la misma Cita. No se
    /// registra ni viaja en eventos (AD-23). No comprueba que la Cita exista ni que el enlace siga valiendo: eso es de
    /// quien lo valida (épica 3).
    /// </summary>
    string ObtenerEnlaceDeGestion(CitaId citaId);
}
