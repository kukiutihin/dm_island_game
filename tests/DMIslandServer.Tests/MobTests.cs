using Xunit;
using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game;
using RoguelikeServerMVP.Game.Entities.Factory.Preset;

namespace GameTests;

public class MobTests
{
    [Fact]
    public void ModusPonens_Creation_ShouldSetProperties()
    {
        var mob = new ModusPonens(new Position(2, 3));

        Assert.Equal(EntityType.ModusPonens, mob.Type);
        Assert.Equal(2, mob.Position.X);
        Assert.Equal(3, mob.Position.Y);
        Assert.True(mob.Hp > 0);
        Assert.True(mob.IsAlive);
    }

    [Fact]
    public void Lambda_Creation_ShouldSetType()
    {
        var mob = new Lambda(new Position(0, 0));

        Assert.Equal(EntityType.Lambda, mob.Type);
        Assert.True(mob.IsAlive);
    }

    [Fact]
    public void Mob_TakeDamage_ReducesHpAndCanKill()
    {
        var player = new Player(new Position(0, 0), 10);
        var room = new Room(10, 10);
        var state = new GameState(player, room);

        var mob = new Lambda(new Position(5, 5));
        var startHp = mob.Hp;

        mob.TakeDamage(1, state);
        Assert.Equal(startHp - 1, mob.Hp);
        Assert.True(mob.IsAlive);

        mob.TakeDamage(999, state);
        Assert.False(mob.IsAlive);
    }
}
