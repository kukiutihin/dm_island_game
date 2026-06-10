using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.ItemBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Pickups;

public sealed class Item(ItemType type, Position position) : Entity(GetEntityType(type), position, Int32.MaxValue)
{
    private readonly IBehaviour _behaviour = new PickupOnCollisionBehaviour();
    
    protected override void OnDeath(GameState state) { }
    protected override void OnDamage(int damage, GameState state) { }

    public ItemType GetItemType() => type;
    private static EntityType GetEntityType(ItemType type)
    {
        return type switch
        {
            ItemType.Haskell => EntityType.HaskellItem,
            ItemType.Python3 => EntityType.Python3Item,
            ItemType.Cpp => EntityType.CppItem,
            ItemType.Heart => EntityType.HeartItem,
            ItemType.HalfHeart => EntityType.HalfHeartItem,
            ItemType.Amethyst => EntityType.AmethystItem,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
    
    public void PerformTurn(GameState state)
    {
        _behaviour.PerformTurn(this, state);
    }
}