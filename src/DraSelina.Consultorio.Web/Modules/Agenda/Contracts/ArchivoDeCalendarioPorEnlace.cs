using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// El archivo de calendario de la Cita cuyo Enlace de gestión lleva <see cref="Token"/> (FR-5): se busca por el hash
/// del token, nunca por el id (AD-19). Solo una Cita Agendada cuyo inicio no ha pasado lo entrega; todo lo demás es
/// <see cref="CodigosDeAgenda.CitaNoEncontrada"/>, sin decir por qué (RN-22). Una lectura: sin actor, bitácora,
/// transacción ni eventos. El token nunca va a registros (AD-23): <see cref="ToString"/> no lo muestra.
/// </summary>
/// <param name="Token">Lo que va después de <c>/c/</c> en el Enlace de gestión.</param>
public sealed record ArchivoDeCalendarioPorEnlace(string Token) : IQuery<ArchivoDeCalendario>
{
    public override string ToString() => "ArchivoDeCalendarioPorEnlace(oculto)";
}
