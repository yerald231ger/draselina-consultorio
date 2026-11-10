using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// El Detalle de una Cita para el Administrador (entrada 6). Una consulta: no abre transacción ni cambia estado. Reúne la
/// Cita sin rastrear con el nombre de su tipo, su bitácora y el contacto completo de su Paciente, que da Pacientes
/// (AD-10, RN-29): la página no compone módulos ni decide reglas. El estado es el de la Agenda
/// (<see cref="EstadoMostrado"/>); las acciones que proceden las decide la Cita con la misma regla de sus transiciones
/// (<see cref="Cita.PuedeMarcarNoSePresento"/>, <see cref="Cita.PuedeCancelarPorConsultorio"/>, RN-31). Fechas y horas,
/// locales (AD-17). Ni este handler ni sus puertos registran nombre, teléfono, correo, nota ni motivo (AD-23).
/// </summary>
public sealed class DetalleDeCitaHandler(
    ICitaDelDetalle citas, IBitacoraDeLaCita bitacora, IPacientesApi pacientes, IClock reloj)
    : IQueryHandler<DetalleDeCita, CitaEnDetalle>
{
    public async Task<Resultado<CitaEnDetalle>> HandleAsync(DetalleDeCita consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (await citas.LeerAsync(consulta.CitaId, cancellationToken) is not { } leida)
        {
            return Resultado.Fallo(CodigosDeAgenda.CitaNoEncontrada).Como<CitaEnDetalle>();
        }

        var cita = leida.Cita;
        var entradas = await bitacora.DeLaCitaAsync(cita.Id, cancellationToken);
        var contacto = await pacientes.ObtenerContactoConTelefonoAsync(cita.PacienteId, cancellationToken);
        var ahora = reloj.UtcNow;

        var (fecha, inicio) = HoraLocal.DesdeUtc(cita.Rango.Inicio);
        var fin = HoraLocal.DesdeUtc(cita.Rango.Fin).Hora;
        var (fechaDelConsentimiento, horaDelConsentimiento) = HoraLocal.DesdeUtc(cita.Consentimiento.En);

        return Resultado.Exito(new CitaEnDetalle(
            cita.Id,
            contacto is null ? null : new ContactoDelPaciente(contacto.Nombre, contacto.Telefono, contacto.Correo),
            leida.TipoDeVisita,
            fecha,
            inicio,
            fin,
            Origen(cita.Origen),
            cita.Nota,
            cita.MotivoDeConsulta,
            new ConsentimientoEnDetalle(
                Canal(cita.Consentimiento.Canal), fechaDelConsentimiento, horaDelConsentimiento, cita.Consentimiento.VersionDelAviso),
            EstadoMostrado.Estado(cita.Estado, cita.Rango.Inicio, ahora),
            EstadoMostrado.Cancelacion(cita.MotivoDeCancelacion),
            SinCorreo: contacto?.Correo is null,
            cita.PuedeMarcarNoSePresento(ahora),
            cita.PuedeCancelarPorConsultorio(ahora),
            [.. entradas.Select(Entrada)]));
    }

    private static EntradaDeLaBitacora Entrada(EntradaLeidaDeLaBitacora entrada)
    {
        var (fecha, hora) = HoraLocal.DesdeUtc(entrada.OcurridoEn);
        var accion = entrada.Accion switch
        {
            AccionDeBitacora.Agendada => AccionEnLaBitacora.Agendada,
            AccionDeBitacora.Reprogramada => AccionEnLaBitacora.Reprogramada,
            AccionDeBitacora.Cancelada => AccionEnLaBitacora.Cancelada,
            AccionDeBitacora.NoSePresento => AccionEnLaBitacora.NoSePresento,
            _ => throw new InvalidOperationException($"Acción de bitácora sin lugar en una Cita: {entrada.Accion}."),
        };

        return new EntradaDeLaBitacora(
            fecha,
            hora,
            entrada.Actor,
            accion,
            accion == AccionEnLaBitacora.Agendada && entrada.Origen is { } origen ? Origen(origen) : null,
            accion == AccionEnLaBitacora.Cancelada ? EstadoMostrado.Cancelacion(entrada.Motivo) : null,
            accion == AccionEnLaBitacora.Reprogramada ? Momento(entrada.InicioAnterior) : null,
            accion == AccionEnLaBitacora.Reprogramada ? Momento(entrada.InicioNuevo) : null);
    }

    private static MomentoLocal? Momento(DateTimeOffset? instante)
    {
        if (instante is not { } valor)
        {
            return null;
        }

        var (fecha, hora) = HoraLocal.DesdeUtc(valor);
        return new MomentoLocal(fecha, hora);
    }

    private static OrigenEnLaAgenda Origen(OrigenDeCita origen) => origen switch
    {
        OrigenDeCita.EnLinea => OrigenEnLaAgenda.EnLinea,
        OrigenDeCita.Consultorio => OrigenEnLaAgenda.DelConsultorio,
        _ => throw new InvalidOperationException($"Origen de Cita desconocido: {origen}."),
    };

    private static CanalEnDetalle Canal(CanalDeConsentimiento canal) => canal switch
    {
        CanalDeConsentimiento.EnLinea => CanalEnDetalle.EnLinea,
        CanalDeConsentimiento.Externo => CanalEnDetalle.Externo,
        _ => throw new InvalidOperationException($"Canal de consentimiento desconocido: {canal}."),
    };
}
