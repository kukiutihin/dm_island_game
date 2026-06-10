using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Factory.Preset;

namespace RoguelikeServerMVP.Game;

public class Player(Position startPos, int maxHp)
    : Entity(EntityType.Player, startPos, maxHp), IActor
{
    private readonly List<ItemType> _items = [];

    public void TryMove(Direction dir, GameState state)
    {
        var targetPos = Position.Move(dir);

        if (!state.CanMoveTo(targetPos))
            return;

        Position = targetPos;
    }

    public void Attack(Direction dir, GameState state)
    {
        var tearEntity = new Tear(dir, _items, Position);
        state.AddProjectile(tearEntity);
    }
    
    public void PickupItem(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Amethyst: break;
            case ItemType.Heart: Heal(2); break;
            case ItemType.HalfHeart: Heal(1); break;
            case ItemType.Cpp:
            case ItemType.Haskell:
            case ItemType.Python3:
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
}