namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// La agenda base y las reglas de reserva iniciales (modelo de datos, Datos iniciales; AD-25): lunes a viernes de
/// 15:30 a 18:00; anticipación mínima 1 día, ventana 2 meses, umbral de cancelación tardía 120 min y tope semanal 3.
/// Los ids son fijos para que la migración los siembre una sola vez.
/// </summary>
public static class AgendaDeAtencionSembrada
{
    public static readonly TimeOnly Inicio = new(15, 30);

    public static readonly TimeOnly Fin = new(18, 0);

    public const int AnticipacionMinimaDias = 1;
    public const int VentanaMeses = 2;
    public const int UmbralCancelacionTardiaMin = 120;
    public const int TopeSemanalPorTelefono = 3;

    /// <summary>Lunes (1) a viernes (5), un tramo cada uno.</summary>
    internal static FilaDeTramoBase[] TramosBase =>
    [
        Tramo("01a19e61-3a10-7a01-8b10-6f2a3c4d5e01", 1),
        Tramo("01a19e61-3a10-7a02-8b10-6f2a3c4d5e02", 2),
        Tramo("01a19e61-3a10-7a03-8b10-6f2a3c4d5e03", 3),
        Tramo("01a19e61-3a10-7a04-8b10-6f2a3c4d5e04", 4),
        Tramo("01a19e61-3a10-7a05-8b10-6f2a3c4d5e05", 5),
    ];

    /// <summary>La única fila de <c>reglas_de_reserva</c>, con su id 1 en la propiedad oculta.</summary>
    internal static object[] Reglas =>
    [
        new
        {
            Id = (short)1,
            AnticipacionMinimaDias,
            VentanaMeses,
            UmbralCancelacionTardiaMin,
            TopeSemanalPorTelefono,
        },
    ];

    private static FilaDeTramoBase Tramo(string id, short dia) => new()
    {
        Id = Guid.Parse(id),
        DiaDeLaSemana = dia,
        Inicio = Inicio,
        Fin = Fin,
    };
}
