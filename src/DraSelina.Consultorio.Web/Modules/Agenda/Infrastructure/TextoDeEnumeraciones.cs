using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// Cómo se guarda cada enumeración de la Cita: <c>text</c> con <c>CHECK</c> (modelo de datos, Parte II). Los valores
/// son los de las restricciones de <see cref="AgendaDbContext"/>.
/// </summary>
internal static class TextoDeEnumeraciones
{
    public static string Estado(EstadoCita estado) => estado switch
    {
        EstadoCita.Agendada => "Agendada",
        EstadoCita.NoSePresento => "NoSePresento",
        EstadoCita.Cancelada => "Cancelada",
        _ => throw Desconocido(estado),
    };

    public static EstadoCita Estado(string texto) => texto switch
    {
        "Agendada" => EstadoCita.Agendada,
        "NoSePresento" => EstadoCita.NoSePresento,
        "Cancelada" => EstadoCita.Cancelada,
        _ => throw Desconocido(texto),
    };

    public static string Origen(OrigenDeCita origen) => origen switch
    {
        OrigenDeCita.EnLinea => "en_linea",
        OrigenDeCita.Consultorio => "consultorio",
        _ => throw Desconocido(origen),
    };

    public static OrigenDeCita Origen(string texto) => texto switch
    {
        "en_linea" => OrigenDeCita.EnLinea,
        "consultorio" => OrigenDeCita.Consultorio,
        _ => throw Desconocido(texto),
    };

    public static string Motivo(MotivoDeCancelacion motivo) => motivo switch
    {
        MotivoDeCancelacion.Paciente => "paciente",
        MotivoDeCancelacion.Tarde => "tarde",
        MotivoDeCancelacion.Consultorio => "consultorio",
        _ => throw Desconocido(motivo),
    };

    public static MotivoDeCancelacion? Motivo(string texto) => texto switch
    {
        "paciente" => MotivoDeCancelacion.Paciente,
        "tarde" => MotivoDeCancelacion.Tarde,
        "consultorio" => MotivoDeCancelacion.Consultorio,
        _ => throw Desconocido(texto),
    };

    public static string Canal(CanalDeConsentimiento canal) => canal switch
    {
        CanalDeConsentimiento.EnLinea => "en_linea",
        CanalDeConsentimiento.Externo => "externo",
        _ => throw Desconocido(canal),
    };

    public static CanalDeConsentimiento Canal(string texto) => texto switch
    {
        "en_linea" => CanalDeConsentimiento.EnLinea,
        "externo" => CanalDeConsentimiento.Externo,
        _ => throw Desconocido(texto),
    };

    private static InvalidOperationException Desconocido(object valor) => new($"Valor de enumeración desconocido: '{valor}'.");
}
