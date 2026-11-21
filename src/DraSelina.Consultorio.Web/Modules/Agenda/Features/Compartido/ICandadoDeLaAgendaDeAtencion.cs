namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// El candado de toda la agenda de atención: cambiar la base, guardar y borrar una agenda custom hacen fila aquí antes de
/// leer la agenda, porque guardarla sincroniza la base y todas las custom desde hoy con lo que se leyó; sin fila, el
/// último de dos guardados a la vez deshacía al otro (entrada 6.10). Las custom lo toman después de su candado de fecha
/// (<see cref="ICandadoDeFecha"/>); las reservas no lo toman. Se toma dentro de la transacción abierta del handler y se
/// suelta al confirmarla o deshacerla.
/// </summary>
public interface ICandadoDeLaAgendaDeAtencion
{
    /// <summary>Espera hasta tener el candado. Lanza si no hay una transacción abierta.</summary>
    Task TomarAsync(CancellationToken cancellationToken);
}
