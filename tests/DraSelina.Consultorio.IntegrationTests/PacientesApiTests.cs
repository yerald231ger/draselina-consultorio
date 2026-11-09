using DraSelina.Consultorio.Pacientes.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Escenario = DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests.Escenario;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// <see cref="IPacientesApi"/> contra PostgreSQL real, por el contenedor de la web. Cada prueba usa su propia base
/// migrada, con el escenario de <see cref="ReservaEnLineaTests"/>.
/// </summary>
public sealed class PacientesApiTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

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

    [Fact]
    public async Task Pacientes_da_el_nombre_tal_como_se_guardo_y_nada_si_el_Paciente_no_existe()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var api = ambito.ServiceProvider.GetRequiredService<IPacientesApi>();
        var ana = (await api.BuscarOCrearAsync("  Ana   Sofía López ", "4441234567", null, Cancelacion)).Valor;

        Assert.Equal("Ana   Sofía López", await api.NombreAsync(ana, Cancelacion));
        Assert.Null(await api.NombreAsync(DraSelina.Consultorio.Kernel.PatientId.Nuevo(), Cancelacion));
    }

    [Fact]
    public async Task Pacientes_da_los_contactos_con_correo_sin_correo_y_nada_si_el_Paciente_no_existe()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var api = ambito.ServiceProvider.GetRequiredService<IPacientesApi>();
        var ana = (await api.BuscarOCrearAsync("  Ana Sofía López ", "4441234567", "ana@correo.com", Cancelacion)).Valor;
        var luis = (await api.BuscarOCrearAsync("Luis Pérez", "4449876543", null, Cancelacion)).Valor;

        Assert.Equal(new Contactos("Ana Sofía López", "ana@correo.com"), await api.ObtenerContactosAsync(ana, Cancelacion));
        Assert.Equal(new Contactos("Luis Pérez", null), await api.ObtenerContactosAsync(luis, Cancelacion));
        Assert.Null(await api.ObtenerContactosAsync(DraSelina.Consultorio.Kernel.PatientId.Nuevo(), Cancelacion));
    }

    [Fact]
    public void Los_contactos_no_muestran_nombre_ni_correo_al_escribirse()
    {
        var texto = new Contactos("Ana Sofía López", "ana@correo.com").ToString();

        Assert.DoesNotContain("Ana", texto);
        Assert.DoesNotContain("ana@correo.com", texto);
    }
}
