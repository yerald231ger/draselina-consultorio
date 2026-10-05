namespace DraSelina.Consultorio.Kernel.Tests;

public class ActorTests
{
    [Fact]
    public void Hay_exactamente_tres_actores()
    {
        Assert.Equal([Actor.Paciente, Actor.Administrador, Actor.Sistema], Enum.GetValues<Actor>());
    }

    [Fact]
    public void El_valor_por_omision_no_es_un_actor()
    {
        Assert.False(Enum.IsDefined(default(Actor)));
    }
}
