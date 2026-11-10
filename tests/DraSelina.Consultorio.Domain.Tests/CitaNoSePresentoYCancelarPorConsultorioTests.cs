using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary>
/// <see cref="Cita.MarcarNoSePresento"/> y <see cref="Cita.CancelarPorConsultorio"/> sin base de datos (AD-9, RN-31,
/// INV-C7, INV-C8). La Cita, Agendada el martes 3 de noviembre de 2026 a las 16:00 local (22:00 UTC), de 30 minutos; se
/// agenda el lunes 2 a las 12:00 local.
/// </summary>
public class CitaNoSePresentoYCancelarPorConsultorioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Martes = new(2026, 11, 3);
    private static readonly DateTimeOffset Inicio = HoraLocal.AUtc(Martes, new TimeOnly(16, 0));

    private static readonly ClaveDeGestion Clave = ClaveDeGestion.Crear("clave-de-prueba-de-32-bytes-o-mas"u8);

    // Cita.MarcarNoSePresento.

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(60 * 24 * 3)]
    public void Desde_el_inicio_se_marca_No_se_presento_sin_motivo_ni_instante_de_cancelacion(int minutosDespues)
    {
        var cita = Agendada();

        Assert.Null(cita.MarcarNoSePresento(Inicio.AddMinutes(minutosDespues)));

        Assert.Equal(EstadoCita.NoSePresento, cita.Estado);
        Assert.Null(cita.MotivoDeCancelacion);
        Assert.Null(cita.CanceladaEn);
        Assert.Equal(Inicio, cita.Rango.Inicio);
        Assert.Equal(Inicio.AddMinutes(30), cita.Rango.Fin);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(1)]
    public void Antes_del_inicio_aun_no_empieza_y_no_cambia_nada(int minutosAntes)
    {
        var cita = Agendada();

        Assert.Equal(RechazoAlMarcarNoSePresento.AunNoEmpieza, cita.MarcarNoSePresento(Inicio.AddMinutes(-minutosAntes)));
        Assert.Equal(EstadoCita.Agendada, cita.Estado);
    }

    [Fact]
    public void Un_instante_antes_del_inicio_aun_no_empieza()
    {
        Assert.Equal(RechazoAlMarcarNoSePresento.AunNoEmpieza, Agendada().MarcarNoSePresento(Inicio.AddTicks(-1)));
    }

    [Fact]
    public void Una_Cita_cancelada_por_el_Paciente_no_se_marca()
    {
        var cita = Agendada();
        Assert.Null(cita.Cancelar(Inicio.AddHours(-3), Reglas()));

        Assert.Equal(RechazoAlMarcarNoSePresento.CitaNoAgendada, cita.MarcarNoSePresento(Inicio.AddMinutes(10)));
        Assert.Equal(EstadoCita.Cancelada, cita.Estado);
        Assert.Equal(MotivoDeCancelacion.Paciente, cita.MotivoDeCancelacion);
    }

    [Fact]
    public void Una_Cita_cancelada_por_el_Consultorio_no_se_marca()
    {
        var cita = Agendada();
        Assert.Null(cita.CancelarPorConsultorio(Inicio.AddHours(-3)));

        Assert.Equal(RechazoAlMarcarNoSePresento.CitaNoAgendada, cita.MarcarNoSePresento(Inicio.AddMinutes(10)));
        Assert.Equal(EstadoCita.Cancelada, cita.Estado);
    }

    [Fact]
    public void No_se_presento_es_final()
    {
        var cita = Agendada();
        Assert.Null(cita.MarcarNoSePresento(Inicio.AddMinutes(10)));

        // INV-C7: ni otra marca, ni cancelación, ni reprogramación la sacan de ahí.
        Assert.Equal(RechazoAlMarcarNoSePresento.CitaNoAgendada, cita.MarcarNoSePresento(Inicio.AddMinutes(20)));
        Assert.Equal(RechazoAlCancelar.CitaNoAgendada, cita.CancelarPorConsultorio(Inicio.AddMinutes(-30)));
        Assert.Equal(RechazoAlCancelar.CitaNoAgendada, cita.Cancelar(Inicio.AddMinutes(-30), Reglas()));
        Assert.Equal(EstadoCita.NoSePresento, cita.Estado);
        Assert.Null(cita.MotivoDeCancelacion);
        Assert.Null(cita.CanceladaEn);
    }

    // Cita.CancelarPorConsultorio.

    [Theory]
    [InlineData(60 * 24)]
    [InlineData(180)]
    [InlineData(10)]
    [InlineData(1)]
    public void Antes_del_inicio_se_cancela_por_el_Consultorio_sin_umbral_de_tardia(int minutosAntes)
    {
        var cita = Agendada();
        var ahora = Inicio.AddMinutes(-minutosAntes);

        Assert.Null(cita.CancelarPorConsultorio(ahora));

        // Ni a un minuto es tardía: el Consultorio nunca cancela tarde.
        Assert.Equal(EstadoCita.Cancelada, cita.Estado);
        Assert.Equal(MotivoDeCancelacion.Consultorio, cita.MotivoDeCancelacion);
        Assert.Equal(ahora, cita.CanceladaEn);
        Assert.Equal(TimeSpan.Zero, cita.CanceladaEn!.Value.Offset);
        Assert.Equal(Inicio, cita.Rango.Inicio);
        Assert.Equal(Inicio.AddMinutes(30), cita.Rango.Fin);
    }

    [Fact]
    public void El_instante_de_la_cancelacion_queda_en_UTC()
    {
        var cita = Agendada();
        var enMexico = Inicio.AddHours(-3).ToOffset(TimeSpan.FromHours(-6));

        Assert.Null(cita.CancelarPorConsultorio(enMexico));
        Assert.Equal(TimeSpan.Zero, cita.CanceladaEn!.Value.Offset);
        Assert.Equal(enMexico, cita.CanceladaEn);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Desde_el_inicio_ya_empezo_y_no_cambia_nada(int minutosDespues)
    {
        var cita = Agendada();

        Assert.Equal(RechazoAlCancelar.YaEmpezo, cita.CancelarPorConsultorio(Inicio.AddMinutes(minutosDespues)));
        Assert.Equal(EstadoCita.Agendada, cita.Estado);
        Assert.Null(cita.MotivoDeCancelacion);
        Assert.Null(cita.CanceladaEn);
    }

    [Fact]
    public void Una_Cita_que_no_se_presento_no_se_cancela()
    {
        var cita = Agendada();
        Assert.Null(cita.MarcarNoSePresento(Inicio));

        Assert.Equal(RechazoAlCancelar.CitaNoAgendada, cita.CancelarPorConsultorio(Inicio.AddMinutes(-10)));
        Assert.Equal(EstadoCita.NoSePresento, cita.Estado);
        Assert.Null(cita.MotivoDeCancelacion);
    }

    [Fact]
    public void Una_Cita_cancelada_por_el_Paciente_no_cambia_de_motivo()
    {
        var cita = Agendada();
        var primera = Inicio.AddHours(-3);
        Assert.Null(cita.Cancelar(primera, Reglas()));

        Assert.Equal(RechazoAlCancelar.CitaNoAgendada, cita.CancelarPorConsultorio(Inicio.AddHours(-2)));
        Assert.Equal(MotivoDeCancelacion.Paciente, cita.MotivoDeCancelacion);
        Assert.Equal(primera, cita.CanceladaEn);
    }

    [Fact]
    public void Cancelar_dos_veces_no_cambia_lo_guardado_la_primera()
    {
        var cita = Agendada();
        var primera = Inicio.AddHours(-3);

        Assert.Null(cita.CancelarPorConsultorio(primera));
        Assert.Equal(RechazoAlCancelar.CitaNoAgendada, cita.CancelarPorConsultorio(Inicio.AddHours(-1)));
        Assert.Equal(MotivoDeCancelacion.Consultorio, cita.MotivoDeCancelacion);
        Assert.Equal(primera, cita.CanceladaEn);
    }

    [Fact]
    public void Una_Cita_en_linea_tambien_se_cancela_por_el_Consultorio()
    {
        var cita = EnLinea();

        Assert.Null(cita.CancelarPorConsultorio(Inicio.AddHours(-3)));
        Assert.Equal(MotivoDeCancelacion.Consultorio, cita.MotivoDeCancelacion);
        Assert.Equal(OrigenDeCita.EnLinea, cita.Origen);
    }

    // Lo que procede ahora (entrada 6): la misma regla de las transiciones.

    [Theory]
    [InlineData(-1, false, true)]
    [InlineData(0, true, false)]
    [InlineData(10, true, false)]
    public void Agendada_procede_cancelar_antes_del_inicio_y_marcar_desde_el_inicio(int minutos, bool marcar, bool cancelar)
    {
        var cita = Agendada();
        var ahora = Inicio.AddMinutes(minutos);

        Assert.Equal(marcar, cita.PuedeMarcarNoSePresento(ahora));
        Assert.Equal(cancelar, cita.PuedeCancelarPorConsultorio(ahora));

        // Las transiciones deciden igual.
        Assert.Equal(marcar, Agendada().MarcarNoSePresento(ahora) is null);
        Assert.Equal(cancelar, Agendada().CancelarPorConsultorio(ahora) is null);
    }

    [Fact]
    public void Un_instante_antes_del_inicio_procede_cancelar_y_no_marcar()
    {
        var cita = Agendada();

        Assert.True(cita.PuedeCancelarPorConsultorio(Inicio.AddTicks(-1)));
        Assert.False(cita.PuedeMarcarNoSePresento(Inicio.AddTicks(-1)));
    }

    [Fact]
    public void Cancelada_o_No_se_presento_no_procede_ninguna()
    {
        var cancelada = Agendada();
        Assert.Null(cancelada.CancelarPorConsultorio(Inicio.AddHours(-1)));
        var marcada = Agendada();
        Assert.Null(marcada.MarcarNoSePresento(Inicio.AddMinutes(5)));

        foreach (var ahora in new[] { Inicio.AddHours(-2), Inicio.AddHours(2) })
        {
            Assert.False(cancelada.PuedeMarcarNoSePresento(ahora));
            Assert.False(cancelada.PuedeCancelarPorConsultorio(ahora));
            Assert.False(marcada.PuedeMarcarNoSePresento(ahora));
            Assert.False(marcada.PuedeCancelarPorConsultorio(ahora));
        }
    }

    private static Cita Agendada()
    {
        Assert.Null(Cita.AgendarPorConsultorio(
            PatientId.Nuevo(), Tipo(), Inicio, null, null,
            Consentimiento.Crear(Ahora, "2026-11-borrador", CanalDeConsentimiento.Externo),
            Reglas().TramosVigentes, Ahora, Clave, out var cita));
        return cita!;
    }

    private static Cita EnLinea()
    {
        Assert.Null(Cita.Agendar(
            Guid.CreateVersion7(), PatientId.Nuevo(), Tipo(), Inicio, "Dolor",
            Consentimiento.Crear(Ahora, "2026-11-borrador", CanalDeConsentimiento.EnLinea),
            Reglas(), 0, Ahora, Clave, out var cita));
        return cita!;
    }

    /// <summary>Las reglas sembradas con los Tramos del martes: 15:30 a 18:00, umbral de 120.</summary>
    private static ReglasDeAgenda Reglas()
    {
        var agenda = AgendaAtencion.Crear();
        Assert.Null(agenda.CambiarBase(DayOfWeek.Tuesday, [Tramo.Crear(new TimeOnly(15, 30), new TimeOnly(18, 0))], TimeSpan.FromMinutes(30)));
        Assert.Null(ReglasDeReserva.Crear(1, 2, 120, 3, out var reserva));
        return ReglasDeAgenda.Crear(agenda.TramosVigentes(Martes), reserva!);
    }

    private static TipoDeVisita Tipo() => TipoDeVisita.Crear("Primera consulta", 30, reservableEnLinea: true, orden: 1);
}
