namespace DraSelina.Consultorio.Kernel.Tests;

public class IdsTests
{
    [Fact]
    public void Los_ids_nuevos_son_Guid_version_7()
    {
        Assert.Equal(7, CitaId.Nuevo().Valor.Version);
        Assert.Equal(7, PatientId.Nuevo().Valor.Version);
        Assert.Equal(7, TipoDeVisitaId.Nuevo().Valor.Version);
    }

    [Fact]
    public void Dos_ids_nuevos_son_distintos()
    {
        Assert.NotEqual(CitaId.Nuevo(), CitaId.Nuevo());
        Assert.NotEqual(PatientId.Nuevo(), PatientId.Nuevo());
        Assert.NotEqual(TipoDeVisitaId.Nuevo(), TipoDeVisitaId.Nuevo());
    }

    [Fact]
    public void Dos_ids_del_mismo_tipo_con_el_mismo_Guid_son_iguales()
    {
        var guid = Guid.CreateVersion7();

        Assert.Equal(new CitaId(guid), new CitaId(guid));
        Assert.Equal(guid.ToString(), new CitaId(guid).ToString());
    }

    [Fact]
    public void Dos_ids_de_tipo_distinto_no_son_intercambiables()
    {
        var guid = Guid.CreateVersion7();
        Type[] ids = [typeof(CitaId), typeof(PatientId), typeof(TipoDeVisitaId)];

        // Con el mismo Guid, un id nunca es igual al de otro tipo...
        Assert.False(new CitaId(guid).Equals((object)new PatientId(guid)));
        Assert.False(new PatientId(guid).Equals((object)new TipoDeVisitaId(guid)));
        Assert.False(new TipoDeVisitaId(guid).Equals((object)new CitaId(guid)));

        // ...y el compilador no deja pasar uno por otro, ni por un Guid desnudo: no hay conversiones.
        foreach (var id in ids)
        {
            Assert.DoesNotContain(id.GetMethods(), m => m.Name is "op_Implicit" or "op_Explicit");
            Assert.All(ids.Append(typeof(Guid)).Where(otro => otro != id), otro =>
            {
                Assert.False(id.IsAssignableFrom(otro));
                Assert.False(otro.IsAssignableFrom(id));
            });
        }
    }
}
