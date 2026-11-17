using System.Security.Claims;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// La vista previa <see cref="CitasFueraDelHorario"/> contra PostgreSQL real (RN-9, entrada 6.3): las Citas que
/// quedarían fuera al guardar unos Tramos o al quitar el horario propio, con nombre, "sin correo" y origen. Con el
/// escenario y el reloj de <see cref="ReservaEnLineaTests"/>: lunes 2 de noviembre de 2026 a las 12:00 local; base de
/// lunes a viernes, 3:30 a 6:00 pm; sábado sin atención.
/// </summary>
public sealed class CitasFueraDelHorarioTests(Arnes arnes)
{
    private static readonly DateOnly Sabado = new(2026, 11, 7);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Al_guardar_lista_las_que_no_caben_con_su_Paciente_correo_y_origen()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        // 5:00 pm del Consultorio, de un Paciente que Pacientes no conoce; 5:30 pm en línea, de Ana con correo; 4:00 pm cabe.
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(17, 0), new TimeOnly(17, 30));
        Assert.True((await escenario.AgendarAsync(Comando(Manana, new TimeOnly(17, 30)))).EsExito);
        Assert.True((await escenario.AgendarAsync(Comando(Manana, new TimeOnly(16, 0)))).EsExito);

        var vista = await VistaAsync(escenario, CitasFueraDelHorario.AlGuardar(Manana, [new(new(15, 30), new(17, 0))]));

        Assert.Equal([new TramoDeLaAgenda(new(15, 30), new(17, 0))], vista.Tramos);
        Assert.Collection(
            vista.Citas,
            c =>
            {
                Assert.Equal((new TimeOnly(17, 0), new TimeOnly(17, 30)), (c.Inicio, c.Fin));
                Assert.Null(c.Paciente);
                Assert.True(c.SinCorreo);
                Assert.True(c.DelConsultorio);
            },
            c =>
            {
                Assert.Equal(new TimeOnly(17, 30), c.Inicio);
                Assert.Equal("Ana Sofía López", c.Paciente);
                Assert.False(c.SinCorreo);
                Assert.False(c.DelConsultorio);
                Assert.DoesNotContain("Ana", c.ToString(), StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task Al_quitar_compara_con_la_base_de_ese_dia()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCustomAsync(Sabado, "sábado abierto", (new TimeOnly(10, 0), new TimeOnly(13, 0)));
        await escenario.InsertarCitaDelConsultorioAsync(Sabado, new TimeOnly(10, 0), new TimeOnly(10, 30));
        await escenario.InsertarCustomAsync(Manana, "cirugía", (new TimeOnly(15, 30), new TimeOnly(17, 0)));

        var sabado = await VistaAsync(escenario, CitasFueraDelHorario.AlQuitar(Sabado));
        var martes = await VistaAsync(escenario, CitasFueraDelHorario.AlQuitar(Manana));

        Assert.Empty(sabado.Tramos);
        Assert.Equal(new TimeOnly(10, 0), Assert.Single(sabado.Citas).Inicio);
        Assert.Equal([new TramoDeLaAgenda(new(15, 30), new(18, 0))], martes.Tramos);
        Assert.Empty(martes.Citas);
    }

    [Fact]
    public async Task Tramos_al_reves_son_invalidos_y_sin_sesion_no_autorizado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var alReves = await PedirAsync(escenario, CitasFueraDelHorario.AlGuardar(Sabado, [new(new(13, 0), new(10, 0))]), comoAdministrador: true);
        var sinSesion = await PedirAsync(escenario, CitasFueraDelHorario.AlQuitar(Sabado), comoAdministrador: false);

        Assert.Equal(FormaDeResultado.Invalido, alReves.Forma);
        Assert.Equal(FormaDeResultado.NoAutorizado, sinSesion.Forma);
    }

    private static async Task<HorarioConCitasFuera> VistaAsync(Escenario escenario, CitasFueraDelHorario consulta)
    {
        var resultado = await PedirAsync(escenario, consulta, comoAdministrador: true);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    private static async Task<Resultado<HorarioConCitasFuera>> PedirAsync(Escenario escenario, CitasFueraDelHorario consulta, bool comoAdministrador)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        if (comoAdministrador)
        {
            Arnes.FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "cuenta-del-administrador"), new Claim(ClaimTypes.Name, "administradora")],
                authenticationType: "Prueba")));
        }
        else
        {
            SinSesion(ambito);
        }

        return await ambito.ServiceProvider.GetRequiredService<IQueryHandler<CitasFueraDelHorario, HorarioConCitasFuera>>()
            .HandleAsync(consulta, Cancelacion);
    }
}
