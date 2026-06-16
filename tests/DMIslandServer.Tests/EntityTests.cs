using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game;

namespace GameTests;

/// <summary>
/// The shared Entity health model (damage clamping, healing, death) plus player-specific
/// behaviour: ranged attacks spawn projectiles, item pickups change stats, and blocking
/// entities stop movement.
/// </summary>
public class EntityTests
{
    private static GameState NewState(out Player player)
    {
        player = new Player(new Position(5, 5), 10);
        return new GameState(player, new Room(12, 12));
    }

    [Fact]
    public void TakeDamage_ReducesHpAndClampsAtZero()
    {
        var state = NewState(out var player);
        player.TakeDamage(3, state);
        Assert.Equal(7, player.Hp);

        player.TakeDamage(999, state);
        Assert.Equal(0, player.Hp);
        Assert.False(player.IsAlive);
    }

    [Fact]
    public void TakeDamage_OnDeadEntity_IsNoop()
    {
        var state = NewState(out var player);
        player.Kill(state);
        Assert.False(player.IsAlive);
        player.TakeDamage(5, state); // should not throw or go negative
        Assert.Equal(0, player.Hp);
    }

    [Fact]
    public void AddHealth_RaisesMaxAndCurrent()
    {
        var state = NewState(out var player);
        player.AddHealth(4);
        Assert.Equal(14, player.MaxHp);
        Assert.Equal(14, player.Hp);
    }

    [Fact]
    public void RestoreFullHealth_RefillsToMax()
    {
        var state = NewState(out var player);
        player.TakeDamage(6, state);
        player.RestoreFullHealth();
        Assert.Equal(player.MaxHp, player.Hp);
    }

    [Fact]
    public void Player_Attack_SpawnsAtLeastOneProjectile()
    {
        var state = NewState(out var player);
        Assert.Empty(state.Projectiles);
        player.Attack(Direction.Right, state);
        Assert.NotEmpty(state.Projectiles);
    }

    [Fact]
    public void Player_PickupHeart_Heals()
    {
        var state = NewState(out var player);
        player.TakeDamage(4, state); // hp 6
        player.PickupItem(ItemType.Heart);
        Assert.Equal(8, player.Hp); // Heart heals 2
    }

    [Fact]
    public void Player_PickupJava_IncreasesMaxHpAndIsRecorded()
    {
        var state = NewState(out var player);
        player.PickupItem(ItemType.Java);
        Assert.Equal(14, player.MaxHp); // Java grants +4 max
        Assert.Contains(ItemType.Java, player.GetItems());
    }

    [Fact]
    public void Player_TryMove_BlockedByMob_DoesNotMove()
    {
        var state = NewState(out var player);
        state.AddMob(new RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob.Lambda(new Position(6, 5)));
        player.TryMove(Direction.Right, state);
        Assert.Equal(new Position(5, 5), player.Position);
    }

    [Fact]
    public void Player_Teleport_SetsPositionAndPrevious()
    {
        var state = NewState(out var player);
        player.Teleport(new Position(3, 9));
        Assert.Equal(new Position(3, 9), player.Position);
        Assert.Equal(new Position(3, 9), player.PreviousPosition);
    }
}
