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
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// <see cref="DiasDeLaAgenda"/>, la consulta de la Agenda del Administrador (CAP-12, AD-22, entradas 5.1 y 5.2), contra
/// PostgreSQL real con la agenda base sembrada (lunes a viernes, 3:30 a 6:00 pm): los días del rango con su tipo, su
/// motivo y sus Tramos vigentes; sus Citas en orden de inicio, con su estado mostrado (Atendida derivada, RN-31), el
/// nombre en lote desde Pacientes, el motivo, "sin correo" y "fuera de horario" con su nota. Sin Administrador, no
/// autorizado; un rango al revés o de más de siete días, inválido. Cada prueba usa su base propia y el reloj de la prueba.
/// </summary>
public sealed class DiasDeLaAgendaTests(Arnes arnes)
{
    /// <summary>Lunes 9 de noviembre de 2026.</summary>
    private static readonly DateOnly Hoy = new(2026, 11, 9);

    private static readonly DateOnly Martes = Hoy.AddDays(1);

    private static readonly DateOnly Miercoles = Hoy.AddDays(2);

    private static readonly DateOnly Sabado = Hoy.AddDays(5);

    private static readonly DateOnly Domingo = Hoy.AddDays(6);

    /// <summary>Hoy a las 4:10 pm en Ciudad de México.</summary>
    private static readonly DateTimeOffset Ahora = HoraLocal.AUtc(Hoy, new TimeOnly(16, 10));

    private static readonly TramoDeLaAgenda TramoSembrado = new(new TimeOnly(15, 30), new TimeOnly(18, 0));

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sin_Administrador_la_consulta_vuelve_no_autorizado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity()));

        var resultado = await Consulta(ambito).HandleAsync(DiasDeLaAgenda.DelDia(Hoy), Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 7)]
    [InlineData(0, 30)]
    public async Task Un_rango_al_reves_o_de_mas_de_siete_dias_es_invalido(int desde, int hasta)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await escenario.ConsultarAsync(new DiasDeLaAgenda(Hoy.AddDays(desde), Hoy.AddDays(hasta)));

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.True(resultado.Errores.ContainsKey(nameof(DiasDeLaAgenda.Hasta)));
    }

    [Fact]
    public async Task La_semana_trae_los_siete_dias_con_su_tipo_sus_Tramos_y_sus_Citas()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteAsync("Ana Sofía López", "4441234567", "ana@correo.com");

        await escenario.InsertarAsync(ana, Martes, new TimeOnly(16, 0), new TimeOnly(16, 30), "Agendada", null, "Dolor");
        await escenario.InsertarAsync(ana, Hoy.AddDays(3), new TimeOnly(17, 0), new TimeOnly(17, 30), "Agendada", null, "Revisión");
        // El lunes siguiente: fuera del rango.
        await escenario.InsertarAsync(ana, Hoy.AddDays(7), new TimeOnly(16, 0), new TimeOnly(16, 30), "Agendada", null, "Fuera");

        var dias = await escenario.ConsultarComoAdministradorAsync(new DiasDeLaAgenda(Hoy, Domingo));

        Assert.Equal(Enumerable.Range(0, 7).Select(Hoy.AddDays), dias.Select(d => d.Fecha));
        Assert.Equal(
            [TipoDeDia.Normal, TipoDeDia.Normal, TipoDeDia.Normal, TipoDeDia.Normal, TipoDeDia.Normal, TipoDeDia.SinAtencion, TipoDeDia.SinAtencion],
            dias.Select(d => d.Tipo));
        Assert.All(dias.Take(5), d => Assert.Equal([TramoSembrado], d.Tramos));
        Assert.All(dias.Skip(5), d => Assert.Empty(d.Tramos));
        Assert.All(dias, d => Assert.Null(d.MotivoDeCustom));
        Assert.Equal([0, 1, 0, 1, 0, 0, 0], dias.Select(d => d.Citas.Count));
        Assert.Equal(new TimeOnly(17, 0), Assert.Single(dias[3].Citas).Inicio);
    }

    [Fact]
    public async Task Las_Citas_del_dia_van_en_orden_de_inicio_con_su_estado_nombre_y_motivo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteAsync("Ana Sofía López", "4441234567");
        var bruno = await escenario.PacienteAsync("Bruno Díaz", "4449876543");

        await escenario.InsertarAsync(ana, Hoy, new TimeOnly(16, 0), new TimeOnly(16, 30), "Agendada", null, "Dolor de rodilla");
        await escenario.InsertarAsync(bruno, Hoy, new TimeOnly(17, 0), new TimeOnly(17, 30), "Agendada", null, "Revisión de hombro");
        await escenario.InsertarAsync(ana, Hoy, new TimeOnly(17, 30), new TimeOnly(18, 0), "Cancelada", "tarde", "Esguince");
        await escenario.InsertarAsync(bruno, Hoy, new TimeOnly(15, 30), new TimeOnly(16, 0), "NoSePresento", null, null);
        // Otro día: no aparece.
        await escenario.InsertarAsync(ana, Martes, new TimeOnly(16, 0), new TimeOnly(16, 30), "Agendada", null, "Mañana");

        var citas = Assert.Single(await escenario.ConsultarComoAdministradorAsync(DiasDeLaAgenda.DelDia(Hoy))).Citas;

        Assert.Equal(
            [
                (new TimeOnly(15, 30), new TimeOnly(16, 0), "Bruno Díaz", (string?)null, EstadoEnLaAgenda.NoSePresento, (CancelacionEnLaAgenda?)null),
                (new TimeOnly(16, 0), new TimeOnly(16, 30), "Ana Sofía López", "Dolor de rodilla", EstadoEnLaAgenda.Atendida, null),
                (new TimeOnly(17, 0), new TimeOnly(17, 30), "Bruno Díaz", "Revisión de hombro", EstadoEnLaAgenda.Agendada, null),
                (new TimeOnly(17, 30), new TimeOnly(18, 0), "Ana Sofía López", "Esguince", EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.Tardia),
            ],
            citas.Select(c => (c.Inicio, c.Fin, c.Paciente!, c.MotivoDeConsulta, c.Estado, c.Cancelacion)));
    }

    [Fact]
    public async Task En_el_inicio_mismo_la_Cita_ya_cuenta_como_Atendida()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteAsync("Ana Sofía López", "4441234567");
        await escenario.InsertarAsync(ana, Hoy, new TimeOnly(16, 10), new TimeOnly(16, 40), "Agendada", null, "Dolor");
        await escenario.InsertarAsync(ana, Hoy, new TimeOnly(16, 40), new TimeOnly(17, 10), "Agendada", null, "Dolor");

        var citas = Assert.Single(await escenario.ConsultarComoAdministradorAsync(DiasDeLaAgenda.DelDia(Hoy))).Citas;

        Assert.Equal([EstadoEnLaAgenda.Atendida, EstadoEnLaAgenda.Agendada], citas.Select(c => c.Estado));
    }

    [Fact]
    public async Task Sin_correo_se_marca_en_cualquier_estado_y_con_correo_no()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteAsync("Ana Sofía López", "4441234567", "ana@correo.com");
        var bruno = await escenario.PacienteAsync("Bruno Díaz", "4449876543");
        await escenario.InsertarAsync(ana, Martes, new TimeOnly(16, 0), new TimeOnly(16, 30), "Agendada", null, "Dolor");
        await escenario.InsertarAsync(bruno, Martes, new TimeOnly(16, 30), new TimeOnly(17, 0), "Agendada", null, "Revisión");
        await escenario.InsertarAsync(bruno, Martes, new TimeOnly(17, 0), new TimeOnly(17, 30), "Cancelada", "paciente", "Revisión");

        var citas = Assert.Single(await escenario.ConsultarComoAdministradorAsync(DiasDeLaAgenda.DelDia(Martes))).Citas;

        Assert.Equal([false, true, true], citas.Select(c => c.SinCorreo));
    }

    [Fact]
    public async Task Una_Cita_del_consultorio_fuera_de_los_Tramos_lleva_la_marca_y_su_nota_y_una_dentro_o_en_linea_no()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteAsync("Ana Sofía López", "4441234567", "ana@correo.com");

        // Sábado sin Tramos, 10:15.
        await escenario.InsertarAsync(
            ana, Sabado, new TimeOnly(10, 15), new TimeOnly(10, 45), "Agendada", null, null, nota: "lo pidió Selina");
        // Martes dentro del Tramo, y uno que se sale por el final.
        await escenario.InsertarAsync(ana, Martes, new TimeOnly(16, 0), new TimeOnly(16, 30), "Agendada", null, "Dolor");
        await escenario.InsertarAsync(ana, Martes, new TimeOnly(17, 45), new TimeOnly(18, 15), "Agendada", null, "Dolor");
        // En línea fuera de los Tramos (no debería pasar, INV-C6): nunca lleva la marca.
        await escenario.InsertarAsync(ana, Martes, new TimeOnly(18, 30), new TimeOnly(19, 0), "Agendada", null, "Dolor", enLinea: true);

        var dias = await escenario.ConsultarComoAdministradorAsync(new DiasDeLaAgenda(Martes, Sabado));

        var sabado = Assert.Single(dias[^1].Citas);
        Assert.Equal(TipoDeDia.SinAtencion, dias[^1].Tipo);
        Assert.True(sabado.FueraDeHorario);
        Assert.Equal("lo pidió Selina", sabado.Nota);
        Assert.Equal([false, true, false], dias[0].Citas.Select(c => c.FueraDeHorario));
    }

    [Fact]
    public async Task Cada_Agenda_custom_da_su_tipo_su_motivo_y_sus_Tramos()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteAsync("Ana Sofía López", "4441234567", "ana@correo.com");
        await escenario.CustomAsync(Martes, "cirugía", (new TimeOnly(15, 30), new TimeOnly(17, 0)));
        await escenario.CustomAsync(Miercoles, "vacaciones");
        await escenario.CustomAsync(Sabado, "sábado abierto", (new TimeOnly(10, 0), new TimeOnly(13, 0)));
        // Una ampliación también es horario especial.
        await escenario.CustomAsync(Hoy.AddDays(3), "tarde larga", (new TimeOnly(15, 30), new TimeOnly(20, 0)));
        // Fuera del rango: no se lee.
        await escenario.CustomAsync(Hoy.AddDays(-1), "otra semana");

        await escenario.InsertarAsync(ana, Martes, new TimeOnly(16, 0), new TimeOnly(16, 30), "Agendada", null, "Dolor");
        await escenario.InsertarAsync(ana, Martes, new TimeOnly(17, 0), new TimeOnly(17, 30), "Agendada", null, "Dolor");
        await escenario.InsertarAsync(ana, Sabado, new TimeOnly(10, 0), new TimeOnly(10, 30), "Agendada", null, "Dolor");

        var dias = await escenario.ConsultarComoAdministradorAsync(new DiasDeLaAgenda(Hoy, Domingo));

        Assert.Equal(
            [
                (TipoDeDia.Normal, (string?)null),
                (TipoDeDia.HorarioEspecial, "cirugía"),
                (TipoDeDia.CustomSinAtencion, "vacaciones"),
                (TipoDeDia.HorarioEspecial, "tarde larga"),
                (TipoDeDia.Normal, null),
                (TipoDeDia.DiaAbierto, "sábado abierto"),
                (TipoDeDia.SinAtencion, null),
            ],
            dias.Select(d => (d.Tipo, d.MotivoDeCustom)));
        Assert.Equal([new TramoDeLaAgenda(new TimeOnly(15, 30), new TimeOnly(17, 0))], dias[1].Tramos);
        Assert.Empty(dias[2].Tramos);
        Assert.Equal([new TramoDeLaAgenda(new TimeOnly(10, 0), new TimeOnly(13, 0))], dias[5].Tramos);

        // El recorte deja fuera la de las 5:00; el sábado abierto deja dentro la de las 10:00.
        Assert.Equal([false, true], dias[1].Citas.Select(c => c.FueraDeHorario));
        Assert.False(Assert.Single(dias[5].Citas).FueraDeHorario);
    }

    [Fact]
    public async Task Una_Cita_cuyo_Paciente_no_existe_se_lista_sin_nombre_y_sin_correo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarAsync(PatientId.Nuevo(), Hoy, new TimeOnly(17, 0), new TimeOnly(17, 30), "Agendada", null, "Dolor");

        var cita = Assert.Single(Assert.Single(await escenario.ConsultarComoAdministradorAsync(DiasDeLaAgenda.DelDia(Hoy))).Citas);

        Assert.Null(cita.Paciente);
        Assert.True(cita.SinCorreo);
        Assert.Equal("Dolor", cita.MotivoDeConsulta);
    }

    [Fact]
    public async Task Los_contactos_en_lote_dan_uno_por_Paciente_que_existe()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteAsync("Ana Sofía López", "4441234567", "ana@correo.com");
        var bruno = await escenario.PacienteAsync("Bruno Díaz", "4449876543");
        var nadie = PatientId.Nuevo();

        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var api = ambito.ServiceProvider.GetRequiredService<IPacientesApi>();
        var contactos = await api.ObtenerContactosAsync([ana, bruno, nadie, ana], Cancelacion);

        Assert.Equal(2, contactos.Count);
        Assert.Equal(new Contactos("Ana Sofía López", "ana@correo.com"), contactos[ana]);
        Assert.Equal(new Contactos("Bruno Díaz", null), contactos[bruno]);
        Assert.Empty(await api.ObtenerContactosAsync([], Cancelacion));
    }

    [Fact]
    public async Task El_resultado_no_muestra_nombre_motivo_nota_ni_motivo_de_la_custom_al_convertirse_en_texto()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteAsync("Ana Sofía López", "4441234567");
        await escenario.CustomAsync(Hoy, "cirugía", (new TimeOnly(15, 30), new TimeOnly(19, 0)));
        await escenario.InsertarAsync(
            ana, Hoy, new TimeOnly(18, 0), new TimeOnly(18, 30), "Agendada", null, "Dolor de rodilla", nota: "Pidió por WhatsApp");

        var dia = Assert.Single(await escenario.ConsultarComoAdministradorAsync(DiasDeLaAgenda.DelDia(Hoy)));

        foreach (var texto in new[] { dia.ToString(), Assert.Single(dia.Citas).ToString() })
        {
            Assert.DoesNotContain("Ana", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("rodilla", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("WhatsApp", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("cirugía", texto, StringComparison.Ordinal);
        }
    }

    private static IQueryHandler<DiasDeLaAgenda, IReadOnlyList<DiaDeLaAgenda>> Consulta(AsyncServiceScope ambito) =>
        ambito.ServiceProvider.GetRequiredService<IQueryHandler<DiasDeLaAgenda, IReadOnlyList<DiaDeLaAgenda>>>();

    private static void FijarUsuario(AsyncServiceScope ambito, ClaimsPrincipal usuario) =>
        ((IHostEnvironmentAuthenticationStateProvider)ambito.ServiceProvider.GetRequiredService<AuthenticationStateProvider>())
            .SetAuthenticationState(Task.FromResult(new AuthenticationState(usuario)));

    private sealed class Escenario(string cadena, WebApplicationFactory<Program> web) : IAsyncDisposable
    {
        public WebApplicationFactory<Program> Web { get; } = web;

        public static async Task<Escenario> CrearAsync(Arnes arnes)
        {
            var cadena = await arnes.CrearBaseMigradaAsync(Cancelacion);
            return new Escenario(cadena, arnes.ArrancarWebSinEntregaAutomatica(cadena, new RelojDePrueba(Ahora)));
        }

        public async Task<PatientId> PacienteAsync(string nombre, string telefono, string? correo = null)
        {
            await using var ambito = Web.Services.CreateAsyncScope();
            var resultado = await ambito.ServiceProvider.GetRequiredService<IPacientesApi>()
                .BuscarOCrearAsync(nombre, telefono, correo, Cancelacion);
            Assert.True(resultado.EsExito);
            return resultado.Valor;
        }

        /// <summary>
        /// Una Cita escrita directo en la base, en hora local: del consultorio (con <paramref name="nota"/>) o, con
        /// <paramref name="enLinea"/>, en línea y sin nota.
        /// </summary>
        public async Task InsertarAsync(
            PatientId paciente, DateOnly fecha, TimeOnly inicio, TimeOnly fin, string estado, string? motivoDeCancelacion, string? motivo,
            string? nota = null, bool enLinea = false)
        {
            await using var conexion = new NpgsqlConnection(cadena);
            await conexion.OpenAsync(Cancelacion);
            await using var comando = new NpgsqlCommand(
                """
                insert into agenda.cita (id, paciente_id, tipo_de_visita_id, inicio, fin, estado, motivo_de_cancelacion,
                    cancelada_en, origen, nota, motivo_de_consulta, consentimiento_en, consentimiento_version_aviso,
                    consentimiento_canal, creada_en, token_hash)
                values (gen_random_uuid(), @paciente, @tipo, @inicio, @fin, @estado, @motivoDeCancelacion,
                    case when @estado = 'Cancelada' then now() end, @origen, @nota, @motivo, now(),
                    '2026-11-borrador', @canal, now(), sha256(uuid_send(gen_random_uuid())))
                """, conexion);
            comando.Parameters.AddWithValue("paciente", paciente.Valor);
            comando.Parameters.AddWithValue("tipo", TiposDeVisitaSembrados.PrimeraConsulta.Valor);
            comando.Parameters.AddWithValue("inicio", HoraLocal.AUtc(fecha, inicio));
            comando.Parameters.AddWithValue("fin", HoraLocal.AUtc(fecha, fin));
            comando.Parameters.AddWithValue("estado", estado);
            comando.Parameters.AddWithValue("motivoDeCancelacion", (object?)motivoDeCancelacion ?? DBNull.Value);
            comando.Parameters.AddWithValue("origen", enLinea ? "en_linea" : "consultorio");
            comando.Parameters.AddWithValue("canal", enLinea ? "en_linea" : "externo");
            comando.Parameters.AddWithValue("nota", (object?)nota ?? DBNull.Value);
            comando.Parameters.AddWithValue("motivo", (object?)motivo ?? DBNull.Value);
            await comando.ExecuteNonQueryAsync(Cancelacion);
        }

        /// <summary>La Agenda custom de una fecha, escrita directo en la base; sin Tramos, ese día no se atiende.</summary>
        public async Task CustomAsync(DateOnly fecha, string motivo, params (TimeOnly Inicio, TimeOnly Fin)[] tramos)
        {
            await using var conexion = new NpgsqlConnection(cadena);
            await conexion.OpenAsync(Cancelacion);
            await using (var comando = new NpgsqlCommand(
                "insert into agenda.agenda_custom (fecha, motivo, creada_en, actualizada_en) values (@fecha, @motivo, now(), now())",
                conexion))
            {
                comando.Parameters.AddWithValue("fecha", fecha);
                comando.Parameters.AddWithValue("motivo", motivo);
                await comando.ExecuteNonQueryAsync(Cancelacion);
            }

            foreach (var (inicio, fin) in tramos)
            {
                await using var comando = new NpgsqlCommand(
                    "insert into agenda.agenda_custom_tramo (id, fecha, inicio, fin) values (gen_random_uuid(), @fecha, @inicio, @fin)",
                    conexion);
                comando.Parameters.AddWithValue("fecha", fecha);
                comando.Parameters.AddWithValue("inicio", inicio);
                comando.Parameters.AddWithValue("fin", fin);
                await comando.ExecuteNonQueryAsync(Cancelacion);
            }
        }

        public async Task<Resultado<IReadOnlyList<DiaDeLaAgenda>>> ConsultarAsync(DiasDeLaAgenda consulta)
        {
            await using var ambito = Web.Services.CreateAsyncScope();
            FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "administrador"), new Claim(ClaimTypes.Name, "administrador")],
                authenticationType: "Prueba")));

            return await Consulta(ambito).HandleAsync(consulta, Cancelacion);
        }

        public async Task<IReadOnlyList<DiaDeLaAgenda>> ConsultarComoAdministradorAsync(DiasDeLaAgenda consulta)
        {
            var resultado = await ConsultarAsync(consulta);
            Assert.True(resultado.EsExito, $"{resultado.Forma}");
            return resultado.Valor;
        }

        public async ValueTask DisposeAsync()
        {
            await Web.DisposeAsync();
            NpgsqlConnection.ClearPool(new NpgsqlConnection(cadena));
        }
    }
}
