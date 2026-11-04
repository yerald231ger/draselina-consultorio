using System.Security.Claims;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// La reserva en línea de punta a punta contra PostgreSQL real: <see cref="AgendarCitaEnLinea"/> por su handler, con
/// Pacientes, Contenido y la restricción de exclusión de <c>agenda.cita</c>. Cada prueba usa su propia base migrada y
/// el reloj de la prueba, así sus horarios no chocan con los de otra.
/// </summary>
public sealed class ReservaEnLineaTests(Arnes arnes)
{
    /// <summary>Lunes 2 de noviembre de 2026, 12:00 en Ciudad de México.</summary>
    public static readonly DateTimeOffset Ahora = new(2026, 11, 2, 18, 0, 0, TimeSpan.Zero);

    /// <summary>Mañana, en hora local: martes.</summary>
    public static readonly DateOnly Manana = new(2026, 11, 3);

    /// <summary>La primera parte de la llave del candado por fecha de Agenda (<c>CandadoDeFecha</c>): "AGFE" en ASCII.</summary>
    private const int ClaseDeCandadoDeFecha = 0x41474645;

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Una_reserva_nueva_registra_al_Paciente_y_guarda_la_Cita_Agendada_en_linea_de_30_minutos()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await escenario.AgendarAsync(Comando());

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        var confirmada = resultado.Valor;
        Assert.Equal(Manana, confirmada.Fecha);
        Assert.Equal(new TimeOnly(16, 0), confirmada.Hora);
        Assert.Equal("Primera consulta", confirmada.TipoDeVisita);
        Assert.Equal(30, confirmada.DuracionMin);
        Assert.Equal("Hospital Star Médica, consultorio 101", confirmada.Lugar);
        Assert.Equal("Mariano Arista No. 735, Col. Barrio de Tequisquiapan, San Luis Potosí", confirmada.Direccion);

        var cita = Assert.Single(await escenario.CitasAsync());
        Assert.Equal(confirmada.CitaId.Valor, cita.Id);
        Assert.Equal(7, cita.Id.Version);
        Assert.Equal("Agendada", cita.Estado);
        Assert.Equal("en_linea", cita.Origen);
        Assert.Equal(TimeSpan.FromMinutes(30), cita.Fin - cita.Inicio);
        Assert.Equal(TiposDeVisitaSembrados.PrimeraConsulta.Valor, cita.TipoDeVisitaId);
        Assert.Equal("Dolor de rodilla derecha", cita.MotivoDeConsulta);
        Assert.Equal(Ahora, cita.ConsentimientoEn);
        Assert.Equal("2026-11-borrador", cita.ConsentimientoVersion);
        Assert.Equal("en_linea", cita.ConsentimientoCanal);

        var paciente = Assert.Single(await escenario.PacientesAsync());
        Assert.Equal(cita.PacienteId, paciente.Id);
        Assert.Equal("Ana Sofía López", paciente.Nombre);
        Assert.Equal("+524441234567", paciente.Telefono);
        Assert.Equal("ana@correo.com", paciente.Correo);
    }

    [Fact]
    public async Task La_Cita_queda_en_UTC_a_partir_de_la_hora_local_de_Ciudad_de_Mexico()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);

        // Las 16:00 de Ciudad de México (UTC-6) son las 22:00 UTC; así lo guarda timestamptz.
        var cita = Assert.Single(await escenario.CitasAsync());
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 22, 0, 0, TimeSpan.Zero), cita.Inicio);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 22, 30, 0, TimeSpan.Zero), cita.Fin);
        Assert.Equal(
            "2026-11-03 16:00:00",
            await escenario.EscalarAsync<string>(
                "select to_char(inicio at time zone 'America/Mexico_City', 'YYYY-MM-DD HH24:MI:SS') from agenda.cita"));
    }

    [Fact]
    public async Task El_mismo_telefono_y_el_mismo_nombre_sin_distinguir_mayusculas_acentos_ni_espacios_es_el_mismo_Paciente()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var primera = Comando(hora: new TimeOnly(16, 0));
        var segunda = Comando(hora: new TimeOnly(17, 0));
        segunda.Nombre = "  ana   sofia LOPEZ ";
        segunda.Telefono = "+52 444-123-4567";
        segunda.Correo = "ana.nueva@correo.com";

        Assert.True((await escenario.AgendarAsync(primera)).EsExito);
        Assert.True((await escenario.AgendarAsync(segunda)).EsExito);

        var paciente = Assert.Single(await escenario.PacientesAsync());
        Assert.All(await escenario.CitasAsync(), cita => Assert.Equal(paciente.Id, cita.PacienteId));

        // El correo más reciente reemplaza al guardado (RN-30); el nombre guardado es el del registro.
        Assert.Equal("ana.nueva@correo.com", paciente.Correo);
        Assert.Equal("Ana Sofía López", paciente.Nombre);
    }

    [Fact]
    public async Task Sin_correo_el_Paciente_conserva_el_que_tenia()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var segunda = Comando(hora: new TimeOnly(17, 0));
        segunda.Correo = null;

        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);
        Assert.True((await escenario.AgendarAsync(segunda)).EsExito);

        Assert.Equal("ana@correo.com", Assert.Single(await escenario.PacientesAsync()).Correo);
    }

    [Fact]
    public async Task El_mismo_telefono_con_otro_nombre_es_otro_Paciente()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var otra = Comando(hora: new TimeOnly(17, 0));
        otra.Nombre = "Mariana López";

        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);
        Assert.True((await escenario.AgendarAsync(otra)).EsExito);

        var pacientes = await escenario.PacientesAsync();
        Assert.Equal(2, pacientes.Count);
        Assert.Equal(2, (await escenario.CitasAsync()).Select(c => c.PacienteId).Distinct().Count());
    }

    [Fact]
    public async Task BuscarOCrear_simultaneo_con_los_mismos_datos_da_un_solo_Paciente()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var ids = await Task.WhenAll(Enumerable.Range(0, 6).Select(async i =>
        {
            await using var ambito = escenario.Web.Services.CreateAsyncScope();
            var api = ambito.ServiceProvider.GetRequiredService<IPacientesApi>();
            var nombre = i % 2 == 0 ? "Luis Pérez" : "LUIS  perez";
            var resultado = await api.BuscarOCrearAsync(nombre, "4449876543", correo: null, Cancelacion);
            Assert.True(resultado.EsExito);
            return resultado.Valor;
        }));

        Assert.Single(ids.Distinct());
        Assert.Single(await escenario.PacientesAsync());
    }

    [Fact]
    public async Task Una_Cita_traslapada_vuelve_como_horario_ocupado_y_no_se_crea()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // Una Cita del consultorio fuera de la rejilla, de 15:45 a 16:15, traslapa la de las 16:00.
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(15, 45), new TimeOnly(16, 15));
        var resultado = await escenario.AgendarAsync(Comando());

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.HorarioOcupado, resultado.Codigo);
        Assert.Equal("consultorio", Assert.Single(await escenario.CitasAsync()).Origen);

        // El Paciente sí queda: confirma antes que la Cita (AD-28, RN-28).
        Assert.Single(await escenario.PacientesAsync());
    }

    [Fact]
    public async Task Una_Cita_en_linea_contigua_a_una_del_consultorio_fuera_de_la_rejilla_no_choca()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(15, 30), new TimeOnly(16, 0));

        var resultado = await escenario.AgendarAsync(Comando());

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(2, (await escenario.CitasAsync()).Count);
    }

    [Fact]
    public async Task Una_Cita_contigua_no_choca()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        Assert.True((await escenario.AgendarAsync(Comando(hora: new TimeOnly(16, 0)))).EsExito);
        Assert.True((await escenario.AgendarAsync(Comando(hora: new TimeOnly(16, 30)))).EsExito);

        Assert.Equal(2, (await escenario.CitasAsync()).Count);
    }

    [Fact]
    public async Task Una_Cita_Cancelada_no_choca_con_una_nueva_en_su_horario()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);
        await escenario.EjecutarAsync(
            "update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'paciente', cancelada_en = now()");

        var resultado = await escenario.AgendarAsync(Comando());

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(2, (await escenario.CitasAsync()).Count);
    }

    // INV-C5 con la base sembrada (lunes a viernes, 15:30 a 18:00) y el reloj en el lunes 2 a las 12:00.

    [Theory]
    [InlineData("2026-11-03", "15:45")]
    [InlineData("2026-11-03", "17:45")]
    [InlineData("2026-11-03", "15:00")]
    [InlineData("2026-11-07", "16:00")]
    public async Task Fuera_de_la_rejilla_o_de_los_tramos_vuelve_como_fuera_de_horario_y_no_crea_Cita(string fecha, string hora)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await escenario.AgendarAsync(Comando(DateOnly.Parse(fecha), TimeOnly.Parse(hora)));

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.FueraDeHorario, resultado.Codigo);
        Assert.Empty(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Un_dia_con_custom_que_no_se_atiende_vuelve_como_fuera_de_horario()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCustomAsync(Manana, "cirugía");

        var resultado = await escenario.AgendarAsync(Comando());

        Assert.Equal(CodigosDeAgenda.FueraDeHorario, resultado.Codigo);
        Assert.Empty(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Una_custom_que_abre_un_sabado_deja_reservar_en_su_rejilla()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var sabado = new DateOnly(2026, 11, 7);
        await escenario.InsertarCustomAsync(sabado, "sábado abierto", (new TimeOnly(10, 0), new TimeOnly(11, 0)));

        var resultado = await escenario.AgendarAsync(Comando(sabado, new TimeOnly(10, 30)));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(HoraLocal.AUtc(sabado, new TimeOnly(10, 30)), Assert.Single(await escenario.CitasAsync()).Inicio);
    }

    [Fact]
    public async Task Hoy_con_anticipacion_de_un_dia_vuelve_como_fuera_de_ventana()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // Hoy lunes a las 16:00: futuro y en la rejilla, pero antes de mañana.
        var resultado = await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 2), new TimeOnly(16, 0)));

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.FueraDeVentana, resultado.Codigo);
        Assert.Empty(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Un_dia_despues_de_la_ventana_vuelve_como_fuera_de_ventana()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // Hoy + 2 meses + 1 día: domingo 3 de enero de 2027, abierto por una custom para que solo falle la ventana.
        var domingo = new DateOnly(2027, 1, 3);
        await escenario.InsertarCustomAsync(domingo, "domingo abierto", (new TimeOnly(10, 0), new TimeOnly(11, 0)));

        var resultado = await escenario.AgendarAsync(Comando(domingo, new TimeOnly(10, 0)));

        Assert.Equal(CodigosDeAgenda.FueraDeVentana, resultado.Codigo);
        Assert.Empty(await escenario.CitasAsync());
    }

    [Fact]
    public async Task El_ultimo_dia_de_la_ventana_se_reserva()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // Hoy + 2 meses: sábado 2 de enero de 2027, abierto por una custom.
        var sabado = new DateOnly(2027, 1, 2);
        await escenario.InsertarCustomAsync(sabado, "sábado abierto", (new TimeOnly(10, 0), new TimeOnly(11, 0)));

        var resultado = await escenario.AgendarAsync(Comando(sabado, new TimeOnly(10, 30)));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Single(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Dos_reservas_simultaneas_del_mismo_horario_dan_una_Cita_y_la_otra_horario_ocupado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var otra = Comando();
        otra.Nombre = "Mariana López";

        var resultados = await Task.WhenAll(escenario.AgendarAsync(Comando()), escenario.AgendarAsync(otra));

        Assert.Single(resultados, r => r.EsExito);
        var fallo = Assert.Single(resultados, r => !r.EsExito);
        Assert.Equal(FormaDeResultado.Fallo, fallo.Forma);
        Assert.Equal(CodigosDeAgenda.HorarioOcupado, fallo.Codigo);
        Assert.Single(await escenario.CitasAsync());
    }

    [Fact]
    public async Task La_reserva_espera_el_candado_de_su_fecha_y_lee_los_tramos_despues_de_tomarlo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // Otra transacción toma el candado de mañana y, sin confirmar, cierra el día con una custom sin tramos.
        await using var conexion = new NpgsqlConnection(escenario.Cadena);
        await conexion.OpenAsync(Cancelacion);
        await using var otra = await conexion.BeginTransactionAsync(Cancelacion);
        await using (var candado = new NpgsqlCommand(
            $"select pg_advisory_xact_lock({ClaseDeCandadoDeFecha}, {Manana.DayNumber})", conexion, otra))
        {
            await candado.ExecuteNonQueryAsync(Cancelacion);
        }

        await using (var custom = new NpgsqlCommand(
            $"insert into agenda.agenda_custom (fecha, motivo, creada_en, actualizada_en) values ('{Manana:yyyy-MM-dd}', 'cirugía', now(), now())",
            conexion,
            otra))
        {
            await custom.ExecuteNonQueryAsync(Cancelacion);
        }

        var reserva = escenario.AgendarAsync(Comando());

        // Mientras la otra no termina, la reserva hace fila.
        await Task.Delay(TimeSpan.FromMilliseconds(500), Cancelacion);
        Assert.False(reserva.IsCompleted);

        await otra.CommitAsync(Cancelacion);
        var resultado = await reserva;

        // Leyó los tramos después del candado: ve el día cerrado, no la base de antes.
        Assert.Equal(CodigosDeAgenda.FueraDeHorario, resultado.Codigo);
        Assert.Empty(await escenario.CitasAsync());
    }

    [Theory]
    [InlineData("12345", AgendarCitaEnLinea.MensajeDeTelefono)]
    [InlineData("44412345678", AgendarCitaEnLinea.MensajeDeTelefono)]
    [InlineData("abc4441234567", AgendarCitaEnLinea.MensajeDeTelefono)]
    [InlineData("0441234567", AgendarCitaEnLinea.MensajeDeTelefonoDeMexico)]
    [InlineData("1234567890", AgendarCitaEnLinea.MensajeDeTelefonoDeMexico)]
    public async Task Un_telefono_imposible_vuelve_invalido_y_no_crea_Paciente_ni_Cita(string telefono, string mensaje)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando();
        comando.Telefono = telefono;

        var resultado = await escenario.AgendarAsync(comando);

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.Equal([mensaje], resultado.Errores[nameof(AgendarCitaEnLinea.Telefono)]);
        Assert.Empty(await escenario.PacientesAsync());
        Assert.Empty(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Sin_Consentimiento_no_se_reserva()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando();
        comando.Consentimiento = false;

        var resultado = await escenario.AgendarAsync(comando);

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.Contains(nameof(AgendarCitaEnLinea.Consentimiento), resultado.Errores.Keys);
        Assert.Empty(await escenario.PacientesAsync());
        Assert.Empty(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Sin_nombre_ni_motivo_vuelve_invalido_con_mensajes_en_espanol()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando();
        comando.Nombre = "";
        comando.Motivo = " ";
        comando.Correo = "ana@";

        var resultado = await escenario.AgendarAsync(comando);

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.Equal([AgendarCitaEnLinea.MensajeDeNombre], resultado.Errores[nameof(AgendarCitaEnLinea.Nombre)]);
        Assert.Equal([AgendarCitaEnLinea.MensajeDeCorreo], resultado.Errores[nameof(AgendarCitaEnLinea.Correo)]);
        Assert.Contains(nameof(AgendarCitaEnLinea.Motivo), resultado.Errores.Keys);
        Assert.Empty(await escenario.PacientesAsync());
    }

    [Fact]
    public async Task Un_horario_que_ya_paso_vuelve_como_fallo_y_no_crea_Cita()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // Hoy a las 11:00 en Ciudad de México; el reloj marca las 12:00.
        var resultado = await escenario.AgendarAsync(Comando(fecha: new DateOnly(2026, 11, 2), hora: new TimeOnly(11, 0)));

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.EnElPasado, resultado.Codigo);
        Assert.Empty(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Un_tipo_inexistente_vuelve_como_fallo_y_no_crea_Paciente_ni_Cita()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando();
        comando.TipoDeVisitaId = TipoDeVisitaId.Nuevo();

        var resultado = await escenario.AgendarAsync(comando);

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.TipoNoReservable, resultado.Codigo);
        Assert.Empty(await escenario.PacientesAsync());
        Assert.Empty(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Un_tipo_que_no_se_reserva_en_linea_vuelve_como_fallo_y_no_crea_Cita()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.EjecutarAsync(
            $"update agenda.tipo_de_visita set reservable_en_linea = false where id = '{TiposDeVisitaSembrados.PrimeraConsulta}'");

        var resultado = await escenario.AgendarAsync(Comando());

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.TipoNoReservable, resultado.Codigo);
        Assert.Empty(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Los_tipos_de_visita_sembrados_son_dos_de_30_minutos_reservables_en_linea()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var tipos = await escenario.FilasAsync(
            "select id, nombre, duracion_min, reservable_en_linea, activo from agenda.tipo_de_visita order by orden",
            l => (Id: l.GetGuid(0), Nombre: l.GetString(1), Duracion: l.GetInt32(2), Reservable: l.GetBoolean(3), Activo: l.GetBoolean(4)));

        Assert.Equal(
            [
                (TiposDeVisitaSembrados.PrimeraConsulta.Valor, "Primera consulta", 30, true, true),
                (TiposDeVisitaSembrados.ConsultaDeSeguimiento.Valor, "Consulta de seguimiento", 30, true, true),
            ],
            tipos);
    }

    [Fact]
    public async Task Los_registros_no_llevan_nombre_telefono_correo_ni_motivo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var choca = Comando();
        choca.Nombre = "Mariana López";

        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);
        Assert.False((await escenario.AgendarAsync(choca)).EsExito);

        var registros = Arnes.Registros(escenario.Web);
        foreach (var dato in new[] { "Ana Sofía López", "Mariana López", "4441234567", "ana@correo.com", "Dolor de rodilla" })
        {
            Assert.DoesNotContain(registros, r => r.Mensaje.Contains(dato, StringComparison.OrdinalIgnoreCase));
        }
    }

    // RN-18 y AD-13: tope semanal por teléfono, con el sembrado de 3. El reloj está en el lunes 2 de noviembre; la
    // semana de mañana va del lunes 2 al domingo 8.

    [Fact]
    public async Task Bajo_el_tope_semanal_la_tercera_Cita_de_la_semana_se_guarda()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(Comando(hora: new TimeOnly(16, 0)))).EsExito);
        Assert.True((await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 5), new TimeOnly(16, 0)))).EsExito);

        var resultado = await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 6), new TimeOnly(16, 0)));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(3, (await escenario.CitasAsync()).Count);
    }

    [Fact]
    public async Task Con_tres_Citas_en_la_semana_la_cuarta_vuelve_como_tope_semanal_y_no_se_guarda()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.AgendarTresAsync();

        var resultado = await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 6), new TimeOnly(17, 0)));

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.TopeSemanal, resultado.Codigo);
        Assert.Equal(3, (await escenario.CitasAsync()).Count);
    }

    [Fact]
    public async Task El_tope_es_del_telefono_aunque_las_Citas_sean_de_Pacientes_con_otro_nombre()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var nombres = new[] { "Mariana López", "Jorge López", "Luisa López" };
        for (var i = 0; i < nombres.Length; i++)
        {
            var otra = Comando(hora: new TimeOnly(16, 0).AddMinutes(30 * i));
            otra.Nombre = nombres[i];
            otra.Telefono = "+52 444 123 4567";
            Assert.True((await escenario.AgendarAsync(otra)).EsExito);
        }

        var resultado = await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 4), new TimeOnly(16, 0)));

        Assert.Equal(CodigosDeAgenda.TopeSemanal, resultado.Codigo);
        Assert.Equal(3, (await escenario.CitasAsync()).Count);
        Assert.Equal(4, (await escenario.PacientesAsync()).Count);
    }

    [Fact]
    public async Task Otro_telefono_no_suma_al_tope()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.AgendarTresAsync();
        var otra = Comando(new DateOnly(2026, 11, 6), new TimeOnly(17, 0));
        otra.Telefono = "444 765 4321";

        var resultado = await escenario.AgendarAsync(otra);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
    }

    [Fact]
    public async Task Con_tres_Citas_la_semana_pasada_la_de_la_semana_siguiente_se_guarda()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.AgendarTresAsync();

        // Lunes 9 de noviembre: la semana siguiente.
        var resultado = await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 9), new TimeOnly(16, 0)));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(4, (await escenario.CitasAsync()).Count);
    }

    [Fact]
    public async Task Las_Citas_del_domingo_anterior_y_del_lunes_siguiente_no_cuentan_para_la_semana()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var paciente = await escenario.PacienteDeAnaAsync();

        // Domingo 8 por la noche (ya lunes 9 en UTC) y lunes 16 desde las 0:00: fuera de la semana del 9 al 15.
        foreach (var (fecha, hora) in new[]
                 {
                     (new DateOnly(2026, 11, 8), new TimeOnly(19, 0)),
                     (new DateOnly(2026, 11, 8), new TimeOnly(20, 0)),
                     (new DateOnly(2026, 11, 8), new TimeOnly(23, 0)),
                     (new DateOnly(2026, 11, 16), new TimeOnly(0, 0)),
                     (new DateOnly(2026, 11, 16), new TimeOnly(9, 0)),
                     (new DateOnly(2026, 11, 16), new TimeOnly(10, 0)),
                 })
        {
            await escenario.InsertarCitaDelConsultorioAsync(fecha, hora, hora.AddMinutes(30), paciente);
        }

        var resultado = await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 11), new TimeOnly(16, 0)));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
    }

    [Fact]
    public async Task Una_Cita_Cancelada_no_cuenta_para_el_tope()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.AgendarTresAsync();
        await escenario.EjecutarAsync(
            $"""
            update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'paciente', cancelada_en = now()
            where inicio = '{HoraLocal.AUtc(Manana, new TimeOnly(16, 0)):O}'
            """);

        var resultado = await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 6), new TimeOnly(17, 0)));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
    }

    [Fact]
    public async Task Una_Cita_del_consultorio_cuenta_para_el_tope()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(Comando(hora: new TimeOnly(16, 0)))).EsExito);
        Assert.True((await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 5), new TimeOnly(16, 0)))).EsExito);
        var paciente = Assert.Single(await escenario.PacientesAsync()).Id;

        // Sábado 7, fuera de los tramos: del consultorio, escrita directo en la base.
        await escenario.InsertarCitaDelConsultorioAsync(new DateOnly(2026, 11, 7), new TimeOnly(10, 0), new TimeOnly(10, 30), paciente);

        var resultado = await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 6), new TimeOnly(16, 0)));

        Assert.Equal(CodigosDeAgenda.TopeSemanal, resultado.Codigo);
        Assert.Equal(3, (await escenario.CitasAsync()).Count);
    }

    [Fact]
    public async Task El_tope_se_lee_de_las_reglas_de_reserva()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.EjecutarAsync("update agenda.reglas_de_reserva set tope_semanal_por_telefono = 1");
        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);

        var resultado = await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 5), new TimeOnly(16, 0)));

        Assert.Equal(CodigosDeAgenda.TopeSemanal, resultado.Codigo);
        Assert.Single(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Pacientes_da_los_ids_de_todos_los_que_comparten_el_telefono_y_nada_con_uno_invalido()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var api = ambito.ServiceProvider.GetRequiredService<IPacientesApi>();
        var ana = (await api.BuscarOCrearAsync("Ana Sofía López", "4441234567", null, Cancelacion)).Valor;
        var mariana = (await api.BuscarOCrearAsync("Mariana López", "+52 444-123-4567", null, Cancelacion)).Valor;
        Assert.True((await api.BuscarOCrearAsync("Luis Pérez", "4449876543", null, Cancelacion)).EsExito);

        var ids = await api.ConTelefonoAsync("444 123 4567", Cancelacion);

        Assert.Equal(new[] { ana.Valor, mariana.Valor }.Order(), ids.Select(id => id.Valor).Order());
        Assert.Empty(await api.ConTelefonoAsync("12345", Cancelacion));
        Assert.Empty(await api.ConTelefonoAsync("4447777777", Cancelacion));
    }

    // AD-27 y RN-21: confirmar dos veces la misma solicitud no crea dos Citas.

    [Fact]
    public async Task La_Cita_guarda_su_solicitud()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando();

        Assert.True((await escenario.AgendarAsync(comando)).EsExito);

        Assert.Equal(comando.SolicitudId, await escenario.EscalarAsync<Guid>("select solicitud_id from agenda.cita"));
    }

    [Fact]
    public async Task Confirmar_dos_veces_la_misma_solicitud_da_una_Cita_y_la_misma_confirmacion()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var solicitud = Guid.CreateVersion7();

        var primera = await escenario.AgendarAsync(Comando(solicitud: solicitud));
        var segunda = await escenario.AgendarAsync(Comando(solicitud: solicitud));

        Assert.True(primera.EsExito, $"{primera.Forma} {primera.Codigo}");
        Assert.True(segunda.EsExito, $"{segunda.Forma} {segunda.Codigo}");
        Assert.Equal(primera.Valor, segunda.Valor);
        Assert.Equal(primera.Valor.CitaId.Valor, Assert.Single(await escenario.CitasAsync()).Id);
    }

    [Fact]
    public async Task Una_solicitud_que_ya_tiene_Cita_devuelve_esa_aunque_el_segundo_envio_traiga_otro_horario()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var solicitud = Guid.CreateVersion7();
        var primera = await escenario.AgendarAsync(Comando(solicitud: solicitud));

        var segunda = await escenario.AgendarAsync(Comando(hora: new TimeOnly(17, 0), solicitud: solicitud));

        Assert.True(segunda.EsExito, $"{segunda.Forma} {segunda.Codigo}");
        Assert.Equal(primera.Valor, segunda.Valor);
        Assert.Equal(new TimeOnly(16, 0), segunda.Valor.Hora);
        Assert.Single(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Envios_simultaneos_de_la_misma_solicitud_dan_una_Cita_y_todos_confirman()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var solicitud = Guid.CreateVersion7();

        var resultados = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => escenario.AgendarAsync(Comando(solicitud: solicitud))));

        Assert.All(resultados, r => Assert.True(r.EsExito, $"{r.Forma} {r.Codigo}"));
        Assert.Single(resultados.Select(r => r.Valor).Distinct());
        Assert.Single(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Envios_simultaneos_de_la_misma_solicitud_con_fechas_distintas_dan_una_Cita_y_los_dos_confirman()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var solicitud = Guid.CreateVersion7();

        // Fechas distintas: cada envío toma su propio candado y ninguno espera al otro; los separa el único.
        var resultados = await Task.WhenAll(
            escenario.AgendarAsync(Comando(solicitud: solicitud)),
            escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 4), solicitud: solicitud)));

        Assert.All(resultados, r => Assert.True(r.EsExito, $"{r.Forma} {r.Codigo}"));
        Assert.Equal(resultados[0].Valor, resultados[1].Valor);
        Assert.Equal(resultados[0].Valor.CitaId.Valor, Assert.Single(await escenario.CitasAsync()).Id);
    }

    [Fact]
    public async Task Un_envio_repetido_que_espero_el_candado_devuelve_la_Cita_que_se_guardo_mientras()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var solicitud = Guid.CreateVersion7();

        // Otra transacción tiene el candado de mañana: el envío pasa la primera búsqueda y hace fila.
        await using var conexion = new NpgsqlConnection(escenario.Cadena);
        await conexion.OpenAsync(Cancelacion);
        await using var otra = await conexion.BeginTransactionAsync(Cancelacion);
        await using (var candado = new NpgsqlCommand(
            $"select pg_advisory_xact_lock({ClaseDeCandadoDeFecha}, {Manana.DayNumber})", conexion, otra))
        {
            await candado.ExecuteNonQueryAsync(Cancelacion);
        }

        var repetido = escenario.AgendarAsync(Comando(solicitud: solicitud));
        await Task.Delay(TimeSpan.FromMilliseconds(500), Cancelacion);
        Assert.False(repetido.IsCompleted);

        // Mientras, el primer envío de la misma solicitud, de otra fecha, guarda su Cita.
        var primero = await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 5), solicitud: solicitud));
        Assert.True(primero.EsExito, $"{primero.Forma} {primero.Codigo}");

        await otra.CommitAsync(Cancelacion);
        var resultado = await repetido;

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(primero.Valor, resultado.Valor);
        Assert.Single(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Un_envio_repetido_no_cuenta_su_propia_Cita_para_el_tope()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 5), new TimeOnly(16, 0)))).EsExito);
        Assert.True((await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 6), new TimeOnly(16, 0)))).EsExito);
        var solicitud = Guid.CreateVersion7();

        // La tercera de la semana llega dos veces: la segunda vez el teléfono ya tiene tres, pero son la misma.
        var primera = await escenario.AgendarAsync(Comando(solicitud: solicitud));
        var segunda = await escenario.AgendarAsync(Comando(solicitud: solicitud));

        Assert.True(segunda.EsExito, $"{segunda.Forma} {segunda.Codigo}");
        Assert.Equal(primera.Valor, segunda.Valor);
        Assert.Equal(3, (await escenario.CitasAsync()).Count);
    }

    [Fact]
    public async Task Un_envio_repetido_de_otra_fecha_que_espero_el_candado_con_el_tope_lleno_devuelve_la_Cita_del_primero()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 5), new TimeOnly(16, 0)))).EsExito);
        Assert.True((await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 6), new TimeOnly(16, 0)))).EsExito);
        var solicitud = Guid.CreateVersion7();

        // El repetido, de mañana, pasa la primera búsqueda y hace fila en el candado de su fecha.
        await using var conexion = new NpgsqlConnection(escenario.Cadena);
        await conexion.OpenAsync(Cancelacion);
        await using var otra = await conexion.BeginTransactionAsync(Cancelacion);
        await using (var candado = new NpgsqlCommand(
            $"select pg_advisory_xact_lock({ClaseDeCandadoDeFecha}, {Manana.DayNumber})", conexion, otra))
        {
            await candado.ExecuteNonQueryAsync(Cancelacion);
        }

        var repetido = escenario.AgendarAsync(Comando(solicitud: solicitud));
        await Task.Delay(TimeSpan.FromMilliseconds(500), Cancelacion);
        Assert.False(repetido.IsCompleted);

        // El primero, del miércoles de la misma semana, guarda la tercera Cita; el repetido verá el tope lleno.
        var primero = await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 4), solicitud: solicitud));
        Assert.True(primero.EsExito, $"{primero.Forma} {primero.Codigo}");

        await otra.CommitAsync(Cancelacion);
        var resultado = await repetido;

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(primero.Valor, resultado.Valor);
        Assert.Equal(3, (await escenario.CitasAsync()).Count);
    }

    [Fact]
    public async Task Tras_un_horario_ocupado_la_misma_solicitud_reserva_otra_hora()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);
        var solicitud = Guid.CreateVersion7();
        var otra = Comando(solicitud: solicitud);
        otra.Nombre = "Mariana López";

        var ocupado = await escenario.AgendarAsync(otra);
        otra.Hora = new TimeOnly(16, 30);
        var reintento = await escenario.AgendarAsync(otra);

        Assert.Equal(CodigosDeAgenda.HorarioOcupado, ocupado.Codigo);
        Assert.True(reintento.EsExito, $"{reintento.Forma} {reintento.Codigo}");
        Assert.Equal(new TimeOnly(16, 30), reintento.Valor.Hora);
        Assert.Equal(
            solicitud,
            await escenario.EscalarAsync<Guid>($"select solicitud_id from agenda.cita where id = '{reintento.Valor.CitaId}'"));
        Assert.Equal(2, (await escenario.CitasAsync()).Count);
    }

    [Fact]
    public async Task Las_Citas_del_consultorio_sin_solicitud_no_chocan_por_el_unico_y_dos_iguales_si()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(9, 0), new TimeOnly(9, 30));
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(10, 0), new TimeOnly(10, 30));
        Assert.Equal(0, await escenario.EscalarAsync<long>("select count(solicitud_id) from agenda.cita"));

        var solicitud = Guid.CreateVersion7();
        await escenario.EjecutarAsync($"update agenda.cita set solicitud_id = '{solicitud}' where inicio = '{HoraLocal.AUtc(Manana, new TimeOnly(9, 0)):O}'");
        var error = await Assert.ThrowsAsync<PostgresException>(() => escenario.EjecutarAsync(
            $"update agenda.cita set solicitud_id = '{solicitud}' where inicio = '{HoraLocal.AUtc(Manana, new TimeOnly(10, 0)):O}'"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal(AgendaDbContext.IndiceDeSolicitud, error.ConstraintName);
    }

    /// <summary>
    /// Fija un usuario anónimo en el ámbito, como lo fija el renderizador de Blazor en una petición sin sesión: el
    /// actor de <see cref="IActorActual"/> es el Paciente (AD-22).
    /// </summary>
    internal static void SinSesion(AsyncServiceScope ambito)
    {
        var autenticacion = (IHostEnvironmentAuthenticationStateProvider)ambito.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        autenticacion.SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
    }

    /// <summary>
    /// Un comando válido: Primera consulta, mañana a las 16:00 (o lo que se pida), con Consentimiento y una solicitud
    /// nueva (o la que se pida).
    /// </summary>
    public static AgendarCitaEnLinea Comando(DateOnly? fecha = null, TimeOnly? hora = null, Guid? solicitud = null) => new()
    {
        SolicitudId = solicitud ?? Guid.CreateVersion7(),
        TipoDeVisitaId = TiposDeVisitaSembrados.PrimeraConsulta,
        Fecha = fecha ?? Manana,
        Hora = hora ?? new TimeOnly(16, 0),
        Nombre = "Ana Sofía López",
        Telefono = "444 123 4567",
        Correo = "ana@correo.com",
        Motivo = "Dolor de rodilla derecha",
        Consentimiento = true,
    };

    /// <summary>Una base migrada propia y la web en memoria sobre ella, con el reloj en <see cref="Ahora"/>.</summary>
    internal sealed class Escenario : IAsyncDisposable
    {
        private Escenario(string cadena, RelojDePrueba reloj, WebApplicationFactory<Program> web)
        {
            Cadena = cadena;
            Reloj = reloj;
            Web = web;
        }

        public string Cadena { get; }

        /// <summary>El reloj de la web: solo avanza cuando la prueba lo mueve.</summary>
        public RelojDePrueba Reloj { get; }

        public WebApplicationFactory<Program> Web { get; }

        /// <summary>Lo que tarda una persona en llenar un formulario: más que el tiempo mínimo de la verificación anti-bots.</summary>
        public static readonly TimeSpan LoQueTardaUnaPersona = TimeSpan.FromSeconds(20);

        public static async Task<Escenario> CrearAsync(Arnes arnes)
        {
            var cadena = await arnes.CrearBaseMigradaAsync(TestContext.Current.CancellationToken);
            var reloj = new RelojDePrueba(Ahora);
            return new Escenario(cadena, reloj, arnes.ArrancarWebSinEntregaAutomatica(cadena, reloj));
        }

        /// <summary>
        /// Un cliente con cookies, como <c>CreateClient</c>, que se toma su tiempo como una persona: antes de cada
        /// envío (POST) mueve el reloj <see cref="LoQueTardaUnaPersona"/>. Con <paramref name="web"/>, sobre otra
        /// fábrica de este escenario (con el mismo reloj).
        /// </summary>
        public HttpClient ClienteDeUnaPersona(WebApplicationFactory<Program>? web = null) =>
            (web ?? Web).CreateDefaultClient(new CookieContainerHandler(), new TardaComoUnaPersona(Reloj));

        private sealed class TardaComoUnaPersona(RelojDePrueba reloj) : DelegatingHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken cancelacion)
            {
                if (peticion.Method == HttpMethod.Post)
                {
                    reloj.Avanzar(LoQueTardaUnaPersona);
                }

                return base.SendAsync(peticion, cancelacion);
            }
        }

        /// <summary>
        /// El handler se pide al contenedor por su interfaz, en un ámbito propio, como lo hace la página. El usuario del
        /// ámbito es anónimo, como el de una petición sin sesión: el actor es el Paciente (AD-22).
        /// </summary>
        public async Task<Resultado<CitaAgendadaEnLinea>> AgendarAsync(AgendarCitaEnLinea comando)
        {
            await using var ambito = Web.Services.CreateAsyncScope();
            SinSesion(ambito);
            return await ambito.ServiceProvider
                .GetRequiredService<ICommandHandler<AgendarCitaEnLinea, CitaAgendadaEnLinea>>()
                .HandleAsync(comando, Cancelacion);
        }

        /// <summary>Tres Citas de Ana esta semana: martes 3, jueves 5 y viernes 6 a las 16:00.</summary>
        public async Task AgendarTresAsync()
        {
            foreach (var dia in new[] { 3, 5, 6 })
            {
                var resultado = await AgendarAsync(Comando(new DateOnly(2026, 11, dia), new TimeOnly(16, 0)));
                Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
            }
        }

        /// <summary>El id de Ana, registrándola por Pacientes si todavía no existe.</summary>
        public async Task<Guid> PacienteDeAnaAsync()
        {
            await using var ambito = Web.Services.CreateAsyncScope();
            var resultado = await ambito.ServiceProvider.GetRequiredService<IPacientesApi>()
                .BuscarOCrearAsync("Ana Sofía López", "4441234567", "ana@correo.com", Cancelacion);
            Assert.True(resultado.EsExito);
            return resultado.Valor.Valor;
        }

        public Task<List<CitaLeida>> CitasAsync() => FilasAsync(
            "select id, paciente_id, tipo_de_visita_id, inicio, fin, estado, origen, motivo_de_consulta, "
            + "consentimiento_en, consentimiento_version_aviso, consentimiento_canal from agenda.cita order by inicio, creada_en",
            l => new CitaLeida(
                l.GetGuid(0), l.GetGuid(1), l.GetGuid(2),
                l.GetFieldValue<DateTimeOffset>(3), l.GetFieldValue<DateTimeOffset>(4),
                l.GetString(5), l.GetString(6), l.IsDBNull(7) ? null : l.GetString(7),
                l.GetFieldValue<DateTimeOffset>(8), l.GetString(9), l.GetString(10)));

        public Task<List<PacienteLeido>> PacientesAsync() => FilasAsync(
            "select id, nombre_completo, telefono, correo from pacientes.paciente order by creado_en",
            l => new PacienteLeido(l.GetGuid(0), l.GetString(1), l.GetString(2), l.IsDBNull(3) ? null : l.GetString(3)));

        public async Task<List<T>> FilasAsync<T>(string sql, Func<NpgsqlDataReader, T> leer)
        {
            await using var conexion = new NpgsqlConnection(Cadena);
            await conexion.OpenAsync(Cancelacion);
            await using var comando = new NpgsqlCommand(sql, conexion);
            await using var lector = await comando.ExecuteReaderAsync(Cancelacion);
            var filas = new List<T>();
            while (await lector.ReadAsync(Cancelacion))
            {
                filas.Add(leer(lector));
            }

            return filas;
        }

        public async Task<T> EscalarAsync<T>(string sql) => Assert.Single(await FilasAsync(sql, l => l.GetFieldValue<T>(0)));

        public async Task EjecutarAsync(string sql)
        {
            await using var conexion = new NpgsqlConnection(Cadena);
            await conexion.OpenAsync(Cancelacion);
            await using var comando = new NpgsqlCommand(sql, conexion);
            await comando.ExecuteNonQueryAsync(Cancelacion);
        }

        /// <summary>
        /// Una Cita del consultorio escrita directo en la base, de <paramref name="inicio"/> a <paramref name="fin"/>
        /// en hora local: puede quedar fuera de la rejilla, que no la valida (AD-7, RN-13).
        /// </summary>
        /// <param name="pacienteId">El Paciente de la Cita; si no se da, uno al azar que no existe.</param>
        public Task InsertarCitaDelConsultorioAsync(DateOnly fecha, TimeOnly inicio, TimeOnly fin, Guid? pacienteId = null) =>
            EjecutarAsync(
                $"""
                insert into agenda.cita (id, paciente_id, tipo_de_visita_id, inicio, fin, estado, origen, nota,
                    consentimiento_en, consentimiento_version_aviso, consentimiento_canal, creada_en, token_hash)
                values (gen_random_uuid(), {(pacienteId is { } id ? $"'{id}'" : "gen_random_uuid()")}, '{TiposDeVisitaSembrados.PrimeraConsulta}',
                    '{HoraLocal.AUtc(fecha, inicio):O}', '{HoraLocal.AUtc(fecha, fin):O}', 'Agendada', 'consultorio',
                    'Pidió por WhatsApp', now(), '2026-11-borrador', 'externo', now(), sha256(uuid_send(gen_random_uuid())))
                """);

        /// <summary>Una agenda custom escrita directo en la base; sin tramos, ese día no se atiende.</summary>
        public async Task InsertarCustomAsync(DateOnly fecha, string motivo, params (TimeOnly Inicio, TimeOnly Fin)[] tramos)
        {
            await EjecutarAsync(
                $"insert into agenda.agenda_custom (fecha, motivo, creada_en, actualizada_en) values ('{fecha:yyyy-MM-dd}', '{motivo}', now(), now())");
            foreach (var (inicio, fin) in tramos)
            {
                await EjecutarAsync(
                    $"insert into agenda.agenda_custom_tramo (id, fecha, inicio, fin) values (gen_random_uuid(), '{fecha:yyyy-MM-dd}', '{inicio:HH:mm}', '{fin:HH:mm}')");
            }
        }

        public ValueTask DisposeAsync() => Web.DisposeAsync();
    }

    internal sealed record CitaLeida(
        Guid Id,
        Guid PacienteId,
        Guid TipoDeVisitaId,
        DateTimeOffset Inicio,
        DateTimeOffset Fin,
        string Estado,
        string Origen,
        string? MotivoDeConsulta,
        DateTimeOffset ConsentimientoEn,
        string ConsentimientoVersion,
        string ConsentimientoCanal);

    internal sealed record PacienteLeido(Guid Id, string Nombre, string Telefono, string? Correo);
}
