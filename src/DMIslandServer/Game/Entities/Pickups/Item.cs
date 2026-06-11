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
            ItemType.Java => EntityType.JavaItem,
            ItemType.Heart => EntityType.HeartItem,
            ItemType.HalfHeart => EntityType.HalfHeartItem,
            ItemType.Amethyst => EntityType.AmethystItem,
            ItemType.OCaml => EntityType.OCamlItem,
            ItemType.Zig => EntityType.ZigItem,
            ItemType.Rust => EntityType.RustItem,
            ItemType.AnsiC => EntityType.AnsiCItem,
            ItemType.FSharp => EntityType.FSharpItem,
            ItemType.Roc => EntityType.RocItem,
            ItemType.OneF => EntityType.OneFItem,
            ItemType.JavaScript => EntityType.JavaScriptItem,
            ItemType.TypeScript => EntityType.TypeScriptItem,
            ItemType.Go => EntityType.GoItem,
            ItemType.Kotlin => EntityType.KotlinItem,
            ItemType.Asm => EntityType.AsmItem,
            ItemType.Scala3 => EntityType.Scala3Item,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
    
    public void PerformTurn(GameState state)
    {
        _behaviour.PerformTurn(this, state);
    }
}