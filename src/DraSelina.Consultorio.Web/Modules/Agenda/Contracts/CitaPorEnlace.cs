using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// La Cita cuyo Enlace de gestión lleva <see cref="Token"/>, para la página Tu cita (CAP-6, FR-8): se busca por el hash
/// del token, nunca por el id (AD-19). Solo una Cita cuyo inicio no ha pasado (RN-22, RN-31), Agendada o Cancelada, se
/// muestra; todo lo demás es <see cref="CodigosDeAgenda.CitaNoEncontrada"/>, sin decir por qué. Una lectura: sin actor,
/// bitácora, transacción ni eventos. El token nunca va a registros (AD-23): <see cref="ToString"/> no lo muestra.
/// </summary>
/// <param name="Token">Lo que va después de <c>/c/</c> en el Enlace de gestión.</param>
public sealed record CitaPorEnlace(string Token) : IQuery<CitaDelEnlace>
{
    public override string ToString() => "CitaPorEnlace(oculto)";
}

/// <summary>Lo que Tu cita muestra de una Cita (mockup Gestionar). Lo leen también Reprogramar y Cancelar.</summary>
/// <param name="Fecha">Fecha local del consultorio.</param>
/// <param name="Hora">Hora local de inicio.</param>
/// <param name="TipoDeVisita">El nombre de hoy del tipo de visita.</param>
/// <param name="DuracionMin">Lo que dura la Cita, de su inicio a su fin.</param>
/// <param name="Paciente">El nombre del Paciente, tal como se guardó (RN-29). Nunca va a registros (AD-23).</param>
/// <param name="Lugar">Hospital y consultorio, de Contenido.</param>
/// <param name="Estado">Cómo se ve la Cita en Tu cita.</param>
/// <param name="CitaId">
/// Solo para pedir <see cref="HorariosLibres"/> sin la propia Cita: la interfaz nunca lo pinta ni lo pone en una
/// dirección (AD-19, AD-23).
/// </param>
/// <param name="TipoDeVisitaId">El tipo de la Cita: al reprogramar no cambia.</param>
/// <param name="PuedeReprogramar">
/// Agendada y con al menos <paramref name="UmbralMin"/> minutos antes de su inicio (RN-17).
/// </param>
/// <param name="UmbralMin">El umbral de reprogramación de las reglas de reserva, en minutos.</param>
public sealed record CitaDelEnlace(
    DateOnly Fecha,
    TimeOnly Hora,
    string TipoDeVisita,
    int DuracionMin,
    string Paciente,
    string Lugar,
    EstadoDelEnlace Estado,
    CitaId CitaId,
    TipoDeVisitaId TipoDeVisitaId,
    bool PuedeReprogramar,
    int UmbralMin)
{
    // El nombre del Paciente no se escribe al registrar la Cita (AD-23).
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append(
            $"Fecha = {Fecha}, Hora = {Hora}, TipoDeVisita = {TipoDeVisita}, DuracionMin = {DuracionMin}, Estado = {Estado}, "
            + $"CitaId = {CitaId}, TipoDeVisitaId = {TipoDeVisitaId}, PuedeReprogramar = {PuedeReprogramar}, UmbralMin = {UmbralMin}");
        return true;
    }
}

/// <summary>
/// El estado de una Cita como lo ve el Paciente en Tu cita. La cancelada por el Paciente, a tiempo o tarde, se ve igual
/// (Gerardo, 2026-11-06).
/// </summary>
public enum EstadoDelEnlace
{
    Agendada = 1,
    CanceladaPorElPaciente = 2,
    CanceladaPorElConsultorio = 3,
}
