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
/// <see cref="CitasDeLaFecha"/>, la consulta de la Agenda del Administrador (CAP-12, AD-22, entrada 5.1), contra
/// PostgreSQL real: todas las Citas de la fecha en orden de inicio, con su estado mostrado (Atendida derivada, RN-31),
/// el nombre en lote desde Pacientes y el motivo. Sin Administrador, no autorizado. Cada prueba usa su base propia y el
/// reloj de la prueba.
/// </summary>
public sealed class CitasDeLaFechaTests(Arnes arnes)
{
    /// <summary>Lunes 9 de noviembre de 2026.</summary>
    private static readonly DateOnly Hoy = new(2026, 11, 9);

    /// <summary>Hoy a las 4:10 pm en Ciudad de México.</summary>
    private static readonly DateTimeOffset Ahora = HoraLocal.AUtc(Hoy, new TimeOnly(16, 10));

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sin_Administrador_la_consulta_vuelve_no_autorizado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity()));

        var resultado = await Consulta(ambito).HandleAsync(new CitasDeLaFecha(Hoy), Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
    }

    [Fact]
    public async Task Las_Citas_de_hoy_van_en_orden_de_inicio_con_su_estado_nombre_y_motivo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteAsync("Ana Sofía López", "4441234567");
        var bruno = await escenario.PacienteAsync("Bruno Díaz", "4449876543");

        await escenario.InsertarAsync(ana, new TimeOnly(16, 0), new TimeOnly(16, 30), "Agendada", null, "Dolor de rodilla");
        await escenario.InsertarAsync(bruno, new TimeOnly(17, 0), new TimeOnly(17, 30), "Agendada", null, "Revisión de hombro");
        await escenario.InsertarAsync(ana, new TimeOnly(17, 30), new TimeOnly(18, 0), "Cancelada", "tarde", "Esguince");
        await escenario.InsertarAsync(bruno, new TimeOnly(15, 30), new TimeOnly(16, 0), "NoSePresento", null, null);
        // Otro día: no aparece.
        await escenario.InsertarAsync(ana, new TimeOnly(16, 0), new TimeOnly(16, 30), "Agendada", null, "Mañana", Hoy.AddDays(1));

        var citas = await escenario.ConsultarComoAdministradorAsync(Hoy);

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
        await escenario.InsertarAsync(ana, new TimeOnly(16, 10), new TimeOnly(16, 40), "Agendada", null, "Dolor");
        await escenario.InsertarAsync(ana, new TimeOnly(16, 40), new TimeOnly(17, 10), "Agendada", null, "Dolor");

        var citas = await escenario.ConsultarComoAdministradorAsync(Hoy);

        Assert.Equal([EstadoEnLaAgenda.Atendida, EstadoEnLaAgenda.Agendada], citas.Select(c => c.Estado));
    }

    [Fact]
    public async Task Un_dia_sin_Citas_es_una_lista_vacia()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        Assert.Empty(await escenario.ConsultarComoAdministradorAsync(Hoy));
    }

    [Fact]
    public async Task Una_Cita_cuyo_Paciente_no_existe_se_lista_sin_nombre()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarAsync(PatientId.Nuevo(), new TimeOnly(17, 0), new TimeOnly(17, 30), "Agendada", null, "Dolor");

        var cita = Assert.Single(await escenario.ConsultarComoAdministradorAsync(Hoy));

        Assert.Null(cita.Paciente);
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
    public async Task El_resultado_no_muestra_nombre_ni_motivo_al_convertirse_en_texto()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteAsync("Ana Sofía López", "4441234567");
        await escenario.InsertarAsync(ana, new TimeOnly(17, 0), new TimeOnly(17, 30), "Agendada", null, "Dolor de rodilla");

        var texto = Assert.Single(await escenario.ConsultarComoAdministradorAsync(Hoy)).ToString();

        Assert.DoesNotContain("Ana", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("rodilla", texto, StringComparison.Ordinal);
    }

    private static IQueryHandler<CitasDeLaFecha, IReadOnlyList<CitaDeLaFecha>> Consulta(AsyncServiceScope ambito) =>
        ambito.ServiceProvider.GetRequiredService<IQueryHandler<CitasDeLaFecha, IReadOnlyList<CitaDeLaFecha>>>();

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

        /// <summary>Una Cita del consultorio escrita directo en la base, hoy (o en <paramref name="fecha"/>), en hora local.</summary>
        public async Task InsertarAsync(
            PatientId paciente, TimeOnly inicio, TimeOnly fin, string estado, string? motivoDeCancelacion, string? motivo,
            DateOnly? fecha = null)
        {
            var dia = fecha ?? Hoy;
            await using var conexion = new NpgsqlConnection(cadena);
            await conexion.OpenAsync(Cancelacion);
            await using var comando = new NpgsqlCommand(
                """
                insert into agenda.cita (id, paciente_id, tipo_de_visita_id, inicio, fin, estado, motivo_de_cancelacion,
                    cancelada_en, origen, nota, motivo_de_consulta, consentimiento_en, consentimiento_version_aviso,
                    consentimiento_canal, creada_en, token_hash)
                values (gen_random_uuid(), @paciente, @tipo, @inicio, @fin, @estado, @motivoDeCancelacion,
                    case when @estado = 'Cancelada' then now() end, 'consultorio', 'Pidió por WhatsApp', @motivo, now(),
                    '2026-11-borrador', 'externo', now(), sha256(uuid_send(gen_random_uuid())))
                """, conexion);
            comando.Parameters.AddWithValue("paciente", paciente.Valor);
            comando.Parameters.AddWithValue("tipo", TiposDeVisitaSembrados.PrimeraConsulta.Valor);
            comando.Parameters.AddWithValue("inicio", HoraLocal.AUtc(dia, inicio));
            comando.Parameters.AddWithValue("fin", HoraLocal.AUtc(dia, fin));
            comando.Parameters.AddWithValue("estado", estado);
            comando.Parameters.AddWithValue("motivoDeCancelacion", (object?)motivoDeCancelacion ?? DBNull.Value);
            comando.Parameters.AddWithValue("motivo", (object?)motivo ?? DBNull.Value);
            await comando.ExecuteNonQueryAsync(Cancelacion);
        }

        public async Task<IReadOnlyList<CitaDeLaFecha>> ConsultarComoAdministradorAsync(DateOnly fecha)
        {
            await using var ambito = Web.Services.CreateAsyncScope();
            FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "administrador"), new Claim(ClaimTypes.Name, "administrador")],
                authenticationType: "Prueba")));

            var resultado = await Consulta(ambito).HandleAsync(new CitasDeLaFecha(fecha), Cancelacion);
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
