using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Factory.Preset;

namespace RoguelikeServerMVP.Game;

public class Player(Position startPos, int maxHp)
    : Entity(EntityType.Player, startPos, maxHp), IActor
{
    private readonly List<ItemType> _items = [];
    public int Luck { get; private set; } = 0;

    public void TryMove(Direction dir, GameState state)
    {
        var targetPos = Position.Move(dir);

        if (!state.CanMoveTo(targetPos))
            return;

        Position = targetPos;
    }

    public void Attack(Direction dir, GameState state)
    {
        var shootLeftChance = 4 - int.Min(_items.Count(x => x == ItemType.FSharp), 3);
        var shootRightChance = 4 - int.Min(_items.Count(x => x == ItemType.OneF), 3);
        
        if (shootLeftChance != 4 && state.GetRandom().OneIn(shootLeftChance))
            state.AddProjectile(new Tear(dir, _items, Position.Move(DirectionUtil.TurnLeft(dir))));
            
        if (shootRightChance != 4 && state.GetRandom().OneIn(shootRightChance))
            state.AddProjectile(new Tear(dir, _items, Position.Move(DirectionUtil.TurnRight(dir))));
        
        state.AddProjectile(new Tear(dir, _items, Position));
    }
    
    public void PickupItem(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Amethyst: break;
            case ItemType.Heart: Heal(2); break;
            case ItemType.HalfHeart: Heal(1); break;
            case ItemType.Java:
                AddHealth(4);
                _items.Add(itemType);
                break;
            case ItemType.Kotlin:
                AddHealth(2);
                _items.Add(itemType);
                break;
            case ItemType.JavaScript:
            case ItemType.TypeScript:
                Luck++;
                _items.Add(itemType);
                break;
            case ItemType.Cpp:
            case ItemType.Haskell:
            case ItemType.Python3:
            case ItemType.OCaml:
            case ItemType.Zig:
            case ItemType.Rust:
            case ItemType.AnsiC:
            case ItemType.FSharp:
            case ItemType.Roc:
            case ItemType.OneF:
            case ItemType.Go:
            case ItemType.Asm:
            case ItemType.Scala3:
            default: _items.Add(itemType); break;
        }
    }

    public void Teleport(Position pos)
    {
        Position = pos;
        PreviousPosition = pos;
    }

    public void PerformTurn(GameState state) { }
    protected override void OnDeath(GameState state) { }
    protected override void OnDamage(int damage, GameState state) { }

    public List<ItemType> GetItems()
    {
        return _items;
    }
}