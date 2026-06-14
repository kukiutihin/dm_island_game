using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Debuffs;
using RoguelikeServerMVP.Game.Entities.Pickups;
using RoguelikeServerMVP.Game.Events;

namespace RoguelikeServerMVP.Game.Entities;

public abstract class Mob(EntityType name, Position position, int maxHp) : Entity(name, position, maxHp), IActor
{
    public override bool IsBlocking => true;
    private readonly List<Debuff> _debuffs = [];

    protected virtual void DropLoot(GameState state)
    {
        switch (state.GetRandom().Random.Next(20))
        {
            case 0:
                state.Items.Add(new Item(ItemType.HalfHeart, Position));
                break;
            
            case 1:
                state.Items.Add(new Item(ItemType.Heart, Position));
                break;
            
            case 2:
                state.Items.Add(new Item(ItemType.Amethyst, Position));
                break;
        }
    }

    protected override void OnDeath(GameState state)
    {
        DropLoot(state);
        state.AddEvent(new Event(EventType.EntityDeath, Position, Id.ToString()));
    }

    protected override void OnDamage(int damage, GameState state)
    {
        _debuffs.Add(new Debuff(DebuffType.Stunned, 3));
    }
    
    public override void TryMoveTo(Position target)
    {
        if (_debuffs.Exists(x => x.DebuffType == DebuffType.Stunned))
            return;
        
        base.TryMoveTo(target);
    }
    

    public virtual void PerformTurn(GameState state)
    {
        PreviousPosition = Position;
        _debuffs.RemoveAll(x => x.Duration-- < 0);
    }
}
