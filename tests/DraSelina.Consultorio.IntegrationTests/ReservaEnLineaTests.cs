using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
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

    /// <summary>Mañana, en hora local.</summary>
    public static readonly DateOnly Manana = new(2026, 11, 3);

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
        var otra = Comando(hora: new TimeOnly(16, 15));
        otra.Nombre = "Mariana López";

        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);
        var resultado = await escenario.AgendarAsync(otra);

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.HorarioOcupado, resultado.Codigo);
        Assert.Single(await escenario.CitasAsync());

        // El Paciente sí queda: confirma antes que la Cita (AD-28, RN-28).
        Assert.Equal(2, (await escenario.PacientesAsync()).Count);
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

    /// <summary>Un comando válido: Primera consulta, mañana a las 16:00 (o lo que se pida), con Consentimiento.</summary>
    public static AgendarCitaEnLinea Comando(DateOnly? fecha = null, TimeOnly? hora = null) => new()
    {
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
        private Escenario(string cadena, WebApplicationFactory<Program> web)
        {
            Cadena = cadena;
            Web = web;
        }

        public string Cadena { get; }

        public WebApplicationFactory<Program> Web { get; }

        public static async Task<Escenario> CrearAsync(Arnes arnes)
        {
            var cadena = await arnes.CrearBaseMigradaAsync(TestContext.Current.CancellationToken);
            return new Escenario(cadena, arnes.ArrancarWebSinEntregaAutomatica(cadena, new RelojDePrueba(Ahora)));
        }

        /// <summary>El handler se pide al contenedor por su interfaz, en un ámbito propio, como lo hace la página.</summary>
        public async Task<Resultado<CitaAgendadaEnLinea>> AgendarAsync(AgendarCitaEnLinea comando)
        {
            await using var ambito = Web.Services.CreateAsyncScope();
            return await ambito.ServiceProvider
                .GetRequiredService<ICommandHandler<AgendarCitaEnLinea, CitaAgendadaEnLinea>>()
                .HandleAsync(comando, Cancelacion);
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
