using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;
using RoguelikeServerMVP.Game.Events;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset;

public class Tear(Direction direction, IEnumerable<ItemType> items, Position position) : Projectile(EntityType.Tear, position, 1)
{
    private readonly IBehaviour _behaviour = new ProjectileBehaviourBuilder(direction, items).Build();
    
    public override bool IsBlocking => false;
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }

    protected override void OnDeath(GameState state)
    {
        state.AddEvent(new Event(EventType.TearPop, Position, Type.ToString()));
    }

    protected override void OnDamage(int damage, GameState state)
    {
        
    }
}