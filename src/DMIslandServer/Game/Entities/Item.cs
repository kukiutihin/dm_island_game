using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities;

public class Item(EntityType type, Position position) : Entity(type, position, Int32.MaxValue)
{
    protected override void OnDeath(GameState state) { }

    protected override void OnDamage(int damage, GameState state) { }
}