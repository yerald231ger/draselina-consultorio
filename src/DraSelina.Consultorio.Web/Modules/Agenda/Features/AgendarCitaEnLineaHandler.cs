using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Agenda una Cita en línea. Orquesta y no decide (T-13): pide el Paciente a Pacientes, que confirma en su propia
/// transacción antes de la de Agenda (AD-28); abre la transacción de Agenda, crea la Cita con su Consentimiento por
/// <see cref="Cita.Agendar"/> y la guarda bajo la restricción de exclusión (AD-8). Ni este handler ni sus puertos
/// registran nombre, teléfono, correo ni motivo (AD-23).
/// </summary>
public sealed class AgendarCitaEnLineaHandler(
    ITiposDeVisita tiposDeVisita,
    IPacientesApi pacientes,
    IContenidoApi contenido,
    ITransaccionDeAgenda transaccion,
    ICitas citas,
    IClock reloj) : ICommandHandler<AgendarCitaEnLinea, CitaAgendadaEnLinea>
{
    public async Task<Resultado<CitaAgendadaEnLinea>> HandleAsync(
        AgendarCitaEnLinea comando,
        CancellationToken cancellationToken)
    {
        // La validación de forma (AD-12) ya garantizó que estos no son nulos.
        var fecha = comando.Fecha!.Value;
        var hora = comando.Hora!.Value;
        var ahora = reloj.UtcNow;

        var tipo = await tiposDeVisita.ObtenerAsync(comando.TipoDeVisitaId!.Value, cancellationToken);
        if (tipo is null)
        {
            return Resultado.Fallo(CodigosDeAgenda.TipoNoReservable).Como<CitaAgendadaEnLinea>();
        }

        // Fecha y hora locales del consultorio; la Cita guarda instantes UTC (AD-17).
        var inicio = HoraLocal.AUtc(fecha, hora);

        var paciente = await pacientes.BuscarOCrearAsync(comando.Nombre!, comando.Telefono!, comando.Correo, cancellationToken);
        if (!paciente.EsExito)
        {
            return DatoDelPacienteInvalido(paciente);
        }

        var consentimiento = Consentimiento.Crear(ahora, contenido.VersionDelAvisoDePrivacidad, CanalDeConsentimiento.EnLinea);

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        var rechazo = Cita.Agendar(paciente.Valor, tipo, inicio, comando.Motivo!, consentimiento, ahora, out var cita);
        if (rechazo is not null)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(Codigo(rechazo.Value)).Como<CitaAgendadaEnLinea>();
        }

        citas.Agregar(cita!);
        if (await citas.GuardarAsync(cancellationToken) == GuardadoDeCita.HorarioOcupado)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAgenda.HorarioOcupado).Como<CitaAgendadaEnLinea>();
        }

        await tx.ConfirmarAsync(cancellationToken);

        var consultorio = contenido.DatosDelConsultorio;
        return new CitaAgendadaEnLinea(
            cita!.Id,
            fecha,
            hora,
            tipo.Nombre,
            tipo.DuracionMin,
            consultorio.Lugar,
            $"{consultorio.Direccion}, {consultorio.Ciudad}");
    }

    private static string Codigo(RechazoAlAgendar rechazo) => rechazo switch
    {
        RechazoAlAgendar.TipoNoReservable => CodigosDeAgenda.TipoNoReservable,
        RechazoAlAgendar.EnElPasado => CodigosDeAgenda.EnElPasado,
        _ => throw new ArgumentOutOfRangeException(nameof(rechazo), rechazo, "Rechazo sin código."),
    };

    // Lo que Pacientes rechaza de los datos es un error de forma del comando: va bajo su propiedad (AD-12).
    private static Resultado<CitaAgendadaEnLinea> DatoDelPacienteInvalido(Resultado paciente) =>
        paciente.Codigo switch
        {
            CodigosDePacientes.TelefonoInvalido => Resultado.Invalido(
                nameof(AgendarCitaEnLinea.Telefono), AgendarCitaEnLinea.MensajeDeTelefonoDeMexico).Como<CitaAgendadaEnLinea>(),
            CodigosDePacientes.NombreInvalido => Resultado.Invalido(
                nameof(AgendarCitaEnLinea.Nombre), AgendarCitaEnLinea.MensajeDeNombre).Como<CitaAgendadaEnLinea>(),
            CodigosDePacientes.CorreoInvalido => Resultado.Invalido(
                nameof(AgendarCitaEnLinea.Correo), AgendarCitaEnLinea.MensajeDeCorreo).Como<CitaAgendadaEnLinea>(),
            _ => paciente.Como<CitaAgendadaEnLinea>(),
        };
}
